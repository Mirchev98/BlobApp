using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobApp.Services.Interfaces;
using BlobApp.Services.Models.StorageService;
using Microsoft.Extensions.Configuration;

namespace BlobApp.Services
{
    public class StorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "blobappcontainer";
        private readonly IEncryptionService _encryptionService;
        private readonly string _cacheDirectory;

        public StorageService(IConfiguration configuration, IEncryptionService encryptionService)
        {
            _blobServiceClient = new BlobServiceClient(configuration.GetConnectionString("AzureBlobStorage"));
            _encryptionService = encryptionService;
            _cacheDirectory = configuration["CacheSettings:Directory"];
        }

        //Uploads files to the local directory
        public async Task UploadFileAsync(Stream fileStream, string fileName, IDictionary<string, string> tags)
        {
            var localFilePath = Path.Combine(_cacheDirectory, fileName);

            using (var localFileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(localFileStream);
            }

            File.SetLastAccessTime(localFilePath, DateTime.UtcNow);

            // TO DO: Find some way to save the tags as well
        }

        // Download a file based on its name
        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var localFilePath = Path.Combine(_cacheDirectory, fileName);

            if (File.Exists(localFilePath))
            {
                // Update the last access time if the file is found locally
                File.SetLastAccessTime(localFilePath, DateTime.UtcNow);
                var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // Return the file stream
                return fileStream;
            }
            else
            {
                // File not found locally, download from Blob Storage
                var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
                var blobClient = containerClient.GetBlobClient(fileName);

                if (await blobClient.ExistsAsync())
                {
                    try
                    {
                        var downloadInfo = await blobClient.DownloadAsync();
                        var memoryStream = new MemoryStream();
                        await downloadInfo.Value.Content.CopyToAsync(memoryStream);
                        memoryStream.Position = 0;

                        var decryptedStream = DecryptStream(memoryStream);

                        using (var localFileStream = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                        {
                            await decryptedStream.CopyToAsync(localFileStream);
                        }

                        return memoryStream;
                    }
                    catch (Exception ex)
                    {
                        throw new ApplicationException($"Error downloading file {fileName}.", ex);
                    }
                }
                else
                {
                    throw new FileNotFoundException($"The file {fileName} was not found in Blob Storage.");
                }
            }
        }



        // Delete a file based on its name
        public async Task DeleteFileAsync(string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                try
                {
                    await blobClient.DeleteIfExistsAsync();
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error deleting file {fileName}.", ex);
                }
            }
            else
            {
                throw new FileNotFoundException($"The file {fileName} was not found.");
            }
        }

        // List all files in the storage container
        public async Task<List<string>> ListFilesAsync()
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobItems = new List<string>();

            try
            {
                await foreach (var blob in containerClient.GetBlobsAsync())
                {
                    blobItems.Add(blob.Name);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error listing files.", ex);
            }

            return blobItems;
        }

        // Search files by tag key and value
        public async Task<List<string>> SearchFilesByTagAsync(string tagKey, string tagValue)
        {
            var blobsWithTag = new List<string>();
            string searchExpression = $"\"{tagKey}\" = '{tagValue}'";

            try
            {
                await foreach (var page in _blobServiceClient.FindBlobsByTagsAsync(searchExpression).AsPages())
                {
                    foreach (var blob in page.Values)
                    {
                        blobsWithTag.Add(blob.BlobName);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error searching files by tag {tagKey}={tagValue}.", ex);
            }

            return blobsWithTag;
        }

        // List all files along with their tags
        public async Task<IEnumerable<FileTags>> ListFilesWithTagsAsync()
        {
            var filesWithTags = new List<FileTags>();
            var blobContainerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

            await foreach (var blobItem in blobContainerClient.GetBlobsAsync())
            {
                var blobClient = blobContainerClient.GetBlobClient(blobItem.Name);
                var tagsResult = await blobClient.GetTagsAsync();
                var tags = tagsResult.Value.Tags;

                filesWithTags.Add(new FileTags
                {
                    FileName = blobItem.Name,
                    Tags = tags
                });
            }

            return filesWithTags;
        }

        //Checks if there are any files to be moved to the blob storage
        public async Task MoveOldFilesToBlobAsync()
        {
            var files = Directory.GetFiles(_cacheDirectory);
            foreach (var file in files)
            {
                var lastAccessTime = File.GetLastAccessTimeUtc(file);
                if ((DateTime.UtcNow - lastAccessTime).TotalSeconds >= 50)
                {
                    using (var fileStream = File.OpenRead(file))
                    {
                        await UploadFileToBlobAsync(fileStream, Path.GetFileName(file));
                    }
                    File.Delete(file); // Delete the local file after uploading to Blob Storage
                }
            }
        }

        //Uploads files to the blob storage
        private async Task UploadFileToBlobAsync(Stream fileStream, string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(fileName);

            try
            {
                using (var encryptedStream = EncryptStream(fileStream))
                {
                    await blobClient.UploadAsync(encryptedStream, overwrite: true);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error uploading file {fileName} to Blob Storage.", ex);
            }
        }

        // Encrypt a stream of data
        private Stream EncryptStream(Stream dataStream)
        {
            var data = StreamToByteArray(dataStream);
            var encryptedData = _encryptionService.EncryptData(data);
            return new MemoryStream(encryptedData);
        }

        // Decrypt a stream of data
        private Stream DecryptStream(Stream encryptedStream)
        {
            var encryptedData = StreamToByteArray(encryptedStream);
            var decryptedData = _encryptionService.DecryptData(encryptedData);
            return new MemoryStream(decryptedData);
        }

        // Convert a Stream to a byte array
        private byte[] StreamToByteArray(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public void SetDownloadedFileFromBlobTime(string fileName)
        {
            var localFilePath = Path.Combine(_cacheDirectory, fileName);
            File.SetLastAccessTime(localFilePath, DateTime.UtcNow);
        }
    }
}

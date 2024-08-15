using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobApp.Services.Interfaces;
using BlobApp.Services.Models.StorageService;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BlobApp.Services
{
    public class StorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName = "blobappcontainer";
        private readonly IEncryptionService _encryptionService;

        public StorageService(IConfiguration configuration, IEncryptionService encryptionService)
        {
            _blobServiceClient = new BlobServiceClient(configuration.GetConnectionString("AzureBlobStorage"));
            _encryptionService = encryptionService;
        }

        // Upload a file and its associated tags
        public async Task UploadFileAsync(Stream fileStream, string fileName, IDictionary<string, string> tags)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(fileName);

            try
            {
                // Encrypt the file stream before uploading
                using (var encryptedStream = EncryptStream(fileStream))
                {
                    await blobClient.UploadAsync(encryptedStream, overwrite: true);
                }

                if (tags != null && tags.Any())
                {
                    await blobClient.SetTagsAsync(tags);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException($"Error uploading file {fileName}.", ex);
            }
        }

        // Download a file based on its name
        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                try
                {
                    var downloadInfo = await blobClient.DownloadAsync();
                    // Decrypt the file stream after downloading
                    return DecryptStream(downloadInfo.Value.Content);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException($"Error downloading file {fileName}.", ex);
                }
            }
            else
            {
                throw new FileNotFoundException($"The file {fileName} was not found.");
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
    }
}

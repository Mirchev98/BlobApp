using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using BlobApp.Services.Interfaces;
using Microsoft.Extensions.Configuration;
namespace BlobApp.Services
{
	public class StorageService : IStorageService
	{
		private readonly BlobServiceClient _blobServiceClient;
		private readonly string _containerName = "blobappcontainer";

		public StorageService(IConfiguration configuration)
		{
			_blobServiceClient = new BlobServiceClient(configuration.GetConnectionString("AzureBlobStorage"));
		}


		public async Task UploadFileAsync(Stream fileStream, string fileName, IDictionary<string, string> tags)
		{
			var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
			containerClient.CreateIfNotExists(PublicAccessType.Blob);

			var blobClient = containerClient.GetBlobClient(fileName);
			await blobClient.UploadAsync(fileStream, true);
			await blobClient.SetTagsAsync(tags);
		}

		public async Task<Stream> DownloadFileAsync(string fileName)
		{
			var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
			var blobClient = containerClient.GetBlobClient(fileName);
			var downloadInfo = await blobClient.DownloadAsync();
			return downloadInfo.Value.Content;
		}

		public async Task DeleteFileAsync(string fileName)
		{
			var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
			var blobClient = containerClient.GetBlobClient(fileName);
			await blobClient.DeleteIfExistsAsync();
		}

		public async Task<List<string>> ListFilesAsync()
		{
			var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
			var blobItems = new List<string>();

			await foreach (var blob in containerClient.GetBlobsAsync())
			{
				blobItems.Add(blob.Name);
			}

			return blobItems;
		}

		public async Task<List<string>> SearchFilesByTagAsync(string tagKey, string tagValue)
		{
			var query = $"@{tagKey}='{tagValue}'";
			var taggedBlobs = new List<string>();

			await foreach (var blob in _blobServiceClient.FindBlobsByTagsAsync(query))
			{
				taggedBlobs.Add(blob.BlobName);
			}

			return taggedBlobs;
		}
	}
}

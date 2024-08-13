using BlobApp.Services.Models.StorageService;

namespace BlobApp.Services.Interfaces
{
    public interface IStorageService
	{
		Task UploadFileAsync(Stream fileStream, string fileName, IDictionary<string, string> tags);
		Task<Stream> DownloadFileAsync(string fileName);
		Task DeleteFileAsync(string fileName);
		Task<List<string>> ListFilesAsync();
		Task<List<string>> SearchFilesByTagAsync(string tagKey, string tagValue);

        Task<IEnumerable<FileTags>> ListFilesWithTagsAsync();

    }
}

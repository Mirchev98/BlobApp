using BlobApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BlobApp.Controllers
{
	public class StorageController : Controller
	{
		private readonly IStorageService _storageService;

		public StorageController(IStorageService storageService)
		{
			_storageService = storageService;
		}

		public async Task<IActionResult> MainPage()
		{
			var files = await _storageService.ListFilesAsync();
			return View(files);
		}

		[HttpPost]
		public async Task<IActionResult> Upload(IFormFile file, IDictionary<string, string> tags)
		{
			if (file.Length > 0)
			{
				using (var stream = new MemoryStream())
				{
					await file.CopyToAsync(stream);
					stream.Position = 0;
					await _storageService.UploadFileAsync(stream, file.FileName, tags);
				}
			}
			return RedirectToAction("MainPage");
		}

		public async Task<IActionResult> Download(string fileName)
		{
			var stream = await _storageService.DownloadFileAsync(fileName);
			return File(stream, "application/octet-stream", fileName);
		}

		[HttpPost]
		public async Task<IActionResult> Delete(string fileName)
		{
			await _storageService.DeleteFileAsync(fileName);
			return RedirectToAction("MainPage");
		}

		public async Task<IActionResult> Search(string tagKey, string tagValue)
		{
			var files = await _storageService.SearchFilesByTagAsync(tagKey, tagValue);
			return View("Main", files);
		}
	}
}

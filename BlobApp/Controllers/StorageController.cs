using BlobApp.Models.Storage;
using BlobApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

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
            var filesWithTags = await _storageService.ListFilesWithTagsAsync();

            var result = filesWithTags.Select(fileTag => new FileTagsViewModel
            {
                FileName = fileTag.FileName,
                Tags = fileTag.Tags
            }).ToList();

            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string tagKey, string tagValue)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "No file selected or file is empty.";
                return RedirectToAction("MainPage");
            }

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                var tags = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(tagKey) && !string.IsNullOrEmpty(tagValue))
                {
                    tags.Add(tagKey, tagValue);
                }

                await _storageService.UploadFileAsync(stream, file.FileName, tags);
                TempData["Success"] = "File uploaded successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error uploading file: {ex.Message}";
            }

            return RedirectToAction("MainPage");
        }

        [HttpPost]
        public async Task<IActionResult> Download(string fileName)
        {
            try
            {
                var stream = await _storageService.DownloadFileAsync(fileName);
                var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                return File(memoryStream, "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error downloading file: {ex.Message}";
                return RedirectToAction("MainPage");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string fileName)
        {
            try
            {
                await _storageService.DeleteFileAsync(fileName);
                TempData["Success"] = "File deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting file: {ex.Message}";
            }

            return RedirectToAction("MainPage");
        }

        [HttpPost]
        public async Task<IActionResult> Search(string tagKey, string tagValue)
        {
            if (string.IsNullOrEmpty(tagKey) || string.IsNullOrEmpty(tagValue))
            {
                TempData["Error"] = "Tag key and value are required for search.";
                return RedirectToAction("MainPage");
            }

            try
            {
                var files = await _storageService.SearchFilesByTagAsync(tagKey, tagValue);

                var result = files.Select(fileName => new FileTagsViewModel
                {
                    FileName = fileName,
                    Tags = new Dictionary<string, string>()
                }).ToList();

                return View("MainPage", result);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error searching files: {ex.Message}";
                return RedirectToAction("MainPage");
            }
        }

        [HttpPost]
        public async Task<IActionResult> MoveOldFilesToBlob()
        {
            try
            {
                await _storageService.MoveOldFilesToBlobAsync();
                TempData["Success"] = "Old files moved to blob storage successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error moving old files: {ex.Message}";
            }

            return RedirectToAction("MainPage");
        }
    }
}

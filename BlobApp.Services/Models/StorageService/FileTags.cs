namespace BlobApp.Services.Models.StorageService
{
    public class FileTags
    {
        public string FileName { get; set; }
        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }
}

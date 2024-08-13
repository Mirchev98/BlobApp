namespace BlobApp.Models.Storage
{
    public class FileTagsViewModel
    {
        public string FileName { get; set; }
        public IDictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }
}

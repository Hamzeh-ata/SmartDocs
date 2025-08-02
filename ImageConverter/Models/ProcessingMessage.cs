using ImageConverter.Enums;

namespace ImageConverter.Models
{
    public class ProcessingMessage
    {
        public string JobId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public JobType JobType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
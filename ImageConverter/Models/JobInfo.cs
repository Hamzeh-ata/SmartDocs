using ImageConverter.Enums;

namespace ImageConverter.Models
{
    public class JobInfo
    {
        public string JobId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string? ResultPath { get; set; }
        public JobType JobType { get; set; }
        public JobStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}

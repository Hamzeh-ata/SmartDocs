using ImageConverter.Enums;

namespace ImageConverter.Models
{
    public class JobResponse
    {
        public string JobId { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public JobType JobType { get; set; }
        public JobStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsDownloadReady => Status == JobStatus.Completed && !string.IsNullOrEmpty(ResultPath);
        public string? ResultPath { get; set; }
    }
}

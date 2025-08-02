using ImageConverter.Enums;

namespace ImageConverter.Models
{
    public class UploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public JobType JobType { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public string? WatermarkText { get; set; }
    }

}

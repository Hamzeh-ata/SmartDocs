using ImageConverter.Enums;
using ImageConverter.Models;
using ImageConverter.Services.FileStorage;
using ImageConverter.Services.JobStatus;
using ImageConverter.Services.RabbitMq;
using Microsoft.AspNetCore.Mvc;

namespace ImageConverter.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController : ControllerBase
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IJobStatusService _jobStatusService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IRabbitMQService rabbitMQService,
            IJobStatusService jobStatusService,
            IFileStorageService fileStorageService,
            ILogger<DocumentsController> logger)
        {
            _rabbitMQService = rabbitMQService;
            _jobStatusService = jobStatusService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<ActionResult<JobResponse>> UploadFile([FromForm] UploadRequest request)
        {

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("No file provided");
            }

            var jobId = Guid.NewGuid().ToString();
            var fileName = $"{jobId}_{request.File.FileName}";
            var filePath = await _fileStorageService.SaveFileAsync(request.File, fileName);

            var jobInfo = new JobInfo
            {
                JobId = jobId,
                OriginalFileName = request.File.FileName,
                FilePath = filePath,
                JobType = request.JobType,
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            var parameters = new Dictionary<string, object>();
            if (request.Width.HasValue) parameters["Width"] = request.Width.Value;
            if (request.Height.HasValue) parameters["Height"] = request.Height.Value;
            if (!string.IsNullOrEmpty(request.WatermarkText)) parameters["WatermarkText"] = request.WatermarkText;

            jobInfo.Parameters = parameters;

            _jobStatusService.CreateJob(jobInfo);

            var message = new ProcessingMessage
            {
                JobId = jobId,
                FilePath = filePath,
                JobType = request.JobType,
                Parameters = parameters
            };

            var queueName = GetQueueName(request.JobType);
            await _rabbitMQService.PublishMessageAsync(message, queueName);

            var response = MapToJobResponse(jobInfo);
            return Ok(response);
        }


        [HttpGet("status/{jobId}")]
        public ActionResult<JobResponse> GetJobStatus(string jobId)
        {
            var job = _jobStatusService.GetJob(jobId);
            if (job == null)
            {
                return NotFound($"Job {jobId} not found");
            }

            return Ok(MapToJobResponse(job));
        }

        [HttpGet("jobs")]
        public ActionResult<List<JobResponse>> GetAllJobs()
        {
            var jobs = _jobStatusService.GetAllJobs();
            var responses = jobs.Select(MapToJobResponse).ToList();
            return Ok(responses);
        }

        [HttpGet("download/{jobId}")]
        public async Task<IActionResult> DownloadResult(string jobId)
        {

            var job = _jobStatusService.GetJob(jobId);
            if (job == null)
            {
                return NotFound($"Job {jobId} not found");
            }

            if (job.Status != JobStatus.Completed || string.IsNullOrEmpty(job.ResultPath))
            {
                return BadRequest("File not ready for download");
            }

            if (!_fileStorageService.FileExists(job.ResultPath))
            {
                return NotFound("Result file not found");
            }

            var fileData = await _fileStorageService.GetFileAsync(job.ResultPath);

            var fileName = GenerateDownloadFileName(job);
            var contentType = GetContentType(job.JobType);

            return File(fileData, contentType, fileName);


        }

        [HttpDelete("job/{jobId}")]
        public IActionResult DeleteJob(string jobId)
        {

            var job = _jobStatusService.GetJob(jobId);
            if (job == null)
            {
                return NotFound($"Job {jobId} not found");
            }

            if (_fileStorageService.FileExists(job.FilePath))
            {
                _fileStorageService.DeleteFile(job.FilePath);
            }

            if (!string.IsNullOrEmpty(job.ResultPath) && _fileStorageService.FileExists(job.ResultPath))
            {
                _fileStorageService.DeleteFile(job.ResultPath);
            }

            return Ok($"Job {jobId} deleted successfully");

        }

        private static string GetQueueName(JobType jobType)
        {
            return jobType switch
            {
                JobType.ConvertToPDF => "document_processing",
                JobType.ResizeImage => "image_processing",
                JobType.AddWatermark => "image_processing",
                JobType.ConvertToJPG => "image_processing",
                JobType.ConvertToPNG => "image_processing",
                _ => "document_processing"
            };
        }

        private static JobResponse MapToJobResponse(JobInfo job)
        {
            return new JobResponse
            {
                JobId = job.JobId,
                OriginalFileName = job.OriginalFileName,
                JobType = job.JobType,
                Status = job.Status,
                ErrorMessage = job.ErrorMessage,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt,
                ResultPath = job.ResultPath
            };
        }

        private static string GenerateDownloadFileName(JobInfo job)
        {
            var baseName = Path.GetFileNameWithoutExtension(job.OriginalFileName);
            var extension = job.JobType switch
            {
                JobType.ConvertToPDF => "pdf",
                JobType.ConvertToJPG => "jpg",
                JobType.ConvertToPNG => "png",
                JobType.ResizeImage => "jpg",
                JobType.AddWatermark => "jpg",
                _ => "bin"
            };
            return $"{baseName}_processed.{extension}";
        }

        private static string GetContentType(JobType jobType)
        {
            return jobType switch
            {
                JobType.ConvertToPDF => "application/pdf",
                JobType.ConvertToJPG => "image/jpeg",
                JobType.ConvertToPNG => "image/png",
                JobType.ResizeImage => "image/jpeg",
                JobType.AddWatermark => "image/jpeg",
                _ => "application/octet-stream"
            };
        }
    }
}

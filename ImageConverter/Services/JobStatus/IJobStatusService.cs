using ImageConverter.Models;

namespace ImageConverter.Services.JobStatus
{
    public interface IJobStatusService
    {
        void CreateJob(JobInfo job);
        JobInfo? GetJob(string jobId);
        List<JobInfo> GetAllJobs();
        void UpdateJobStatus(string jobId, Enums.JobStatus status, string? errorMessage = null, string? resultPath = null);
        void CleanupOldJobs();
    }
}

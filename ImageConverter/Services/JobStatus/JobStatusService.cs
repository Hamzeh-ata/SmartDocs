using ImageConverter.Models;
using System.Collections.Concurrent;

namespace ImageConverter.Services.JobStatus
{
    public class JobStatusService : IJobStatusService
    {
        private readonly ConcurrentDictionary<string, JobInfo> _jobs = new();
        private readonly Timer _cleanupTimer;

        public JobStatusService()
        {
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }
        public void CreateJob(JobInfo job)
        {
            _jobs[job.JobId] = job;
        }

        public JobInfo? GetJob(string jobId)
        {
            _jobs.TryGetValue(jobId, out var job);
            return job;
        }

        public List<JobInfo> GetAllJobs()
        {
            return _jobs.Values.OrderByDescending(j => j.CreatedAt).ToList();
        }

        public void UpdateJobStatus(string jobId, Enums.JobStatus status, string? errorMessage = null, string? resultPath = null)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = status;
                if (errorMessage != null)
                    job.ErrorMessage = errorMessage;
                if (resultPath != null)
                    job.ResultPath = resultPath;
                if (status == Enums.JobStatus.Completed || status == Enums.JobStatus.Failed)
                    job.CompletedAt = DateTime.UtcNow;
            }
        }

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var oldJobIds = _jobs.Values
                .Where(job => job.CreatedAt < cutoffTime &&
                             (job.Status == Enums.JobStatus.Completed || job.Status == Enums.JobStatus.Failed))
                .Select(job => job.JobId)
                .ToList();

            foreach (var jobId in oldJobIds)
            {
                _jobs.TryRemove(jobId, out _);
            }
        }

        private void CleanupCallback(object? state)
        {
            CleanupOldJobs();
        }
    }
}

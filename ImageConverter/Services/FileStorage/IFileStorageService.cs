namespace ImageConverter.Services.FileStorage
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(IFormFile file, string fileName);
        Task<byte[]> GetFileAsync(string filePath);
        Task SaveProcessedFileAsync(string filePath, byte[] fileData);
        bool FileExists(string filePath);
        void DeleteFile(string filePath);
        string GetUploadsPath();
        string GetResultsPath();
    }
}

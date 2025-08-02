namespace ImageConverter.Services.FileStorage
{
    public class FileStorageService : IFileStorageService
    {
        private readonly string _uploadsPath;
        private readonly string _resultsPath;

        public FileStorageService()
        {
            _uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            _resultsPath = Path.Combine(Directory.GetCurrentDirectory(), "results");

            Directory.CreateDirectory(_uploadsPath);
            Directory.CreateDirectory(_resultsPath);
        }

        public async Task<string> SaveFileAsync(IFormFile file, string fileName)
        {
            var filePath = Path.Combine(_uploadsPath, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(fileStream);

            return filePath;
        }

        public async Task<byte[]> GetFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            return await File.ReadAllBytesAsync(filePath);
        }

        public async Task SaveProcessedFileAsync(string filePath, byte[] fileData)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(filePath, fileData);
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public string GetUploadsPath()
        {
            return _uploadsPath;
        }

        public string GetResultsPath()
        {
            return _resultsPath;
        }
    }
}

using ImageConverter.Enums;
using ImageConverter.Models;
using ImageConverter.Services.FileStorage;
using ImageConverter.Services.JobStatus;
using ImageConverter.Services.RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Text;
using System.Text.Json;

namespace ImageConverter.Services.ImageProcessor
{
    public class ImageProcessorWorker : BackgroundService
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IJobStatusService _jobStatusService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<ImageProcessorWorker> _logger;
        private IConnection? _connection;
        private IChannel? _channel;

        public ImageProcessorWorker(
            IRabbitMQService rabbitMQService,
            IJobStatusService jobStatusService,
            IFileStorageService fileStorageService,
            ILogger<ImageProcessorWorker> logger)
        {
            _rabbitMQService = rabbitMQService;
            _jobStatusService = jobStatusService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _connection = await _rabbitMQService.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(
                durable: true,
                exclusive: false,
                autoDelete: false,
  arguments: new Dictionary<string, object>
                        {
                            { "x-dead-letter-exchange", "document_processing_dlx" }
                        },                cancellationToken: stoppingToken);

            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += ProcessMessageAsync;

            await _channel.BasicConsumeAsync(
                queue: "image_processing",
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessMessageAsync(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            ProcessingMessage? message = null;

            try
            {
                message = JsonSerializer.Deserialize<ProcessingMessage>(json);
                if (message == null)
                {
                    _logger.LogError("Failed to deserialize message");
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
                }

                _jobStatusService.UpdateJobStatus(message.JobId, Enums.JobStatus.Processing);

                var fileData = await _fileStorageService.GetFileAsync(message.FilePath);
                byte[] processedData = message.JobType switch
                {
                    JobType.ResizeImage => await ResizeImageAsync(fileData, message.Parameters),
                    JobType.AddWatermark => await AddWatermarkAsync(fileData, message.Parameters),
                    JobType.ConvertToJPG => await ConvertImageAsync(fileData, "jpg"),
                    JobType.ConvertToPNG => await ConvertImageAsync(fileData, "png"),
                    _ => throw new NotSupportedException($"Job type {message.JobType} not supported by image processor")
                };

                var resultFileName = GenerateResultFileName(message.JobId, message.JobType);
                var resultPath = Path.Combine(_fileStorageService.GetResultsPath(), resultFileName);

                await _fileStorageService.SaveProcessedFileAsync(resultPath, processedData);

                _jobStatusService.UpdateJobStatus(message.JobId, Enums.JobStatus.Completed, resultPath: resultPath);
                await _channel!.BasicAckAsync(ea.DeliveryTag, false);

                _logger.LogInformation($"Successfully processed job {message.JobId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing job {message?.JobId}: {ex.Message}");

                if (message != null)
                {
                    _jobStatusService.UpdateJobStatus(message.JobId, Enums.JobStatus.Failed, ex.Message);
                }

                await _channel!.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        }

        private async Task<byte[]> ResizeImageAsync(byte[] imageData, Dictionary<string, object> parameters)
        {
            var width = Convert.ToInt32(parameters.GetValueOrDefault("Width", 800));
            var height = Convert.ToInt32(parameters.GetValueOrDefault("Height", 600));

            using var image = SixLabors.ImageSharp.Image.Load(imageData);
            image.Mutate(x => x.Resize(width, height));

            using var memoryStream = new MemoryStream();
            await image.SaveAsJpegAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private async Task<byte[]> AddWatermarkAsync(byte[] imageData, Dictionary<string, object> parameters)
        {
            using var image = SixLabors.ImageSharp.Image.Load(imageData);
            using var memoryStream = new MemoryStream();
            await image.SaveAsJpegAsync(memoryStream);
            return memoryStream.ToArray();
        }
        private async Task<byte[]> ConvertImageAsync(byte[] imageData, string format)
        {
            using var image = SixLabors.ImageSharp.Image.Load(imageData);
            using var memoryStream = new MemoryStream();

            if (format.ToLower() == "jpg" || format.ToLower() == "jpeg")
            {
                await image.SaveAsJpegAsync(memoryStream, new JpegEncoder { Quality = 90 });
            }
            else if (format.ToLower() == "png")
            {
                await image.SaveAsPngAsync(memoryStream, new PngEncoder());
            }
            else
            {
                throw new NotSupportedException($"Format {format} not supported");
            }

            return memoryStream.ToArray();
        }

        private string GenerateResultFileName(string jobId, JobType jobType)
        {
            var extension = jobType switch
            {
                JobType.ResizeImage => "jpg",
                JobType.AddWatermark => "jpg",
                JobType.ConvertToJPG => "jpg",
                JobType.ConvertToPNG => "png",
                _ => "jpg"
            };
            return $"{jobId}.{extension}";
        }

    }
}

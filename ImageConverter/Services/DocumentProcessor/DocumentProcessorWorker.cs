using ImageConverter.Enums;
using ImageConverter.Models;
using ImageConverter.Services.FileStorage;
using ImageConverter.Services.JobStatus;
using ImageConverter.Services.RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using iText.IO.Image;


namespace ImageConverter.Services.DocumentProcessor
{
    public class DocumentProcessorWorker : BackgroundService
    {
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IJobStatusService _jobStatusService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<DocumentProcessorWorker> _logger;
        private IConnection? _connection;
        private IChannel? _channel;

        public DocumentProcessorWorker(
            IRabbitMQService rabbitMQService,
            IJobStatusService jobStatusService,
            IFileStorageService fileStorageService,
            ILogger<DocumentProcessorWorker> logger)
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
                      cancellationToken: stoppingToken,
                        arguments: new Dictionary<string, object>
                        {
                            { "x-dead-letter-exchange", "document_processing_dlx" }
                        });

            await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += ProcessMessageAsync;

            await _channel.BasicConsumeAsync(
            queue: "document_processing",
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
                    JobType.ConvertToPDF => await ConvertImageToPdfAsync(fileData),
                    _ => throw new NotSupportedException($"Job type {message.JobType} not supported by document processor")
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

        private async Task<byte[]> ConvertImageToPdfAsync(byte[] imageData)
        {
            using var memoryStream = new MemoryStream();
            using var pdfWriter = new iText.Kernel.Pdf.PdfWriter(memoryStream);
            using var pdfDocument = new iText.Kernel.Pdf.PdfDocument(pdfWriter);
            using var document = new iText.Layout.Document(pdfDocument);

            var imageDataObj = ImageDataFactory.Create(imageData);
            var pdfImage = new iText.Layout.Element.Image(imageDataObj);

            pdfImage.SetAutoScale(true);
            document.Add(pdfImage);

            document.Close();
            return memoryStream.ToArray();
        }

        private string GenerateResultFileName(string jobId, JobType jobType)
        {
            var extension = jobType switch
            {
                JobType.ConvertToPDF => "pdf",
                _ => "bin"
            };
            return $"{jobId}.{extension}";
        }
    }
}

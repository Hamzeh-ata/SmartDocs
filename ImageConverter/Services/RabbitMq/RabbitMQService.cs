using ImageConverter.Models;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace ImageConverter.Services.RabbitMq
{
    public class RabbitMQService : IRabbitMQService, IAsyncDisposable
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IConfiguration _configuration;
        private readonly string[] _queueNames = { "document_processing", "image_processing" };

        public RabbitMQService(IConfiguration configuration)
        {
            _configuration = configuration;

            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                Port = _configuration.GetValue("RabbitMQ:Port", 5672)
            };

            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            Task.Run(() => DeclareQueuesAsync()).GetAwaiter().GetResult();
        }

        private async Task DeclareQueuesAsync()
        {
            foreach (var queueName in _queueNames)
            {
                await _channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object>
                    {
                        { "x-dead-letter-exchange", $"{queueName}_dlx" },
                        { "x-dead-letter-routing-key", $"{queueName}_dlq" }
                    });

                await _channel.ExchangeDeclareAsync($"{queueName}_dlx", ExchangeType.Direct, durable: true);
                await _channel.QueueDeclareAsync($"{queueName}_dlq", durable: true, exclusive: false, autoDelete: false);
                await _channel.QueueBindAsync($"{queueName}_dlq", $"{queueName}_dlx", $"{queueName}_dlq");
            }
        }

        public async Task PublishMessageAsync(ProcessingMessage message, string queueName)
        {
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel.BasicPublishAsync(
                      exchange: "",
                      routingKey: queueName,
                      mandatory: false,
                      basicProperties: properties,
                      body: body);

        }
        public async ValueTask DisposeAsync()
        {
            if (_channel != null)
                await _channel.CloseAsync();

            if (_connection != null)
                await _connection.CloseAsync();
        }

        public async Task<IConnection> CreateConnectionAsync()
        {
            var factory = new ConnectionFactory
            {
                HostName = _configuration["RabbitMQ:HostName"] ?? "localhost",
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                Port = _configuration.GetValue("RabbitMQ:Port", 5672)
            };
            return await factory.CreateConnectionAsync();
        }
    }
}
using ImageConverter.Models;
using RabbitMQ.Client;

namespace ImageConverter.Services.RabbitMq
{
    public interface IRabbitMQService
    {
        Task PublishMessageAsync(ProcessingMessage message, string queueName);
        Task<IConnection> CreateConnectionAsync();
    }
}

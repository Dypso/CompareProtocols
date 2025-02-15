using System.Text.Json;
using Common.Models;
using Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Common.Services;

public class RabbitMQService : IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQService> _logger;
    private const string ExchangeName = "validations.exchange";

    public RabbitMQService(ILogger<RabbitMQService> logger, IOptions<RabbitMQSettings> settings)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = settings.Value.HostName,
            UserName = settings.Value.UserName,
            Password = settings.Value.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Configuration des exchanges et queues
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, true);
        _channel.ExchangeDeclare("validations.dlx", ExchangeType.Fanout, true);

        // Queue par zone avec dead letter
        for (int zone = 1; zone <= 10; zone++)
        {
            var queueName = $"validations.zone{zone}";
            var args = new Dictionary<string, object>
            {
                {"x-dead-letter-exchange", "validations.dlx"},
                {"x-max-length", 1000000},
                {"x-overflow", "reject-publish"},
                {"x-queue-type", "quorum"}
            };

            _channel.QueueDeclare(queueName, true, false, false, args);
            _channel.QueueBind(queueName, ExchangeName, $"validations.zone{zone}.#");
        }
    }

    public async Task PublishValidationAsync(ValidationEvent validation)
    {
        try
        {
            var zone = DetermineZone(validation.Location);
            var routingKey = $"validations.zone{zone}.{validation.EquipmentId}";
            var message = JsonSerializer.SerializeToUtf8Bytes(validation);

            var props = _channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = $"{validation.EquipmentId}_{validation.Sequence}";
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                ExchangeName,
                routingKey,
                props,
                message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to RabbitMQ");
            throw;
        }
    }

    private int DetermineZone(string location)
    {
        return Math.Abs(location.GetHashCode() % 10) + 1;
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
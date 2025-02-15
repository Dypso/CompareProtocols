using System.Text;
using System.Text.Json;
using Common.Models;
using Common.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Mqtt.Solution.Services;

public class ValidationConsumerService : BackgroundService
{
    private readonly ILogger<ValidationConsumerService> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly OracleDataService _oracleService;
    private readonly List<ValidationEvent> _batchBuffer = new();
    private readonly object _bufferLock = new();
    private readonly Timer _flushTimer;

    public ValidationConsumerService(
        ILogger<ValidationConsumerService> logger,
        IOptions<RabbitMQSettings> settings,
        OracleDataService oracleService)
    {
        _logger = logger;
        _oracleService = oracleService;

        var factory = new ConnectionFactory
        {
            HostName = settings.Value.HostName,
            UserName = settings.Value.UserName,
            Password = settings.Value.Password,
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.BasicQos(0, 1000, false); // Prefetch count

        _flushTimer = new Timer(async _ => await FlushBuffer(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consommer depuis toutes les queues de zone
        for (int zone = 1; zone <= 10; zone++)
        {
            var queueName = $"validations.zone{zone}";
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += HandleMessage;
            _channel.BasicConsume(queueName, false, consumer);
        }

        return Task.CompletedTask;
    }

    private async Task HandleMessage(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var message = Encoding.UTF8.GetString(ea.Body.Span);
            var validation = JsonSerializer.Deserialize<ValidationEvent>(message);

            if (validation != null)
            {
                lock (_bufferLock)
                {
                    _batchBuffer.Add(validation);
                    if (_batchBuffer.Count >= 1000)
                    {
                        _ = FlushBuffer();
                    }
                }
            }

            _channel.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du traitement du message");
            _channel.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task FlushBuffer()
    {
        List<ValidationEvent> batchToProcess;
        
        lock (_bufferLock)
        {
            if (!_batchBuffer.Any()) return;
            
            batchToProcess = new List<ValidationEvent>(_batchBuffer);
            _batchBuffer.Clear();
        }

        try
        {
            await _oracleService.BulkInsertValidationsAsync(batchToProcess);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du flush du buffer");
            
            // Remettre les événements dans le buffer
            lock (_bufferLock)
            {
                _batchBuffer.InsertRange(0, batchToProcess);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await FlushBuffer();
        await _flushTimer.DisposeAsync();
        _channel.Dispose();
        _connection.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
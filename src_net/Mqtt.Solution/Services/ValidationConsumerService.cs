using System.Text;
using System.Text.Json;
using Common.Models;
using Common.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Threading.Channels;

namespace Mqtt.Solution.Services;

public class ValidationConsumerService : BackgroundService
{
    private readonly ILogger<ValidationConsumerService> _logger;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly OracleDataService _oracleService;
    private readonly Channel<ValidationEvent> _processingChannel;
    private readonly SemaphoreSlim _batchProcessingSemaphore;
    private readonly Timer _flushTimer;
    private const int BatchSize = 1000;
    private const int MaxConcurrentBatches = 5;

    public ValidationConsumerService(
        ILogger<ValidationConsumerService> logger,
        IOptions<RabbitMQSettings> settings,
        OracleDataService oracleService)
    {
        _logger = logger;
        _oracleService = oracleService;
        _batchProcessingSemaphore = new SemaphoreSlim(MaxConcurrentBatches);
        _processingChannel = Channel.CreateBounded<ValidationEvent>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        var factory = new ConnectionFactory
        {
            HostName = settings.Value.HostName,
            UserName = settings.Value.UserName,
            Password = settings.Value.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(60)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.BasicQos(0, BatchSize, false);

        _flushTimer = new Timer(async _ => await FlushBufferAsync(), null, 
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        // Start background processing
        _ = ProcessMessagesAsync();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            for (int zone = 1; zone <= 10; zone++)
            {
                var queueName = $"validations.zone{zone}";
                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += HandleMessageAsync;
                _channel.BasicConsume(queueName, false, consumer);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ExecuteAsync");
            throw;
        }
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var message = Encoding.UTF8.GetString(ea.Body.Span);
            var validation = JsonSerializer.Deserialize<ValidationEvent>(message);

            if (validation != null)
            {
                await _processingChannel.Writer.WriteAsync(validation);
                _channel.BasicAck(ea.DeliveryTag, false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            _channel.BasicNack(ea.DeliveryTag, false, true);
        }
    }

    private async Task ProcessMessagesAsync()
    {
        var batch = new List<ValidationEvent>();

        await foreach (var validation in _processingChannel.Reader.ReadAllAsync())
        {
            batch.Add(validation);

            if (batch.Count >= BatchSize)
            {
                await ProcessBatchAsync(batch);
                batch = new List<ValidationEvent>();
            }
        }

        if (batch.Any())
        {
            await ProcessBatchAsync(batch);
        }
    }

    private async Task ProcessBatchAsync(List<ValidationEvent> batch)
    {
        try
        {
            await _batchProcessingSemaphore.WaitAsync();
            try
            {
                await _oracleService.BulkInsertValidationsAsync(batch);
            }
            finally
            {
                _batchProcessingSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch");
            foreach (var validation in batch)
            {
                await _processingChannel.Writer.WriteAsync(validation);
            }
        }
    }

    private async Task FlushBufferAsync()
    {
        try
        {
            if (_processingChannel.Reader.Count > 0)
            {
                var batch = new List<ValidationEvent>();
                while (_processingChannel.Reader.TryRead(out var validation))
                {
                    batch.Add(validation);
                }

                if (batch.Any())
                {
                    await ProcessBatchAsync(batch);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in flush buffer");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FlushBufferAsync();
            await _flushTimer.DisposeAsync();
            _batchProcessingSemaphore.Dispose();
            _channel.Dispose();
            _connection.Dispose();
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping service");
            throw;
        }
    }
}
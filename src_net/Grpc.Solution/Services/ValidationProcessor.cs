using System.Threading.Channels;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;

namespace Grpc.Solution.Services;

public class ValidationProcessor
{
    private readonly ILogger<ValidationProcessor> _logger;
    private readonly Channel<ValidationEvent> _channel;
    private readonly RabbitMQService _rabbitMQService;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;

    public ValidationProcessor(
        ILogger<ValidationProcessor> logger,
        RabbitMQService rabbitMQService)
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _channel = Channel.CreateBounded<ValidationEvent>(
            new BoundedChannelOptions(100000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        
        _cts = new CancellationTokenSource();
        _processingTask = ProcessValidationsAsync(_cts.Token);
    }

    public async Task ProcessValidation(ValidationEvent validation)
    {
        await _channel.Writer.WriteAsync(validation);
        MetricsRegistry.ValidationReceived.Inc();
    }

    private async Task ProcessValidationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var validation in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                var sw = Stopwatch.StartNew();
                
                try
                {
                    await _rabbitMQService.PublishValidationAsync(validation);
                    MetricsRegistry.MessagePublished.Inc();
                    MetricsRegistry.ValidationLatency.Observe(sw.Elapsed.TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing validation");
                    MetricsRegistry.ValidationErrors.Inc();
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Arrêt normal
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in validation processing");
            throw;
        }
    }
}
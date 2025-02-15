using System.Threading.Channels;
using Common.Models;
using Common.Services;
using Http2.Solution.Monitoring;
using Microsoft.Extensions.Logging;

namespace Http2.Solution.Services;

public class ValidationApiService
{
    private readonly ILogger<ValidationApiService> _logger;
    private readonly Channel<ValidationEvent> _channel;
    private readonly RabbitMQService _rabbitMQService;
    private static readonly SemaphoreSlim _throttle = new(1000);

    public ValidationApiService(
        ILogger<ValidationApiService> logger,
        RabbitMQService rabbitMQService)
    {
        _logger = logger;
        _rabbitMQService = rabbitMQService;
        _channel = Channel.CreateBounded<ValidationEvent>(new BoundedChannelOptions(100000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task EnqueueValidation(ValidationEvent validation)
    {
        try
        {
            await _throttle.WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                await _channel.Writer.WriteAsync(validation);
                MetricsRegistry.ValidationReceived.Inc();
            }
            finally
            {
                _throttle.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueueing validation");
            MetricsRegistry.ValidationErrors.Inc();
            throw;
        }
    }

    public IAsyncEnumerable<ValidationEvent> ReadValidations(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
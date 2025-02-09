using Common.Models;

namespace Http2.Solution.Services;

public class ValidationProcessingService : BackgroundService
{
    private readonly ILogger<ValidationProcessingService> _logger;
    private readonly ValidationApiService _apiService;
    private readonly RabbitMQService _rabbitMQService;

    public ValidationProcessingService(
        ILogger<ValidationProcessingService> logger,
        ValidationApiService apiService,
        RabbitMQService rabbitMQService)
    {
        _logger = logger;
        _apiService = apiService;
        _rabbitMQService = rabbitMQService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var validation in _apiService.ReadValidations(stoppingToken))
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in validation processing");
            throw;
        }
    }
}
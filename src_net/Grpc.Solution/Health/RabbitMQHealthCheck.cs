using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Common.Settings;
using RabbitMQ.Client;

namespace Grpc.Solution.Health;

public class RabbitMQHealthCheck : IHealthCheck
{
    private readonly RabbitMQSettings _settings;

    public RabbitMQHealthCheck(IOptions<RabbitMQSettings> settings)
    {
        _settings = settings.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.HostName,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ connection is healthy"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection failed", ex));
        }
    }
}
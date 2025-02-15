using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Grpc.Solution.Health;

public class GrpcHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Healthy("gRPC service is healthy"));
    }
}
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Solution.Monitoring;
using System.Diagnostics;

namespace Grpc.Solution.Interceptors;

public class GrpcMetricsInterceptor : Interceptor
{
    private readonly ILogger<GrpcMetricsInterceptor> _logger;

    public GrpcMetricsInterceptor(ILogger<GrpcMetricsInterceptor> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await continuation(request, context);
            MetricsRegistry.GrpcLatency
                .WithLabels(context.Method)
                .Observe(sw.Elapsed.TotalSeconds);
            return response;
        }
        catch (Exception)
        {
            MetricsRegistry.GrpcErrors
                .WithLabels(context.Method)
                .Inc();
            throw;
        }
    }

    public override async Task StreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
        where TRequest : class
        where TResponse : class
    {
        var sw = Stopwatch.StartNew();
        var messageCount = 0;

        try
        {
            await continuation(requestStream, responseStream, context);
            MetricsRegistry.GrpcStreamingLatency
                .WithLabels(context.Method)
                .Observe(sw.Elapsed.TotalSeconds);
            MetricsRegistry.GrpcMessagesProcessed
                .WithLabels(context.Method)
                .Inc(messageCount);
        }
        catch (Exception)
        {
            MetricsRegistry.GrpcErrors
                .WithLabels(context.Method)
                .Inc();
            throw;
        }
    }
}
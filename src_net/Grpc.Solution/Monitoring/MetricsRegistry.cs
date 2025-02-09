using Prometheus;

namespace Grpc.Solution.Monitoring;

public static class MetricsRegistry
{
    public static readonly Counter ValidationReceived = Metrics
        .CreateCounter("validations_received_total", "Total validations received via gRPC");

    public static readonly Counter ValidationErrors = Metrics
        .CreateCounter("validation_errors_total", "Total validation processing errors");

    public static readonly Counter MessagePublished = Metrics
        .CreateCounter("messages_published_total", "Total messages published to RabbitMQ");

    public static readonly Counter PublishErrors = Metrics
        .CreateCounter("publish_errors_total", "Total RabbitMQ publish errors");

    public static readonly Histogram ValidationLatency = Metrics
        .CreateHistogram("validation_latency_seconds", 
            "Validation processing latency",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
            });

    public static readonly Histogram GrpcLatency = Metrics
        .CreateHistogram("grpc_request_duration_seconds",
            "gRPC request duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
            });

    public static readonly Counter GrpcErrors = Metrics
        .CreateCounter("grpc_errors_total",
            "Total gRPC errors",
            new CounterConfiguration
            {
                LabelNames = new[] { "method" }
            });

    public static readonly Histogram GrpcStreamingLatency = Metrics
        .CreateHistogram("grpc_streaming_duration_seconds",
            "gRPC streaming session duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "method" },
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 10)
            });

    public static readonly Counter GrpcMessagesProcessed = Metrics
        .CreateCounter("grpc_messages_processed_total",
            "Total gRPC messages processed in streaming",
            new CounterConfiguration
            {
                LabelNames = new[] { "method" }
            });
}
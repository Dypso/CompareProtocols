<![CDATA[using Prometheus;

namespace Mqtt.Solution.Monitoring;

public static class MetricsRegistry
{
    public static readonly Counter ValidationReceived = Metrics
        .CreateCounter("validations_received_total", "Total validations received via MQTT");

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
}]]>
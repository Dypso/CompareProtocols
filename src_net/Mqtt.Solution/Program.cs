using Mqtt.Solution.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<MqttSettings>(
    builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

// Services
builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddHostedService<MqttValidationService>();

// Monitoring
builder.Services.AddHealthChecks()
    .AddCheck<MqttHealthCheck>("mqtt_health")
    .AddCheck<RabbitMQHealthCheck>("rabbitmq_health")
    .ForwardToPrometheus();

var app = builder.Build();

// Métriques Prometheus
app.UseMetricServer();
app.UseHttpMetrics();

app.MapHealthChecks("/health");

await app.RunAsync();
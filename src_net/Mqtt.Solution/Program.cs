using Common.Services;
using Common.Settings;
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

// HTTP Server Configuration (change port to avoid conflicts)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5002); // Changed from 5001 to 5002
});

// Monitoring
builder.Services.AddHealthChecks()
    .ForwardToPrometheus();

var app = builder.Build();

// Métriques Prometheus
app.UseMetricServer();
app.UseHttpMetrics();

app.MapHealthChecks("/health");

await app.RunAsync();
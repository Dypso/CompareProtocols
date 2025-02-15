using Common.Services;
using Common.Settings;
using Http2.Solution.Services;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

// Services
builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddSingleton<ValidationApiService>();
builder.Services.AddHostedService<ValidationProcessingService>();

// HTTP/2 Configuration
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
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
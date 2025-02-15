using Common.Services;
using Common.Settings;
using Grpc.Solution.Interceptors;
using Grpc.Solution.Services;
using Grpc.Solution.Health;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<RabbitMQSettings>(
    builder.Configuration.GetSection("RabbitMQ"));

// Services
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
    options.MaxReceiveMessageSize = 1 * 1024 * 1024; // 1MB
    options.MaxSendMessageSize = 1 * 1024 * 1024; // 1MB
    options.Interceptors.Add<GrpcMetricsInterceptor>();
});

builder.Services.AddSingleton<RabbitMQService>();
builder.Services.AddSingleton<ValidationProcessor>();

// Monitoring
builder.Services.AddHealthChecks()
    .AddCheck<GrpcHealthCheck>("grpc_health")
    .AddCheck<RabbitMQHealthCheck>("rabbitmq_health")
    .ForwardToPrometheus();

// Configure Kestrel
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

var app = builder.Build();

// Métriques Prometheus
app.UseMetricServer();

// gRPC
app.MapGrpcService<ValidationGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

await app.RunAsync();
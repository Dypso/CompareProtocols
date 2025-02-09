using Grpc.Solution.Services;
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

var app = builder.Build();

// Métriques Prometheus
app.UseMetricServer();

// gRPC
app.MapGrpcService<ValidationGrpcService>();
app.MapGrpcHealthChecksService();

await app.RunAsync();
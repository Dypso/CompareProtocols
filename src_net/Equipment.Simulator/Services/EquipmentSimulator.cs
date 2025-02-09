using Equipment.Simulator.Protocols;
using Common.Models;

namespace Equipment.Simulator.Services;

public class EquipmentSimulator
{
    private readonly int _startId;
    private readonly int _count;
    private readonly string _protocol;
    private readonly ILogger _logger;
    private readonly List<Task> _equipmentTasks = new();
    private readonly LocalCache _cache;

    public EquipmentSimulator(
        int startId, 
        int count, 
        string protocol,
        ILogger logger)
    {
        _startId = startId;
        _count = count;
        _protocol = protocol;
        _logger = logger;
        _cache = new LocalCache(
            Path.Combine(AppContext.BaseDirectory, "cache"),
            LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<LocalCache>());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        for (int i = 0; i < _count; i++)
        {
            var equipmentId = $"EQ-{_startId + i:D6}";
            var client = CreateClient(equipmentId);
            _equipmentTasks.Add(
                SimulateEquipment(equipmentId, client, cancellationToken));
        }

        await Task.WhenAll(_equipmentTasks);
    }

    private IValidationClient CreateClient(string equipmentId)
    {
        return _protocol.ToLowerInvariant() switch
        {
            "mqtt" => new MqttValidationClient(equipmentId, _cache),
            "http2" => new Http2ValidationClient(equipmentId, _cache),
            "grpc" => new GrpcValidationClient(equipmentId, _cache),
            _ => throw new ArgumentException($"Unsupported protocol: {_protocol}")
        };
    }

    private async Task SimulateEquipment(
        string equipmentId,
        IValidationClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.ConnectAsync();
            var generator = new ValidationGenerator(equipmentId);
            var sequence = 0L;

            while (!cancellationToken.IsCancellationRequested)
            {
                // Simuler un pic de charge (60 validations/sec)
                var validationsPerSecond = Random.Shared.Next(20, 60);
                var interval = TimeSpan.FromSeconds(1.0 / validationsPerSecond);

                for (int i = 0; i < validationsPerSecond; i++)
                {
                    var validation = generator.GenerateValidation(sequence++);
                    await client.SendValidationAsync(validation);
                    await Task.Delay(interval, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error in equipment simulator {EquipmentId}", 
                equipmentId);
        }
    }
}
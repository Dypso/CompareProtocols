using Common.Models;
using Grpc.Core;
using Grpc.Net.Client;

namespace Equipment.Simulator.Protocols;

public class GrpcValidationClient : IValidationClient
{
    private readonly string _equipmentId;
    private readonly LocalCache _cache;
    private readonly GrpcChannel _channel;
    private readonly ValidationService.ValidationServiceClient _client;
    private AsyncDuplexStreamingCall<ValidationRequest, ValidationResponse>? _stream;

    public GrpcValidationClient(string equipmentId, LocalCache cache)
    {
        _equipmentId = equipmentId;
        _cache = cache;
        
        var handler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            EnableMultipleHttp2Connections = true
        };

        _channel = GrpcChannel.ForAddress("https://localhost:5001",
            new GrpcChannelOptions
            {
                HttpHandler = handler,
                MaxReceiveMessageSize = 1 * 1024 * 1024, // 1MB
                MaxSendMessageSize = 1 * 1024 * 1024 // 1MB
            });

        _client = new ValidationService.ValidationServiceClient(_channel);
    }

    public async Task ConnectAsync()
    {
        var headers = new Metadata
        {
            { "equipment-id", _equipmentId }
        };

        _stream = _client.StreamValidations(headers);
        
        // Start receiving responses
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var response in _stream.ResponseStream.ReadAllAsync())
                {
                    // Process response if needed
                }
            }
            catch (Exception)
            {
                // Handle stream error
            }
        });
    }

    public async Task SendValidationAsync(ValidationEvent validation)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException(
                "Client not connected. Call ConnectAsync first.");
        }

        try
        {
            var request = new ValidationRequest
            {
                EquipmentId = validation.EquipmentId,
                CardId = validation.CardId,
                Timestamp = validation.Timestamp.ToFileTimeUtc(),
                Location = validation.Location,
                Amount = (double)validation.Amount,
                Result = (ValidationResult)validation.Result,
                Sequence = validation.Sequence,
                SessionId = validation.SessionId
            };

            await _stream.RequestStream.WriteAsync(request);
        }
        catch (Exception)
        {
            await _cache.StoreEvent(validation);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.RequestStream.CompleteAsync();
            await _stream.DisposeAsync();
        }
        await _channel.ShutdownAsync();
    }
}
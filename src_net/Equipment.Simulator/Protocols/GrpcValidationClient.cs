using System;
using System.Threading;
using System.Threading.Tasks;
using Common.Models;
using Common.Services;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcValidation;
using Microsoft.Extensions.Logging;

namespace Equipment.Simulator.Protocols
{
    public class GrpcValidationClient : IValidationClient
    {
        private readonly string _equipmentId;
        private readonly LocalCache _cache;
        private readonly GrpcChannel _channel;
        private readonly Validator.ValidatorClient _client;
        private readonly ILogger<GrpcValidationClient> _logger;
        private AsyncDuplexStreamingCall<ValidationRequest, ValidationResponse>? _stream;

        public GrpcValidationClient(string equipmentId, LocalCache cache, ILogger<GrpcValidationClient> logger)
        {
            _equipmentId = equipmentId;
            _cache = cache;
            _logger = logger;
            
            var handler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            };

            var channelOptions = new GrpcChannelOptions
            {
                HttpHandler = handler,
                MaxReceiveMessageSize = 1 * 1024 * 1024, // 1MB
                MaxSendMessageSize = 1 * 1024 * 1024 // 1MB
            };

            _channel = GrpcChannel.ForAddress("https://localhost:5001", channelOptions);
            _client = new Validator.ValidatorClient(_channel);
        }

        public async Task ConnectAsync()
        {
            try 
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
                            _logger.LogInformation("Received validation response: {Status}", response.Status);
                        }
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                    {
                        _logger.LogInformation("Stream cancelled");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing validation response stream");
                    }
                });

                _logger.LogInformation("Connected to gRPC validation service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to gRPC validation service");
                throw;
            }
        }

        public async Task SendValidationAsync(ValidationEvent validation)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("Client not connected. Call ConnectAsync first.");
            }

            try
            {
                var request = new ValidationRequest
                {
                    EquipmentId = validation.EquipmentId,
                    TokenId = validation.TokenId,
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(validation.Timestamp.ToUniversalTime()),
                    Location = validation.Location,
                    Amount = (double)validation.Amount,
                    Type = validation.Type.ToString(),
                    Status = validation.Status.ToString(),
                    Sequence = validation.Sequence,
                    SessionId = validation.SessionId
                };

                await _stream.RequestStream.WriteAsync(request);
                _logger.LogDebug("Sent validation request for token {TokenId}", validation.TokenId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending validation request for token {TokenId}", validation.TokenId);
                await _cache.StoreEvent(validation);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_stream != null)
                {
                    await _stream.RequestStream.CompleteAsync();
                    await _stream.DisposeAsync();
                }
                await _channel.ShutdownAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing gRPC client");
                throw;
            }
        }
    }
}
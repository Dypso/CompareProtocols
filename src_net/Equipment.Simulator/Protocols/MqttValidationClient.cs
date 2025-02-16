using System.Text.Json;
using Common.Models;
using Common.Services;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace Equipment.Simulator.Protocols;

public class MqttValidationClient : IValidationClient
{
    private readonly string _equipmentId;
    private readonly LocalCache _cache;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly ILogger<MqttValidationClient> _logger;

    public MqttValidationClient(string equipmentId, LocalCache cache, ILogger<MqttValidationClient> logger)
    {
        _equipmentId = equipmentId;
        _cache = cache;
        _logger = logger;
        
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        
        var tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            CertificateValidationHandler = _ => true // Pour le développement uniquement
        };
        
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 8883)
            .WithTlsOptions(tlsOptions)
            .WithClientId(_equipmentId)
            .WithCleanSession(false)
            .WithCredentials("admin", "admin123!")
            .Build();
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _client.ConnectAsync(_options);
            _logger.LogInformation("Connected to MQTT broker");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MQTT broker");
            throw new InvalidOperationException(
                "Failed to connect to MQTT broker", ex);
        }
    }

    public async Task SendValidationAsync(ValidationEvent validation)
    {
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic($"validations/{_equipmentId}")
                .WithPayload(JsonSerializer.SerializeToUtf8Bytes(validation))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                .WithRetainFlag(false)
                .Build();

            await _client.PublishAsync(message);
            _logger.LogDebug("Sent validation for token {TokenId}", validation.TokenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending validation for token {TokenId}", validation.TokenId);
            await _cache.StoreEvent(validation);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync();
            }
            await Task.Run(() => _client.Dispose());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing MQTT client");
        }
    }
}
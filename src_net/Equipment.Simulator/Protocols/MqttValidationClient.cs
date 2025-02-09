using Common.Models;
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

    public MqttValidationClient(string equipmentId, LocalCache cache)
    {
        _equipmentId = equipmentId;
        _cache = cache;
        
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer("localhost", 8883)
            .WithTls()
            .WithClientId(_equipmentId)
            .WithCleanSession(false)
            .WithCredentials("validation_service", "secret")
            .Build();
    }

    public async Task ConnectAsync()
    {
        try
        {
            await _client.ConnectAsync(_options);
        }
        catch (Exception ex)
        {
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
        }
        catch (Exception)
        {
            await _cache.StoreEvent(validation);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client.IsConnected)
        {
            await _client.DisconnectAsync();
        }
        await _client.DisposeAsync();
    }
}
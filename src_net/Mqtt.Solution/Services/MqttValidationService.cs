using System.Text.Json;
using Common.Models;
using Common.Services;
using Common.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;

namespace Mqtt.Solution.Services;

public class MqttValidationService : IHostedService
{
    private readonly ILogger<MqttValidationService> _logger;
    private readonly IManagedMqttClient _mqttClient;
    private readonly MqttSettings _settings;
    private readonly RabbitMQService _rabbitMQService;

    public MqttValidationService(
        ILogger<MqttValidationService> logger,
        IOptions<MqttSettings> settings,
        RabbitMQService rabbitMQService)
    {
        _logger = logger;
        _settings = settings.Value;
        _rabbitMQService = rabbitMQService;

        var factory = new MqttFactory();
        _mqttClient = factory.CreateManagedMqttClient();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(new MqttClientOptionsBuilder()
                .WithTcpServer(_settings.BrokerHost, _settings.BrokerPort)
                .WithTls(o => {
                    o.IgnoreCertificateChainErrors = true;
                    o.IgnoreCertificateRevocationErrors = true;
                    o.AllowUntrustedCertificates = true;
                })
                .WithCleanSession(false)
                .WithClientId($"validation_service_{Guid.NewGuid()}")
                .WithCredentials(_settings.Username, _settings.Password)
                .Build())
            .Build();

        _mqttClient.ApplicationMessageReceivedAsync += HandleValidationMessageAsync;

        await _mqttClient.StartAsync(options);
        await _mqttClient.SubscribeAsync("validations/#", MqttQualityOfServiceLevel.ExactlyOnce);
        
        _logger.LogInformation("MQTT Service started successfully");
    }

    private async Task HandleValidationMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = System.Text.Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var validation = JsonSerializer.Deserialize<ValidationEvent>(payload);

            if (validation != null)
            {
                await _rabbitMQService.PublishValidationAsync(validation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MQTT message");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttClient.StopAsync();
        _logger.LogInformation("MQTT Service stopped");
    }
}
using System.Security.Authentication;
using System.Text;
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
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace Mqtt.Solution.Services
{
    public class MqttValidationService : IHostedService
    {
        private readonly ILogger<MqttValidationService> _logger;
        private readonly IManagedMqttClient _mqttClient;
        private readonly MqttSettings _settings;
        private readonly RabbitMQService _rabbitMQService;
        private readonly SemaphoreSlim _messageProcessingSemaphore;
        private const int MaxConcurrentProcessing = 100;

        public MqttValidationService(
            ILogger<MqttValidationService> logger,
            IOptions<MqttSettings> settings,
            RabbitMQService rabbitMQService)
        {
            _logger = logger;
            _settings = settings.Value;
            _rabbitMQService = rabbitMQService;
            _messageProcessingSemaphore = new SemaphoreSlim(MaxConcurrentProcessing);

            var factory = new MqttFactory();
            _mqttClient = factory.CreateManagedMqttClient();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var tlsOptions = new MqttClientTlsOptions
                {
                    UseTls = true,
                    SslProtocol = SslProtocols.Tls12,
                    AllowUntrustedCertificates = true, // Pour le développement uniquement
                    CertificateValidationHandler = _ => true // Pour le développement uniquement
                };

                var clientOptions = new MqttClientOptions
                {
                    ClientId = $"validation_service_{Guid.NewGuid()}",
                    ChannelOptions = new MqttClientTcpOptions
                    {
                        Server = _settings.BrokerHost,
                        Port = _settings.BrokerPort,
                        TlsOptions = tlsOptions
                    },
                    Credentials = new MqttClientCredentials(_settings.Username, Encoding.UTF8.GetBytes(_settings.Password)),
                    KeepAlivePeriod = TimeSpan.FromSeconds(60),
                    CleanSession = false
                };

                var options = new ManagedMqttClientOptions
                {
                    ClientOptions = clientOptions,
                    AutoReconnectDelay = TimeSpan.FromSeconds(5)
                };

                _mqttClient.ApplicationMessageReceivedAsync += HandleValidationMessageAsync;
                _mqttClient.DisconnectedAsync += HandleDisconnectedAsync;

                await _mqttClient.StartAsync(options);

                var topicFilter = new MqttTopicFilterBuilder()
                    .WithTopic("validations/#")
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.ExactlyOnce)
                    .Build();

                await _mqttClient.SubscribeAsync(new[] { topicFilter });

                _logger.LogInformation("MQTT Service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start MQTT service");
                throw;
            }
        }

        private async Task HandleValidationMessageAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            await _messageProcessingSemaphore.WaitAsync();

            try
            {
                var payload = e.ApplicationMessage.PayloadSegment.Array != null
                    ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment.Array, e.ApplicationMessage.PayloadSegment.Offset, e.ApplicationMessage.PayloadSegment.Count)
                    : string.Empty;

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
            finally
            {
                _messageProcessingSemaphore.Release();
            }
        }

        private Task HandleDisconnectedAsync(MqttClientDisconnectedEventArgs e)
        {
            _logger.LogWarning(e.Exception, "MQTT client disconnected. Reason: {Reason}", e.Reason);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _mqttClient.StopAsync();
                _messageProcessingSemaphore.Dispose();
                _logger.LogInformation("MQTT Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping MQTT service");
                throw;
            }
        }
    }
}
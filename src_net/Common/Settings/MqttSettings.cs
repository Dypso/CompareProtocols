using System;

namespace Common.Settings;

public class MqttSettings
{
    public string BrokerHost { get; set; } = string.Empty;
    public int BrokerPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseTls { get; set; }
}
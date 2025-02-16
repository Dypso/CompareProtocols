namespace Common.Settings;

public class RabbitMQSettings
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "admin";
    public string Password { get; set; } = "admin123!";
    public string VirtualHost { get; set; } = "/";
    public int Port { get; set; } = 5672;
    public bool UseTls { get; set; } = false;
}
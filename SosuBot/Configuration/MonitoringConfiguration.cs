namespace SosuBot.Configuration;

public sealed class MonitoringConfiguration
{
    public bool Enabled { get; set; } = true;
    public int Port { get; set; } = 9091;
}

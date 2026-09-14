namespace AetherVPN.Models;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Disconnecting,
    Error
}

public class ConnectionStatus
{
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;
    public string StatusText { get; set; } = "Disconnected";
    public string? ExitIp { get; set; }
    public DateTime? ConnectedAt { get; set; }
    public string? DeviceId { get; set; }
    public string? Ipv4 { get; set; }
    public string? Ipv6 { get; set; }
    public string? EdgeEndpoint { get; set; }

    public TimeSpan? Duration =>
        ConnectedAt.HasValue ? DateTime.Now - ConnectedAt.Value : null;

    public string DurationText
    {
        get
        {
            if (!Duration.HasValue) return "--:--:--";
            var d = Duration.Value;
            return d.TotalHours >= 1
                ? $"{(int)d.TotalHours:D2}:{d.Minutes:D2}:{d.Seconds:D2}"
                : $"{d.Minutes:D2}:{d.Seconds:D2}";
        }
    }
}

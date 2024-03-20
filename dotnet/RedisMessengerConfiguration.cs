namespace ESuitePro.RedisMessenger;

public record RedisMessengerConfiguration
{
    public string? ClientName { get; set; }
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
    public string? ChannelPrefix { get; set; }
    public int ConnectRetry { get; set; } = 5;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public string? User { get; set; }
    public string? Password { get; set; }
}

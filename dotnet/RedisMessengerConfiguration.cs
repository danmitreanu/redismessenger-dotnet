using Microsoft.Extensions.DependencyInjection;

namespace RedisMessenger;

public sealed class RedisMessengerConfiguration
{
    public string? RedisConfiguration { get; set; }
    public string? ClientName { get; set; }
    public TimeSpan ReconnectInterval { get; set; } = TimeSpan.FromSeconds(5);
    public string? ChannelPrefix { get; set; }
    public int ConnectRetry { get; set; } = 5;
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public string? User { get; set; }
    public string? Password { get; set; }

    public IMessageHandlerCollection MessageHandlers { get; }

    internal RedisMessengerConfiguration(IServiceCollection serviceCollection)
    {
        MessageHandlers = new MessageHandlerCollection(serviceCollection);
    }

    public RedisMessengerConfiguration AddMessageHandler<THandler>(string channelName) where THandler : MessageHandler
    {
        MessageHandlers.RegisterHandler<THandler>(channelName);
        return this;
    }
}

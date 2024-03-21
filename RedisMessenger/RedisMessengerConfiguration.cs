using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace RedisMessenger;

public sealed class RedisMessengerConfiguration
{
    public string? RedisConfiguration { get; set; }
    public string? ClientName { get; set; }
    public string? ChannelPrefix { get; set; }

    public Action<ConfigurationOptions>? RedisConfigure { get; set; }

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

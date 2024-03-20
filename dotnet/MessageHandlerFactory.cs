using Microsoft.Extensions.DependencyInjection;

namespace RedisMessenger;

internal sealed class MessageHandlerFactory(IServiceProvider serviceProvider, IEnumerable<string> registeredChannels)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    public IEnumerable<string> RegisteredChannels { get; } = registeredChannels;

    public MessageHandler? GetHandler(string channelName)
    {
        var scope = _serviceProvider.CreateAsyncScope();
        return scope.ServiceProvider.GetKeyedService<MessageHandler>(channelName);
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace RedisMessenger;

internal sealed class MessageHandlerCollection(IServiceCollection services) : IMessageHandlerCollection
{
    private readonly IServiceCollection _services = services;
    private readonly List<string> _channels = [];

    public IEnumerable<string> RegisteredChannels => _channels;

    public void RegisterHandler<THandler>(string channelName) where THandler : MessageHandler
    {
        if (_channels.Contains(channelName))
            throw new RedisMessengerException($"A message handler has already been registered for channel {channelName}");

        _channels.Add(channelName);
        _services.AddKeyedTransient<MessageHandler, THandler>(channelName);
    }

    public MessageHandlerFactory BuildFactory(IServiceProvider serviceProvider)
    {
        return new MessageHandlerFactory(serviceProvider, _channels);
    }
}

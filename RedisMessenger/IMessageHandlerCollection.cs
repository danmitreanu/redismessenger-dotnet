namespace RedisMessenger;

public interface IMessageHandlerCollection
{
    void RegisterHandler<THandler>(string channelName) where THandler : MessageHandler;
}

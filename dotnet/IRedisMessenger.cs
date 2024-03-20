namespace RedisMessenger;

public interface IRedisMessenger
{
    IMessageChannel<TReq, TRes> GetMessageChannel<TReq, TRes>(string channelName, TimeSpan? defaultTimeout = null);
}

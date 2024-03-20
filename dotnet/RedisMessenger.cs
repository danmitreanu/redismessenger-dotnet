using StackExchange.Redis;

namespace ESuitePro.RedisMessenger;

public sealed class RedisMessenger : IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly string _clientName;
    private readonly string? _channelPrefix = null;

    public RedisMessenger(string redisConfigString, Action<RedisMessengerConfiguration> redisConfigure)
    {
        RedisMessengerConfiguration config = new();
        redisConfigure(config);

        _redis = ConnectionMultiplexer.Connect(redisConfigString, LibConfigToRedisConfig(config));
        _channelPrefix = config.ChannelPrefix is not null ? $"{config.ChannelPrefix}:" : null;
        _clientName = config.ClientName ?? Guid.NewGuid().ToString();
    }

    public IMessageChannel<TReq, TRes> GetMessageChannel<TReq, TRes>(string channelName, TimeSpan? defaultTimeout = null)
    {
        ISubscriber pubsub = _redis.GetSubscriber();
        string mainChannel = $"{_channelPrefix}{channelName}";

        return new MessageChannel<TReq, TRes>(pubsub, mainChannel, _clientName);
    }

    public static string CreateRequestChannelName(string channelName, string clientName) => $"{channelName}:req-{clientName}";
    public static string CreateResponseChannelName(string channelName, string clientName) => $"{channelName}:res-{clientName}";
    public static string CreateHandlerRequestChannelPattern(string channelName) => $"{channelName}:req-*";

    private static Action<ConfigurationOptions> LibConfigToRedisConfig(RedisMessengerConfiguration libConfig)
    {
        return configure =>
        {
            configure.AbortOnConnectFail = false;
            configure.ReconnectRetryPolicy = new LinearRetry(libConfig.ReconnectInterval.Milliseconds);
            configure.ConnectRetry = int.Max(0, libConfig.ConnectRetry);
            configure.ConnectTimeout = libConfig.ConnectTimeout.Milliseconds;
            configure.User = libConfig.User;
            configure.Password  = libConfig.Password;
        };
    }

    private bool _disposed = false;
    public void Dispose()
    {
        if (_disposed)
            return;

        _redis.Dispose();
        _disposed = true;
    }
}

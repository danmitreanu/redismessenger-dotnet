using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace RedisMessenger;

public sealed class RedisMessenger : IRedisMessenger, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly string _clientName;
    private readonly string? _channelPrefix = null;
    private readonly MessageHandlerFactory _handlerFactory;

    internal RedisMessenger(RedisMessengerConfiguration config, IServiceProvider serviceProvider)
    {
        if (config.RedisConfiguration is null)
            throw new RedisMessengerException($"{nameof(RedisMessengerConfiguration)} is missing required parameter {nameof(RedisMessengerConfiguration.RedisConfiguration)}");

        _redis = ConnectionMultiplexer.Connect(config.RedisConfiguration, LibConfigToRedisConfig(config));
        _channelPrefix = config.ChannelPrefix is not null ? $"{config.ChannelPrefix}_" : null;
        _clientName = config.ClientName ?? Guid.NewGuid().ToString();

        var handlers = config.MessageHandlers as MessageHandlerCollection;
        _handlerFactory = handlers!.BuildFactory(serviceProvider);

        BindHandlers();
    }

    public static IRedisMessenger Create(Action<RedisMessengerConfiguration> configure)
    {
        ServiceCollection services = new();
        RedisMessengerConfiguration config = new(services);
        configure(config);

        services.AddSingleton<IRedisMessenger>(serviceProvider => new RedisMessenger(config, serviceProvider));
        var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IRedisMessenger>();
    }

    public IMessageChannel<TReq, TRes> GetMessageChannel<TReq, TRes>(string channelName, TimeSpan? defaultTimeout = null)
    {
        ISubscriber pubsub = _redis.GetSubscriber();
        return new MessageChannel<TReq, TRes>(pubsub, _channelPrefix, channelName, _clientName, defaultTimeout);
    }

    private void BindHandlers()
    {
        ISubscriber handlerSub = _redis.GetSubscriber();
        foreach (string channelName in _handlerFactory.RegisteredChannels)
        {
            string channelPattern = CreateHandlerRequestChannelPattern(_channelPrefix, channelName);
            RedisChannel incomingChannel = new(channelPattern, RedisChannel.PatternMode.Pattern);

            handlerSub.Subscribe(incomingChannel, (_, requestPayload) =>
            {
                Task.Run(() =>
                {
                    var handler = _handlerFactory.GetHandler(channelName);
                    // todo: handler requestPayload
                });
            });
        }
    }

    public static string CreateRequestChannelName(string? channelPrefix, string channelName, string clientName)
        => $"{channelName}:req-{clientName}";
    public static string CreateResponseChannelName(string? channelPrefix, string channelName, string clientName)
        => $"{channelName}:res-{clientName}";
    public static string CreateHandlerRequestChannelPattern(string? channelPrefix, string channelName)
        => $"{channelName}:req-*";

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

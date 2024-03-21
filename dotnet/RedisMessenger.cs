using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace RedisMessenger;

public sealed class RedisMessenger : IRedisMessenger, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly string _clientName;
    private readonly string? _channelPrefix = null;
    private readonly MessageHandlerFactory _handlerFactory;
    private readonly ILogger? _logger;

    internal static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal RedisMessenger(RedisMessengerConfiguration config, IServiceProvider serviceProvider)
    {
        if (config.RedisConfiguration is null)
            throw new RedisMessengerException($"{nameof(RedisMessengerConfiguration)} is missing required parameter {nameof(RedisMessengerConfiguration.RedisConfiguration)}");

        var redisConfiguration = ConfigurationOptions.Parse(config.RedisConfiguration);
        config.RedisConfigure?.Invoke(redisConfiguration);

        _redis = ConnectionMultiplexer.Connect(redisConfiguration);

        _channelPrefix = config.ChannelPrefix is not null ? $"{config.ChannelPrefix}_" : null;
        _clientName = config.ClientName ?? Guid.NewGuid().ToString();

        var handlers = config.MessageHandlers as MessageHandlerCollection;
        _handlerFactory = handlers!.BuildFactory(serviceProvider);

        _logger = serviceProvider.GetService<ILogger<IRedisMessenger>>();

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

            handlerSub.Subscribe(incomingChannel, (requestChannel, requestPayload) =>
            {
                Task.Run(async () =>
                {
                    var handler = _handlerFactory.GetHandler(channelName);
                    if (handler is null)
                    {
                        _logger?.LogError("Failed to get message handler for channel {channelName}", channelName);
                        return;
                    }

                    await handler.HandleMessageAsync(requestChannel, handlerSub, _logger, _channelPrefix, channelName, requestPayload);
                });
            });
        }
    }

    public static string CreateRequestChannelName(string? channelPrefix, string channelName, string clientName)
        => $"{channelPrefix}{channelName}:req-{clientName}";
    public static string CreateResponseChannelName(string? channelPrefix, string channelName, string clientName)
        => $"{channelPrefix}{channelName}:res-{clientName}";
    public static string CreateHandlerRequestChannelPattern(string? channelPrefix, string channelName)
        => $"{channelPrefix}{channelName}:req-*";

    private bool _disposed = false;
    public void Dispose()
    {
        if (_disposed)
            return;

        _redis.Dispose();
        _disposed = true;
    }
}

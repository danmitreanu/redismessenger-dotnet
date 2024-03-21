using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Toolkit.HighPerformance;
using StackExchange.Redis;

using RedisMessenger.Models;
namespace RedisMessenger;

internal class MessageChannel<TReq, TRes> : IMessageChannel<TReq, TRes>
{
    private readonly string _clientName;
    private readonly ISubscriber _pubsub;
    private readonly RedisChannel _reqChannel;
    private readonly RedisChannel _resChannel;

    private readonly TimeSpan _defaultTimeout;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<TRes?>> _resTasks = [];
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MessageChannel(ISubscriber pubsub, string? channelPrefix, string channelName, string clientName, TimeSpan? defaultTimeout)
    {
        _clientName = clientName;
        _pubsub = pubsub;
        string reqChannel = RedisMessenger.CreateRequestChannelName(channelPrefix, channelName, clientName);
        string resChannel = RedisMessenger.CreateResponseChannelName(channelPrefix, channelName, clientName);

        _reqChannel = new RedisChannel(reqChannel, RedisChannel.PatternMode.Literal);
        _resChannel = new RedisChannel(resChannel, RedisChannel.PatternMode.Literal);

        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task SendAsync(TReq requestPayload)
    {
        string reqId = Guid.NewGuid().ToString();
        RequestModel req = new(reqId, _clientName, requestPayload);

        using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, req, s_jsonOpts);

        var redisValue = RedisValue.CreateFrom(stream);

        await _pubsub.PublishAsync(_reqChannel, redisValue, CommandFlags.FireAndForget);
    }

    public async Task<TRes?> QueryAsync(TReq requestPayload, CancellationToken cancellationToken)
    {
        await EnsureSubscribedAsync();

        string reqId = Guid.NewGuid().ToString();
        RequestModel req = new(reqId, _clientName, requestPayload);

        using MemoryStream stream = new();
        await JsonSerializer.SerializeAsync(stream, req, s_jsonOpts, CancellationToken.None);

        var redisValue = RedisValue.CreateFrom(stream);
        await _pubsub.PublishAsync(_reqChannel, redisValue);

        TaskCompletionSource<TRes?> tcs = new(TaskCreationOptions.AttachedToParent);
        _resTasks.TryAdd(reqId, tcs);
        void cancelTask(CancellationToken ct)
        {
            tcs.TrySetCanceled(ct);
        }

        CancellationTokenSource? cts = null;
        CancellationTokenRegistration ctr;

        if (cancellationToken != default)
        {
            ctr = cancellationToken.Register(() => cancelTask(cancellationToken));
        }
        else
        {
            cts = new CancellationTokenSource((int)_defaultTimeout.TotalMilliseconds);
            ctr = cts.Token.Register(() => cancelTask(cts.Token));
        }

        TRes? res = await tcs.Task;

        await ctr.DisposeAsync();
        cts?.Dispose();

        return res;
    }

    private bool _subscribed = false;
    private async Task EnsureSubscribedAsync()
    {
        if (_subscribed)
            return;

        await _pubsub.SubscribeAsync(
            _resChannel,
            (channel, redisValue) => Task.Run(async () => await HandleResponseAsync(channel, redisValue)),
            CommandFlags.None
        );

        _subscribed = true;
    }

    private async Task HandleResponseAsync(RedisChannel _, RedisValue value)
    {
        if (!value.HasValue || value.IsNullOrEmpty)
            return;

        Stream? GetPayloadStream()
        {
            object? payload = value.Box();
            if (payload is string strPayload)
            {
                var strMem = strPayload.AsMemory().AsBytes();
                return strMem.AsStream();
            }
            else if (payload is byte[] bytePayload)
            {
                return new MemoryStream(bytePayload);
            }

            return null;
        }

        using Stream? payloadStream = GetPayloadStream();

        if (payloadStream is null)
            return;

        var metaResponse = (await JsonSerializer.DeserializeAsync<ResponseModel<TRes>>(payloadStream, s_jsonOpts))!;

        if (!_resTasks.TryRemove(metaResponse.ReplyTo, out var tcs))
            return;

        if (!metaResponse.Success)
        {
            RedisMessengerResponseException ex = new($"The redis message has failed in the message handler: {metaResponse.ErrorMessage}");
            tcs.TrySetException(ex);
            return;
        }

        tcs.TrySetResult(metaResponse.Payload);
    }
}

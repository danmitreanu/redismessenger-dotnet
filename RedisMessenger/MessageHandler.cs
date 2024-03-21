using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.HighPerformance;
using RedisMessenger.Models;
using StackExchange.Redis;

namespace RedisMessenger;

public abstract class MessageHandler
{
    protected internal abstract Task<object?> HandleMessageAsync(JsonElement? payload);

    internal async Task HandleMessageAsync(RedisChannel channel, ISubscriber pub, ILogger? logger, string? channelPrefix, string channelName, RedisValue redisValue)
    {
        Stream? GetRequestPayloadStream()
        {
            object? requestPayload = redisValue.Box();
            if (requestPayload is byte[] bytePayload)
            {
                return new MemoryStream(bytePayload);
            }
            else if (requestPayload is Memory<byte> memoryPayload)
            {
                return memoryPayload.AsStream();
            }
            else if (requestPayload is ReadOnlyMemory<byte> readonlyPayload)
            {
                return readonlyPayload.AsStream();
            }
            else if (requestPayload is string strPayload)
            {
                var strMem = strPayload.AsMemory().AsBytes();
                return strMem.AsStream();
            }

            return null;
        }

        using Stream? reqStream = GetRequestPayloadStream();
        if (reqStream is null)
        {
            logger?.LogError("Invalid request payload");
            return;
        }

        async Task<JsonDocument?> GetJsonFromStream()
        {
            try
            {
                return await JsonSerializer.DeserializeAsync<JsonDocument>(reqStream, RedisMessenger.s_jsonOpts);
            }
            catch
            {
                return null;
            }
        }

        using JsonDocument? reqJson = await GetJsonFromStream();
        var serializableRequest = SerializableRequestModel.FromJson(reqJson);
        if (serializableRequest is null)
        {
            // Would respond, but this is pretty bad, we don't know who to respond to :(
            logger?.LogError("Tried to deserialize request data but failed. Redis channel: {channel}", channel);
            return;
        }

        ResponseModel<object?> response;
        try
        {
            object? rawResponse = await HandleMessageAsync(serializableRequest.Payload);
            response = new ResponseModel<object?>(serializableRequest.RequestId, true, null, rawResponse);
        }
        catch (Exception ex)
        {
            response = new ResponseModel<object?>(serializableRequest.RequestId, false, ex.Message, null);
        }

        string responseChannelName = RedisMessenger.CreateResponseChannelName(channelPrefix, channelName, serializableRequest.ClientName);
        RedisChannel responseChannel = new(responseChannelName, RedisChannel.PatternMode.Literal);

        using MemoryStream resStream = new();
        await JsonSerializer.SerializeAsync(resStream, response, RedisMessenger.s_jsonOpts);

        var resValue = RedisValue.CreateFrom(resStream);

        await pub.PublishAsync(responseChannel, resValue);
    }
}

public abstract class MessageHandler<TReq, TRes> : MessageHandler
{
    protected internal override async Task<object?> HandleMessageAsync(JsonElement? payload)
    {
        if (payload is null)
            return await HandleMessageAsync(null);

        var req = payload.Value.Deserialize<TReq>(RedisMessenger.s_jsonOpts);
        return await HandleMessageAsync(req);
    }

    protected abstract Task<TRes> HandleMessageAsync(TReq? payload);
}

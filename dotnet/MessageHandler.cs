using System.Text.Json;

namespace RedisMessenger;

public abstract class MessageHandler
{
    public abstract Task<object?> HandleMessageAsync(JsonDocument? payload);
}

public abstract class MessageHandler<TReq, TRes> : MessageHandler
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public sealed override async Task<object?> HandleMessageAsync(JsonDocument? payload)
    {
        TReq? req = default;
        if (payload is null)
            return await HandleMessageAsync(req);

        var reqPayload = payload.Deserialize<TReq>();
        return await HandleMessageAsync(reqPayload);
    }

    public abstract Task<TRes> HandleMessageAsync(TReq? payload);
}

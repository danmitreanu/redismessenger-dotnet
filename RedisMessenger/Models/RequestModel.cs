using System.Text.Json;
using StackExchange.Redis;

namespace RedisMessenger.Models;

internal record RequestModel
{
    internal RequestModel() {}

    internal RequestModel(string reqId, string clientName, object? payload)
    {
        RequestId = reqId;
        ClientName = clientName;
        Payload = payload;
    }

    public string? RequestId { get; set; }
    public string? ClientName { get; set; }
    public object? Payload { get; set; }
}

internal record SerializableRequestModel(
    string RequestId,
    string ClientName,
    JsonElement Payload
)
{
    public static SerializableRequestModel? FromJson(JsonDocument? doc)
    {
        if (doc is null)
            return null;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        static string camelCase(string str) => JsonNamingPolicy.CamelCase.ConvertName(str);

        string? requestId, clientName;
        if (!root.TryGetProperty(camelCase(nameof(RequestId)), out var reqIdProp) ||
            (requestId = reqIdProp.GetString()) is null)
            return null;

        if (!root.TryGetProperty(camelCase(nameof(ClientName)), out var clientNameProp) ||
            (clientName = clientNameProp.GetString()) is null)
            return null;

        if (!root.TryGetProperty(camelCase(nameof(Payload)), out var payloadProp))
            return null;

        return new SerializableRequestModel(requestId, clientName, payloadProp);
    }
}

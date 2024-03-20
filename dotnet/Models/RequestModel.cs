namespace RedisMessenger.Models;

internal record RequestModel(
    string RequestId,
    string ClientName,
    object? Payload
);

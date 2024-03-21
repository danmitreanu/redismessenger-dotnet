namespace RedisMessenger.Models;

internal record ResponseModel<T>(
    string ReplyTo,
    bool Success,
    string? ErrorMessage,
    T Payload
);

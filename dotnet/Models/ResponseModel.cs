namespace RedisMessenger.Models;

internal record ResponseModel<T>(
    string ReplyTo,
    T Payload
);

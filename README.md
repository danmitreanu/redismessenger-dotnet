# RedisMessenger .NET Client

The RedisMessenger .NET Client allows you to use Redis as a message broker to communicate between applications that implement the RedisMessenger protocol.

## Install

You can install this library via [NuGet](https://www.nuget.org/packages/RedisMessenger/):

```sh
dotnet add package RedisMessenger
```

## Usage

RedisMessenger can be used either on its own or as an injectable service.

### Standalone

If you don't use a hosted application or dependency injection container:

```csharp
IRedisMessenger messenger = RedisMessenger.Create(configure =>
{
    configure.RedisConfiguration = "localhost";
    configure.ClientName = "my-messaging-client";
    configure.ChannelPrefix = "cool-zone";

    configure.RedisConfigure = redisOpts =>
    {
        redisOpts.AbortOnConnectFail = false;
    };

    configure.AddMessageHandler<MyMessageHandler>("my-message-channel");
});
```

### DI container

If you want it to be injectable:

```csharp
builder.Services.AddRedisMessenger(configure => ConfigureTheMessengerLikeAbove(configure));
```

### Messages

Woooo, now let's send some messages or something:

```csharp
var channel = messenger.GetMessageChannel<MyMessageRequest, MyMessageResponse>("my-message-channel");
await channel.SendAsync(new MyMessageRequest()); // Fire and forget, just like walking away from an explosion

try
{
    MyMessageResponse? res = await channel.QueryAsync(new MyMessageRequest());
    Console.WriteLine($"Knock knock, you got a message {res}");
}
catch (RedisMessengerResponseException ex)
{
    Console.WriteLine($"Woopsies, something failed on the handler side :( {ex.Message}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Probably worked on your machine but this is the cloud");
}
```

## Message handlers

In the .NET library, you can have both typed and raw message handlers:

```csharp
class MyRawMessageHandler : RedisMessenger.MessageHandler
{
    protected override async Task<object?> HandleMessageAsync(System.Text.Json.JsonElement? payload)
    {
        // Exceptions will be reported to the message sender.
        return await DoSomethingWithPayloadAsync(payload);
    }
}

class MyMessageHandler : RedisMessenger.MessageHandler<RequestType, ResponseType>
{
    private readonly ILogger _logger;
    public MyMessageHandler(ILogger<MyMessageHandler> logger) => _logger = logger;

    protected override async Task<ResponseType> HandleMessageAsync(RequestType? payload)
    {
        // JSON deserialization errors and exceptions will be reported to the message sender.
        return await DoSomethingWithPayloadAsync(payload);
    }
}
```

You can only register one message handler per channel.

## Dependency injection

As you can see, other services can be injected into your message handlers. Each message has its own scope just like ASP.NET Core HTTP requests.

## Redis configuration

This library is built upon [`StackExchange.Redis`](https://www.nuget.org/packages/StackExchange.Redis/) and allows you to configure the Redis connection as you wish via `RedisMessengerConfiguration.RedisConfigure` and `RedisMessengerConfiguration.RedisConfiguration`.

## License

MIT. Have fun.

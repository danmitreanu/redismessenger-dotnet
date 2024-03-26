using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RedisMessenger;

ServiceCollection services = new();

services.AddLogging(configure =>
{
    configure.AddConsole();
});

services.AddRedisMessenger(configure =>
{
    configure.RedisConfiguration = "localhost";
    configure.ClientName = "test-client";
    configure.ChannelPrefix = "test";

    configure.AddMessageHandler<TestHandler>("dotnet-test-channel");
});

var provider = services.BuildServiceProvider();

var messenger = provider.GetRequiredService<IRedisMessenger>();

var channel = messenger.GetMessageChannel<TestRequest, TestResponse>("node-test-channel");

Stopwatch stopwatch = new();
string? msg = Console.ReadLine();
do
{
    if (string.IsNullOrWhiteSpace(msg))
        continue;

    TestRequest req = new() { Message = msg };

    TestResponse? res = null;
    try
    {
        stopwatch.Start();
        res = await channel.QueryAsync(req);
        stopwatch.Stop();

        Console.WriteLine($"Query took {stopwatch.ElapsedMilliseconds} ms");
    }
    catch
    {
        Console.WriteLine("exception");
    }
    finally
    {
        stopwatch.Reset();
    }

    Console.WriteLine(res?.Message ?? "Oops, bug");
    msg = Console.ReadLine();
}
while (msg != "exit");

record TestRequest
{
    public string? Message { get; set; }
}

record TestResponse
{
    public string? Message { get; set; }
}

class TestHandler : MessageHandler<TestRequest, TestResponse>
{
    protected override Task<TestResponse> HandleMessageAsync(TestRequest? payload)
    {
        Console.WriteLine("Received {0}", payload?.Message);
        string responseMessage = $"Woah, did you just say {payload?.Message}?";
        return Task.FromResult(new TestResponse { Message = responseMessage });
    }
}

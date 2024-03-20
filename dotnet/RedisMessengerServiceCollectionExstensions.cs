using Microsoft.Extensions.DependencyInjection;

namespace RedisMessenger;

public static class RedisMessengerServiceCollectionExtensions
{
    public static IServiceCollection AddRedisMessenger(this IServiceCollection services, Action<RedisMessengerConfiguration> configure)
    {
        RedisMessengerConfiguration config = new(services);
        configure(config);

        services.AddSingleton<IRedisMessenger>(serviceProvider => new RedisMessenger(config, serviceProvider));
        return services;
    }
}

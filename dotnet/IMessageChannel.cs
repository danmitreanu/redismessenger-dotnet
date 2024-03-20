namespace RedisMessenger;

public interface IMessageChannel<TReq, TRes>
{
    Task SendAsync(TReq request);
    Task<TRes?> QueryAsync(TReq request, CancellationToken token = default);
}

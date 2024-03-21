namespace RedisMessenger;

public class RedisMessengerResponseException : Exception
{
    internal RedisMessengerResponseException(string msg) : base(msg)
    {
    }
}

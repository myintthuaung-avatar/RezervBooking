using StackExchange.Redis;
using RezervBooking.Application;

namespace RezervBooking.Infrastructure;

public sealed class RedisScheduleLock(IConnectionMultiplexer redis) : IScheduleLock
{
    private static readonly LuaScript ReleaseScript = LuaScript.Prepare("if redis.call('get', @key) == @token then return redis.call('del', @key) else return 0 end");
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<IAsyncDisposable?> TryAcquireAsync(int scheduleId, CancellationToken cancellationToken)
    {
        var key = new RedisKey($"rezerv:booking-lock:schedule:{scheduleId}");
        var token = Guid.NewGuid().ToString("N");
        var acquired = await _database.StringSetAsync(key, token, TimeSpan.FromSeconds(15), When.NotExists).WaitAsync(cancellationToken);
        return acquired ? new RedisLockHandle(_database, key, token) : null;
    }

    private sealed class RedisLockHandle(IDatabase database, RedisKey key, RedisValue token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await database.ScriptEvaluateAsync(ReleaseScript, new { key, token });
        }
    }
}

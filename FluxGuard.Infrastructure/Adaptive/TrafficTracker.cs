namespace FluxGuard.Infrastructure.Adaptive;

using FluxGuard.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using StackExchange.Redis;
public class TrafficTracker
{
    private readonly RedisConnectionFactory _redis;

    public TrafficTracker(RedisConnectionFactory redis)
    {
        _redis = redis;
    }

    public async Task RecordRequestAsync(HttpContext context)
    {
        try
        {
            var endpoint = NormalizeEndpoint(context.Request.Path.Value ?? "/");
            var key = $"fluxguard:traffic:{endpoint}:current";
            var db = _redis.GetDatabase();

            await db.StringIncrementAsync(key);

            await db.KeyExpireAsync(key, TimeSpan.FromMinutes(1));
        }
        catch
        {
        }
    }

    private static string NormalizeEndpoint(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"/{parts[0]}/{parts[1]}" : path;
    }
}
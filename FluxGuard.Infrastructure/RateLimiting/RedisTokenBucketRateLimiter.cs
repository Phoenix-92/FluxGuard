namespace FluxGuard.Infrastructure.RateLimiting;

using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Models;
using FluxGuard.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class RedisTokenBucketRateLimiter : IRateLimiter
{
    private readonly RedisConnectionFactory _connectionFactory;
    private readonly ILogger<RedisTokenBucketRateLimiter> _logger;

    private readonly string _luaScript;

    public RedisTokenBucketRateLimiter(
        RedisConnectionFactory connectionFactory,
        ILogger<RedisTokenBucketRateLimiter> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;

        _luaScript = LoadLuaScript();
    }

    public async Task<RateLimitResult> IsAllowedAsync(string clientKey, RateLimitPolicy policy)
    {
        var db = _connectionFactory.GetDatabase();

        var redisKey = $"fluxguard:rl:{clientKey}";
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var ttlSeconds = (int)(policy.BucketCapacity / policy.RefillRatePerSecond * 2);

        try
        {
            var result = (RedisValue[])await db.ScriptEvaluateAsync(
                _luaScript,
                keys: new RedisKey[] { redisKey },
                values: new RedisValue[]
                {
                    policy.BucketCapacity,
                    policy.RefillRatePerSecond,
                    nowMs,
                    ttlSeconds
                });

            var allowed = (int)result[0] == 1;
            var remainingTokens = double.Parse((string)result[1]!);
            var retryAfterMs = double.Parse((string)result[2]!);

            _logger.LogDebug(
                "{Decision} | Key: {Key} | Remaining: {Tokens:F2} | Policy: {Policy}",
                allowed ? "ALLOWED" : "DENIED", redisKey, remainingTokens, policy.PolicyName);

            return new RateLimitResult
            {
                IsAllowed = allowed,
                RemainingTokens = remainingTokens,
                RetryAfterSeconds = retryAfterMs / 1000.0,
                AppliedPolicy = policy.PolicyName
            };
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis unavailable for key {Key} — failing open", redisKey);

            return new RateLimitResult
            {
                IsAllowed = true,
                RemainingTokens = -1,
                RetryAfterSeconds = 0,
                AppliedPolicy = $"{policy.PolicyName}:failopen"
            };
        }
    }

    private static string LoadLuaScript()
    {
        return """
            local key            = KEYS[1]
            local capacity       = tonumber(ARGV[1])
            local refill_rate    = tonumber(ARGV[2])
            local now_ms         = tonumber(ARGV[3])
            local ttl_seconds    = tonumber(ARGV[4])

            local data = redis.call('HMGET', key, 'tokens', 'last_refill')

            local current_tokens
            local last_refill_ms

            if data[1] == false then
                current_tokens  = capacity
                last_refill_ms  = now_ms
            else
                current_tokens  = tonumber(data[1])
                last_refill_ms  = tonumber(data[2])
            end

            local elapsed_seconds = (now_ms - last_refill_ms) / 1000.0
            local tokens_to_add   = elapsed_seconds * refill_rate
            local new_tokens      = math.min(capacity, current_tokens + tokens_to_add)

            local allowed         = 0
            local retry_after_ms  = 0

            if new_tokens >= 1.0 then
                new_tokens = new_tokens - 1.0
                allowed    = 1
            else
                retry_after_ms = math.ceil((1.0 - new_tokens) / refill_rate * 1000)
            end

            redis.call('HSET', key, 'tokens', tostring(new_tokens), 'last_refill', tostring(now_ms))
            redis.call('EXPIRE', key, ttl_seconds)

            return { allowed, tostring(new_tokens), tostring(retry_after_ms) }
            """;
    }
}
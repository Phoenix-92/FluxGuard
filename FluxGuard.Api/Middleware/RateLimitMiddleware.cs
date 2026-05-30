namespace FluxGuard.Api.Middleware;

using FluxGuard.Core.Abstractions;
using FluxGuard.Infrastructure.Adaptive;
using FluxGuard.Infrastructure.Redis;
using StackExchange.Redis;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly IPolicyResolver _policyResolver;
    private readonly TrafficTracker _trafficTracker;
    private readonly RedisConnectionFactory _redis;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        IPolicyResolver policyResolver,
        TrafficTracker trafficTracker,
        RedisConnectionFactory redis,
        ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _policyResolver = policyResolver;
        _trafficTracker = trafficTracker;
        _redis = redis;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientKey = ExtractClientKey(context);
        var policy = _policyResolver.Resolve(context);

        var throttledPolicy = await ApplyThrottleIfActiveAsync(context, policy);

        var result = await _rateLimiter.IsAllowedAsync(clientKey, policy);

        context.Response.Headers["X-RateLimit-Policy"] = result.AppliedPolicy;
        context.Response.Headers["X-RateLimit-Remaining"] = ((int)result.RemainingTokens).ToString();
        context.Response.Headers["X-RateLimit-Limit"] = throttledPolicy.EffectiveCapacity.ToString();

        if (!result.IsAllowed)
        {
            context.Response.Headers["Retry-After"] = ((int)result.RetryAfterSeconds + 1).ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                retryAfterSeconds = (int)result.RetryAfterSeconds + 1,
                policy = result.AppliedPolicy,
                limit = throttledPolicy.EffectiveCapacity
            });

            return;
        }

        await _next(context);
    }

    private async Task<Core.Models.RateLimitPolicy> ApplyThrottleIfActiveAsync(
        HttpContext context,
        Core.Models.RateLimitPolicy policy)
    {
        try
        {
            var endpoint = NormalizeEndpoint(context.Request.Path.Value ?? "/");
            var throttleKey = $"fluxguard:throttle:{endpoint}";
            var db = _redis.GetDatabase();
            var throttleVal = await db.StringGetAsync(throttleKey);

            if (throttleVal.HasValue && double.TryParse(throttleVal, out var multiplier))
            {
                _logger.LogDebug(
                    "Throttle active on {Endpoint} — multiplier: {Multiplier}",
                    endpoint, multiplier);

                return new Core.Models.RateLimitPolicy
                {
                    PolicyName = $"{policy.PolicyName}:throttled",
                    BucketCapacity = policy.BucketCapacity,
                    RefillRatePerSecond = policy.RefillRatePerSecond,
                    ThrottleMultiplier = multiplier
                };
            }
        }
        catch (RedisException)
        {
        }

        return policy;
    }

    private static string ExtractClientKey(HttpContext context)
    {
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrEmpty(apiKey))
            return $"apikey:{apiKey}";

        var userId = context.Request.Headers["X-User-Id"].ToString();
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        var ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                 ?? context.Connection.RemoteIpAddress?.ToString()
                 ?? "unknown";

        return $"ip:{ip}";
    }

    private static string NormalizeEndpoint(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"/{parts[0]}/{parts[1]}" : path;
    }
}
namespace FluxGuard.Api.Middleware;

using FluxGuard.Core.Abstractions;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimiter _rateLimiter;
    private readonly IPolicyResolver _policyResolver;
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(
        RequestDelegate next,
        IRateLimiter rateLimiter,
        IPolicyResolver policyResolver,
        ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _rateLimiter = rateLimiter;
        _policyResolver = policyResolver;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientKey = ExtractClientKey(context);
        var policy = _policyResolver.Resolve(context);

        var result = await _rateLimiter.IsAllowedAsync(clientKey, policy);

        context.Response.Headers["X-RateLimit-Policy"] = result.AppliedPolicy;
        context.Response.Headers["X-RateLimit-Remaining"] = ((int)result.RemainingTokens).ToString();

        if (!result.IsAllowed)
        {
            context.Response.Headers["Retry-After"] = ((int)result.RetryAfterSeconds + 1).ToString();
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            await context.Response.WriteAsJsonAsync(new
            {
                error = "Rate limit exceeded",
                retryAfterSeconds = (int)result.RetryAfterSeconds + 1,
                policy = result.AppliedPolicy
            });

            return;
        }

        await _next(context);
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
}
namespace FluxGuard.Core.Abstractions;
using FluxGuard.Core.Models;

public interface IRateLimiter
{
    Task<RateLimitResult> IsAllowedAsync(string clientKey, RateLimitPolicy policy);
}

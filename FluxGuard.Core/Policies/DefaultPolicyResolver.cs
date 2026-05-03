namespace FluxGuard.Core.Policies;

using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Models;
using Microsoft.AspNetCore.Http;

public class DefaultPolicyResolver : IPolicyResolver
{
    private static readonly Dictionary<string, RateLimitPolicy> Policies = new()
    {
        ["free"] = new RateLimitPolicy
        {
            PolicyName = "free",
            BucketCapacity = 10,
            RefillRatePerSecond = 1
        },
        ["premium"] = new RateLimitPolicy
        {
            PolicyName = "premium",
            BucketCapacity = 100,
            RefillRatePerSecond = 10
        },
        ["admin"] = new RateLimitPolicy
        {
            PolicyName = "admin",
            BucketCapacity = 10000,
            RefillRatePerSecond = 1000
        }
    };

    public RateLimitPolicy Resolve(HttpContext context)
    {
        var role = context.Request.Headers["X-User-Role"].ToString().ToLower();

        return Policies.TryGetValue(role, out var policy)
            ? policy
            : Policies["free"];
    }
}
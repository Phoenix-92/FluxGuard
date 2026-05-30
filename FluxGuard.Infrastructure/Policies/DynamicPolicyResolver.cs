namespace FluxGuard.Infrastructure.Policies;

using System.Collections.Concurrent;
using System.Text.Json;
using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Models;
using FluxGuard.Infrastructure.Redis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class DynamicPolicyResolver : IPolicyResolver
{
    private readonly RedisConnectionFactory _redis;
    private readonly ILogger<DynamicPolicyResolver> _logger;

    private readonly ConcurrentDictionary<string, (RateLimitPolicy Policy, DateTime CachedAt)>
        _cache = new();

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private static readonly RateLimitPolicy FallbackPolicy = new()
    {
        PolicyName = "fallback",
        BucketCapacity = 5,
        RefillRatePerSecond = 1
    };

    private static readonly Dictionary<string, RateLimitPolicy> DefaultPolicies = new()
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

    public DynamicPolicyResolver(
        RedisConnectionFactory redis,
        ILogger<DynamicPolicyResolver> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public RateLimitPolicy Resolve(HttpContext context)
    {
        var tier = ExtractTier(context);
        var endpoint = context.Request.Path.Value ?? "/";

        var endpointPolicyName = $"{tier}:{NormalizeEndpoint(endpoint)}";
        var policy = GetPolicy(endpointPolicyName)
                  ?? GetPolicy(tier)
                  ?? FallbackPolicy;

        _logger.LogDebug(
            "Resolved policy '{Policy}' for tier '{Tier}' on '{Endpoint}'",
            policy.PolicyName, tier, endpoint);

        return policy;
    }

    private RateLimitPolicy? GetPolicy(string policyName)
    {
        if (_cache.TryGetValue(policyName, out var cached))
        {
            if (DateTime.UtcNow - cached.CachedAt < CacheTtl)
                return cached.Policy;

            _cache.TryRemove(policyName, out _);
        }

        try
        {
            var db = _redis.GetDatabase();
            var key = $"fluxguard:cfg:policy:{policyName}";
            var value = db.StringGet(key);

            if (value.HasValue)
            {
                var policy = JsonSerializer.Deserialize<RateLimitPolicy>(value!);
                if (policy != null)
                {
                    _cache[policyName] = (policy, DateTime.UtcNow);
                    _logger.LogInformation(
                        "Policy '{Policy}' loaded from Redis", policyName);
                    return policy;
                }
            }
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during policy resolution for '{Policy}'", policyName);
        }

        if (DefaultPolicies.TryGetValue(policyName, out var defaultPolicy))
        {
            _cache[policyName] = (defaultPolicy, DateTime.UtcNow);
            return defaultPolicy;
        }

        return null;
    }

    private static string ExtractTier(HttpContext context)
    {
        var role = context.Request.Headers["X-User-Role"].ToString().ToLower();
        return role switch
        {
            "premium" => "premium",
            "admin" => "admin",
            _ => "free"
        };
    }

    private static string NormalizeEndpoint(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"/{parts[0]}/{parts[1]}"
            : path;
    }
}
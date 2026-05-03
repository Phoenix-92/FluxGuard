namespace FluxGuard.Core.Algorithms;

using FluxGuard.Core.Abstractions;
using FluxGuard.Core.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class TokenBucketRateLimiter : IRateLimiter
{
    private readonly ConcurrentDictionary<string, BucketState> _buckets = new();
    private readonly ILogger<TokenBucketRateLimiter> _logger;

    public TokenBucketRateLimiter(ILogger<TokenBucketRateLimiter> logger)
    {
        _logger = logger;
    }

    public Task<RateLimitResult> IsAllowedAsync(string clientKey, RateLimitPolicy policy)
    {
        var bucket = _buckets.GetOrAdd(clientKey, _ => new BucketState
        {
            Tokens = policy.BucketCapacity,
            LastRefillTime = DateTime.UtcNow
        });

        lock (bucket)
        {
            RefillTokens(bucket, policy);

            if (bucket.Tokens >= 1.0)
            {
                bucket.Tokens -= 1.0;

                _logger.LogDebug(
                    "ALLOWED | Key: {ClientKey} | Tokens remaining: {Tokens:F2} | Policy: {Policy}",
                    clientKey, bucket.Tokens, policy.PolicyName);

                return Task.FromResult(new RateLimitResult
                {
                    IsAllowed = true,
                    RemainingTokens = bucket.Tokens,
                    RetryAfterSeconds = 0,
                    AppliedPolicy = policy.PolicyName
                });
            }
            else
            {
                double secondsUntilNextToken = (1.0 - bucket.Tokens) / policy.RefillRatePerSecond;

                _logger.LogWarning(
                    "DENIED | Key: {ClientKey} | Tokens: {Tokens:F2} | RetryAfter: {RetryAfter:F2}s | Policy: {Policy}",
                    clientKey, bucket.Tokens, secondsUntilNextToken, policy.PolicyName);

                return Task.FromResult(new RateLimitResult
                {
                    IsAllowed = false,
                    RemainingTokens = 0,
                    RetryAfterSeconds = secondsUntilNextToken,
                    AppliedPolicy = policy.PolicyName
                });
            }
        }
    }

    private static void RefillTokens(BucketState bucket, RateLimitPolicy policy)
    {
        var now = DateTime.UtcNow;
        double elapsedSeconds = (now - bucket.LastRefillTime).TotalSeconds;

        if (elapsedSeconds <= 0) return;

        double tokensToAdd = elapsedSeconds * policy.RefillRatePerSecond;

        bucket.Tokens = Math.Min(policy.BucketCapacity, bucket.Tokens + tokensToAdd);
        bucket.LastRefillTime = now;
    }

    private class BucketState
    {
        public double Tokens { get; set; }
        public DateTime LastRefillTime { get; set; }
    }
}

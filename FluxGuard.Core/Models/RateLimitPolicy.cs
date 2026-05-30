namespace FluxGuard.Core.Models;

public class RateLimitPolicy
{
    public int BucketCapacity { get; init; }

    public double RefillRatePerSecond { get; init; }

    public string PolicyName { get; init; } = "default";

    public double ThrottleMultiplier { get; init; } = 1.0;

    public string? EndpointPattern { get; init; } 

    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;

    public int EffectiveCapacity => Math.Max(1, (int)(BucketCapacity * ThrottleMultiplier));

    public double EffectiveRefillRate => Math.Max(0.1, RefillRatePerSecond * ThrottleMultiplier);
}

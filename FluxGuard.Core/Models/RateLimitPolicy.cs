namespace FluxGuard.Core.Models;

public class RateLimitPolicy
{
    public int BucketCapacity { get; init; }

    public int RefillRatePerSecond { get; init; }

    public string PolicyName { get; init; } = "default";
}

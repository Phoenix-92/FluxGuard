namespace FluxGuard.Core.Models;

public class RateLimitResult
{
    public bool IsAllowed { get; init; }

    public double RemainingTokens { get; init; }

    public double RetryAfterSeconds { get; init; }

    public string AppliedPolicy { get; init; } = string.Empty;
}

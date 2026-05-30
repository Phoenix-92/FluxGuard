namespace FluxGuard.Infrastructure.Adaptive;

using FluxGuard.Infrastructure.Redis;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public class TrafficSpikeDetector : BackgroundService
{
    private readonly RedisConnectionFactory _redis;
    private readonly ILogger<TrafficSpikeDetector> _logger;

    private const double SpikeThresholdMultiplier = 3.0;

    private static readonly TimeSpan MonitoringWindow = TimeSpan.FromSeconds(10);

    private const double ThrottleMultiplierOnSpike = 0.3;

    private static readonly TimeSpan ThrottleDuration = TimeSpan.FromSeconds(30);

    public TrafficSpikeDetector(
        RedisConnectionFactory redis,
        ILogger<TrafficSpikeDetector> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TrafficSpikeDetector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await DetectSpikesAsync();
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task DetectSpikesAsync()
    {
        try
        {
            var db = _redis.GetDatabase();

            var trackedEndpoints = new[] { "/api/test", "/api/payment", "/api/data" };

            foreach (var endpoint in trackedEndpoints)
            {
                var currentKey = $"fluxguard:traffic:{endpoint}:current";
                var baselineKey = $"fluxguard:traffic:{endpoint}:baseline";

                var currentCount = (double?)await db.StringGetAsync(currentKey) ?? 0;
                var baselineCount = (double?)await db.StringGetAsync(baselineKey) ?? 0;

                if (baselineCount < 10) continue;

                var ratio = currentCount / baselineCount;

                if (ratio >= SpikeThresholdMultiplier)
                {
                    var throttleKey = $"fluxguard:throttle:{endpoint}";
                    await db.StringSetAsync(
                        throttleKey,
                        ThrottleMultiplierOnSpike.ToString(),
                        ThrottleDuration);

                    _logger.LogWarning(
                        "SPIKE DETECTED on {Endpoint} | Current: {Current} | " +
                        "Baseline: {Baseline} | Ratio: {Ratio:F2}x | Throttle applied",
                        endpoint, currentCount, baselineCount, ratio);
                }
                else
                {
                    await db.KeyDeleteAsync($"fluxguard:throttle:{endpoint}");

                    var newBaseline = (baselineCount * 0.8) + (currentCount * 0.2);
                    await db.StringSetAsync(baselineKey, newBaseline.ToString());
                }

                await db.StringSetAsync(currentKey, "0", MonitoringWindow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in spike detector — continuing");
        }
    }
}

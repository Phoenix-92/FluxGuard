namespace FluxGuard.Api.Controllers;

using System.Text.Json;
using FluxGuard.Core.Models;
using FluxGuard.Infrastructure.Redis;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

[ApiController]
[Route("admin/policies")]
public class AdminController : ControllerBase
{
    private readonly RedisConnectionFactory _redis;
    private readonly ILogger<AdminController> _logger;

    public AdminController(RedisConnectionFactory redis, ILogger<AdminController> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    [HttpPost("{policyName}")]
    public async Task<IActionResult> UpsertPolicy(
        string policyName,
        [FromBody] PolicyUpdateRequest request)
    {
        var policy = new RateLimitPolicy
        {
            PolicyName = policyName,
            BucketCapacity = request.BucketCapacity,
            RefillRatePerSecond = request.RefillRatePerSecond,
            LoadedAt = DateTime.UtcNow
        };

        var db = _redis.GetDatabase();
        var key = $"fluxguard:cfg:policy:{policyName}";

        await db.StringSetAsync(key, JsonSerializer.Serialize(policy));

        _logger.LogInformation(
            "Policy '{Policy}' updated | Capacity: {Capacity} | Rate: {Rate}",
            policyName, request.BucketCapacity, request.RefillRatePerSecond);

        return Ok(new
        {
            message = $"Policy '{policyName}' updated. Propagates within 60 seconds.",
            policy
        });
    }

    [HttpGet("{policyName}")]
    public async Task<IActionResult> GetPolicy(string policyName)
    {
        var db = _redis.GetDatabase();
        var key = $"fluxguard:cfg:policy:{policyName}";
        var value = await db.StringGetAsync(key);

        if (!value.HasValue)
            return NotFound(new { message = $"Policy '{policyName}' not in Redis. Using defaults." });

        return Ok(JsonSerializer.Deserialize<RateLimitPolicy>(value!));
    }
}

public record PolicyUpdateRequest(int BucketCapacity, double RefillRatePerSecond);
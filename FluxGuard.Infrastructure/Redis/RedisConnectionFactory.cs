namespace FluxGuard.Infrastructure.Redis;

using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class RedisConnectionFactory : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;
    private readonly ILogger<RedisConnectionFactory> _logger;

    public RedisConnectionFactory(IConfiguration config, ILogger<RedisConnectionFactory> logger)
    {
        _logger = logger;

        _connection = new Lazy<ConnectionMultiplexer>(() =>
        {
            var connectionString = config.GetConnectionString("Redis")
                ?? "localhost:6379";

            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;  // Don't crash on startup if Redis is down
            options.ConnectRetry = 3;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000);

            var multiplexer = ConnectionMultiplexer.Connect(options);

            multiplexer.ConnectionFailed += (_, e) =>
                _logger.LogError("Redis connection failed: {Endpoint} — {FailureType}",
                    e.EndPoint, e.FailureType);

            multiplexer.ConnectionRestored += (_, e) =>
                _logger.LogInformation("Redis connection restored: {Endpoint}", e.EndPoint);

            return multiplexer;
        });
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
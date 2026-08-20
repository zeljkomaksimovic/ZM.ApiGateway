using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ZM.ApiGateway.Api.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IConnectionMultiplexer _connection;

        public RedisHealthCheck(IConnectionMultiplexer connection)
        {
            _connection = connection;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var database = _connection.GetDatabase();

                await database.PingAsync();

                return HealthCheckResult.Healthy();

            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Redis is unavailable.", ex);
            }

        }
    }
}

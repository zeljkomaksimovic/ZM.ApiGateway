using ZM.RateLimiter.Core.Enums;
using ZM.RateLimiter.Core.Models;

namespace ZM.ApiGateway.Api.UnitTests.Builders
{
    internal static class RateLimitOutcomeBuilder
    {
        public static RateLimitOutcome Allowed(long limit = 10, long remaining = 9) 
        {
            return new RateLimitOutcome(
            new RateLimitPolicy(
                "free",
                RateLimitingAlgorithmType.FixedWindow,
                limit,
                TimeSpan.FromMinutes(1)),
            RateLimitResult.Allowed(limit, remaining));
        }

        public static RateLimitOutcome Denied(long limit = 10, TimeSpan? retryAfter = null)
        {
            return new RateLimitOutcome(
            new RateLimitPolicy(
                "free",
                RateLimitingAlgorithmType.FixedWindow,
                limit,
                TimeSpan.FromMinutes(1)),
            RateLimitResult.Denied(
                limit,
                retryAfter ?? TimeSpan.FromSeconds(30)));
        }

        public static RateLimitOutcome? NoPolicy() => null;
    }
}

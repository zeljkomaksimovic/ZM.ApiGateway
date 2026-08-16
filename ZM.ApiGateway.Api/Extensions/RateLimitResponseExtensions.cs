using ZM.RateLimiter.Core.Models;

namespace ZM.ApiGateway.Api.Extensions
{
    public static class RateLimitResponseExtensions
    {
        public static void AddRateLimitHeaders(this HttpResponse response, RateLimitResult result)
        {
            response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
            response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();

            if (result.RetryAfter.HasValue)
            {
                response.Headers["Retry-After"] = Math.Ceiling(result.RetryAfter.Value.TotalSeconds).ToString();
            }
        }
    }
}

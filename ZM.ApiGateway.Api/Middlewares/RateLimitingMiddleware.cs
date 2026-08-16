using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using ZM.ApiGateway.Api.Extensions;
using ZM.RateLimiter.Core.Abstractions;

namespace ZM.ApiGateway.Api.Middlewares
{
    public sealed class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;

        public RateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter)
        {
            var clientKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(clientKey))
            {
                await _next(context);
                return;
            }

            var proxyFeature = context.GetReverseProxyFeature();

            var resource = proxyFeature.Route.Config.RouteId;

            var outcome = await rateLimiter.ConsumeAsync(
                clientKey,
                resource,
                context.RequestAborted);

            if (outcome is null)
            {
                await _next(context);
                return;
            }

            var result = outcome.Result;

            context.Response.AddRateLimitHeaders(result);

            if (!result.IsAllowed)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            await _next(context);
        }
    }
}
using System.Diagnostics;
using System.Security.Claims;
using ZM.ApiGateway.Api.Extensions;
using ZM.RateLimiter.Core.Abstractions;
using ZM.RateLimiter.Core.Models;

namespace ZM.ApiGateway.Api.Middlewares
{
    public sealed class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RateLimitingMiddleware> _logger;

        public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IRateLimiter rateLimiter)
        {
            var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            var clientKey = ResolveClientKey(context);

            if (string.IsNullOrWhiteSpace(clientKey))
            {
                // Neither an identity nor a peer address: there is no key to meter on, so refuse
                // rather than let the request through unmetered.
                _logger.LogWarning("Unable to identify the caller for rate limiting. TraceId: {TraceId}.", traceId);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            var proxyFeature = context.GetReverseProxyFeature();
            var resource = proxyFeature.Route.Config.RouteId;

            RateLimitOutcome? outcome;

            try
            {
                outcome = await rateLimiter.ConsumeAsync(
                    clientKey,
                    resource,
                    context.RequestAborted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rate limiter failed for client {ClientKey} on resource {Resource}. TraceId: {TraceId}.",
                    clientKey,
                    resource,
                    traceId);

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

                return;
            }

            if (outcome is null)
            {
                _logger.LogWarning("No rate-limit policy found for client {ClientKey} on resource {Resource}. TraceId: {TraceId}.",
                    clientKey,
                    resource,
                    traceId);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            var result = outcome.Result;

            context.Response.AddRateLimitHeaders(result);

            if (!result.IsAllowed)
            {
                _logger.LogWarning("Rate limit exceeded for client {ClientKey} on resource {Resource}. TraceId: {TraceId}. Retry after {RetryAfter}.",
                    clientKey,
                    resource,
                    traceId,
                    result.RetryAfter);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                return;
            }

            _logger.LogInformation("Rate limit check passed for client {ClientKey} on resource {Resource}. TraceId: {TraceId}. Remaining: {Remaining}/{Limit}.",
                clientKey,
                resource,
                traceId,
                result.Remaining,
                result.Limit);

            await _next(context);
        }

        /// <summary>
        /// Prefers the authenticated subject, falling back to the peer address so anonymous callers
        /// are still metered. Only the IP form is prefixed, which keeps raw subject ids matching the
        /// "ClientPolicies" configuration while ensuring an address can never collide with one.
        /// </summary>
        private static string? ResolveClientKey(HttpContext context)
        {
            var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                return subject;
            }

            var address = context.Connection.RemoteIpAddress?.ToString();

            return string.IsNullOrWhiteSpace(address)
                ? null
                : $"ip:{address}";
        }
    }
}
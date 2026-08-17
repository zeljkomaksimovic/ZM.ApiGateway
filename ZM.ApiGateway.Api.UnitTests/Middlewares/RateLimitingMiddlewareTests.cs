using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;
using Yarp.ReverseProxy.Model;
using ZM.ApiGateway.Api.Middlewares;
using ZM.ApiGateway.Api.UnitTests.Builders;
using ZM.RateLimiter.Core.Abstractions;

namespace ZM.ApiGateway.Api.UnitTests.Middlewares
{
    public class RateLimitingMiddlewareTests
    {
        [Theory]
        [AutoMoqInlineData]
        public async Task InvokeAsync_WhenRequestIsAllowed_CallsNextAndAddsRateLimitHeaders(
            [Frozen] Mock<IRateLimiter> rateLimiter,
            [Frozen] Mock<IReverseProxyFeature> proxyFeature,
            [Frozen] Mock<RequestDelegate> next,
            RateLimitingMiddleware middleware)
        {
            //Arrange
            var userId = "123";
            var resource = "order-route";

            proxyFeature
                .SetupGet(x => x.Route)
                .Returns(RouteModelBuilder.WithRouteId(resource));

            var context = new DefaultHttpContext();

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ],
                    "Test"));

            context.Features.Set(proxyFeature.Object);


            rateLimiter
                .Setup(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RateLimitOutcomeBuilder.Allowed());

            //Act
            await middleware.InvokeAsync(context, rateLimiter.Object);

            //Assert
            next.Verify(x => x(context), Times.Once);
            context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
            context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("9");
            rateLimiter.Verify(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [AutoMoqInlineData]
        public async Task InvokeAsync_WhenRequestIsDenied_Returns429StatusCodeAndDoesNotCallNext(
            [Frozen] Mock<IRateLimiter> rateLimiter,
            [Frozen] Mock<IReverseProxyFeature> proxyFeature,
            [Frozen] Mock<RequestDelegate> next,
            RateLimitingMiddleware middleware)
        {
            //Arrange
            var userId = "123";
            var resource = "order-route";

            proxyFeature
                .SetupGet(x => x.Route)
                .Returns(RouteModelBuilder.WithRouteId(resource));

            var context = new DefaultHttpContext();

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ],
                    "Test"));

            context.Features.Set(proxyFeature.Object);


            rateLimiter
                .Setup(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RateLimitOutcomeBuilder.Denied());

            //Act
            await middleware.InvokeAsync(context, rateLimiter.Object);

            //Assert
            next.Verify(x => x(context), Times.Never);
            context.Response.Headers["Retry-After"].ToString().Should().Be("30");
            context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            context.Response.Headers["X-RateLimit-Limit"].ToString().Should().Be("10");
            context.Response.Headers["X-RateLimit-Remaining"].ToString().Should().Be("0");
            rateLimiter.Verify(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [AutoMoqInlineData]
        public async Task InvokeAsync_WhenNoPolicyExists_Returns403StatusCodeAndDoesNotCallNext(
            [Frozen] Mock<IRateLimiter> rateLimiter,
            [Frozen] Mock<IReverseProxyFeature> proxyFeature,
            [Frozen] Mock<RequestDelegate> next,
            RateLimitingMiddleware middleware)
        {
            //Arrange
            var userId = "123";
            var resource = "order-route";

            proxyFeature
                .SetupGet(x => x.Route)
                .Returns(RouteModelBuilder.WithRouteId(resource));

            var context = new DefaultHttpContext();

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ],
                    "Test"));

            context.Features.Set(proxyFeature.Object);


            rateLimiter
                .Setup(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()))
                .ReturnsAsync(RateLimitOutcomeBuilder.NoPolicy);

            //Act
            await middleware.InvokeAsync(context, rateLimiter.Object);

            //Assert
            next.Verify(x => x(context), Times.Never);
            context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
            rateLimiter.Verify(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [AutoMoqInlineData]
        public async Task InvokeAsync_WhenRateLimiterThrowException_Returns503StatusCodeAndDoesNotCallNext(
            [Frozen] Mock<IRateLimiter> rateLimiter,
            [Frozen] Mock<IReverseProxyFeature> proxyFeature,
            [Frozen] Mock<RequestDelegate> next,
            RateLimitingMiddleware middleware)
        {
            //Arrange
            var userId = "123";
            var resource = "order-route";

            proxyFeature
                .SetupGet(x => x.Route)
                .Returns(RouteModelBuilder.WithRouteId(resource));

            var context = new DefaultHttpContext();

            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    ],
                    "Test"));

            context.Features.Set(proxyFeature.Object);


            rateLimiter
                .Setup(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("An error occurred while checking rate limit."));

            //Act
            await middleware.InvokeAsync(context, rateLimiter.Object);

            //Assert
            next.Verify(x => x(context), Times.Never);
            context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
            rateLimiter.Verify(x => x.ConsumeAsync(userId, resource, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [AutoMoqInlineData((string?)null)]
        [AutoMoqInlineData("")]
        [AutoMoqInlineData("   ")]
        public async Task InvokeAsync_WhenClientIdentityIsMissing_CallsNextAndDoesNotCallRateLimiter(
            string? clientId,
            [Frozen] Mock<IRateLimiter> rateLimiter,
            [Frozen] Mock<RequestDelegate> next,
            RateLimitingMiddleware middleware)
        {
            //Arrange
            var context = new DefaultHttpContext();

            if (clientId is not null)
            {
                context.User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, clientId)
                        ],
                        "Test"));
            }

            //Act
            await middleware.InvokeAsync(context, rateLimiter.Object);

            //Assert
            next.Verify(x => x(context), Times.Once);

            rateLimiter.Verify(
                x => x.ConsumeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}

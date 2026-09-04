using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZM.ApiGateway.Api.IntegrationTests.Builders;
using ZM.ApiGateway.Api.IntegrationTests.Infrastructure;
using ZM.RateLimiter.Core.Abstractions;
using ZM.RateLimiter.Core.Models;

namespace ZM.ApiGateway.Api.IntegrationTests
{
    public class GatewayIntegrationTests : IClassFixture<RedisFixture>
    {
        private readonly RedisFixture _redis;

        public GatewayIntegrationTests(RedisFixture redis)
        {
            _redis = redis;
        }

        [Fact]
        public async Task Request_WithValidTokenAndPolicy_IsForwarded()
        {
            // Arrange
            var clientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);
            using var client = CreateClient(factory, clientId, "user");

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            factory.Downstream.Requests.Should().ContainSingle();

            var forwarded = factory.Downstream.Requests[0];

            forwarded.Method.Should().Be("GET");
            forwarded.Uri.Host.Should().Be("zm.users.api");
            forwarded.Uri.Port.Should().Be(5100);
            forwarded.Uri.AbsolutePath.Should().Be("/api/users");

            response.Headers.Contains("X-RateLimit-Limit").Should().BeTrue();
            response.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();
        }

        [Fact]
        public async Task Request_WithoutToken_Returns401()
        {
            // Arrange
            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);
            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Should().Contain(x => x.Scheme == "Bearer");
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task Request_WithInvalidToken_Returns401()
        {
            // Arrange
            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);

            // Not a JWT at all, so the token fails to parse before any validation parameter is consulted.
            using var client = CreateClientWithToken(factory, "not-a-real-jwt");

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Should().Contain(x => x.Scheme == "Bearer");
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task Request_WithInsufficientRole_Returns403()
        {
            // Arrange
            var clientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);

            // "payments-route" requires the admin policy; this token only carries the user role.
            using var client = CreateClient(factory, clientId, "user");

            // Act
            var response = await client.GetAsync("/api/payments");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task Request_WhenClientHasNoPolicy_Returns403()
        {
            // Arrange
            var clientId = NewClientId();

            // No default policy and an unmapped client, so the provider resolves no policy at all.
            var overrides = new Dictionary<string, string?>
            {
                ["RateLimiting:DefaultPolicy"] = string.Empty
            };

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString, overrides);

            // The token is authorized for this route, so authorization cannot be the source of the 403.
            using var client = CreateClient(factory, clientId, "user");

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task Request_WhenRateLimitIsExceeded_Returns429()
        {
            // Arrange
            var clientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString, WithLimit(2));
            using var client = CreateClient(factory, clientId, "user");

            // Act
            var first = await client.GetAsync("/api/users");
            var second = await client.GetAsync("/api/users");
            var third = await client.GetAsync("/api/users");

            // Assert
            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);
            third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            third.Headers.GetValues("X-RateLimit-Limit").Single().Should().Be("2");
            third.Headers.GetValues("X-RateLimit-Remaining").Single().Should().Be("0");
            third.Headers.Contains("Retry-After").Should().BeTrue();

            factory.Downstream.Requests.Should().HaveCount(2);
        }

        [Fact]
        public async Task Requests_FromDifferentClients_HaveIndependentRateLimits()
        {
            // Arrange
            var firstClientId = NewClientId();
            var secondClientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString, WithLimit(1));
            using var firstClient = CreateClient(factory, firstClientId, "user");
            using var secondClient = CreateClient(factory, secondClientId, "user");

            // Act
            var firstClientAllowed = await firstClient.GetAsync("/api/users");
            var firstClientDenied = await firstClient.GetAsync("/api/users");
            var secondClientAllowed = await secondClient.GetAsync("/api/users");

            // Assert
            firstClientAllowed.StatusCode.Should().Be(HttpStatusCode.OK);
            firstClientDenied.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            // The second client is untouched by the first client's exhausted budget.
            secondClientAllowed.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Requests_ToDifferentRoutes_HaveIndependentRateLimits()
        {
            // Arrange
            var clientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString, WithLimit(1));
            using var client = CreateClient(factory, clientId, "user");

            // Act
            var usersAllowed = await client.GetAsync("/api/users");
            var usersDenied = await client.GetAsync("/api/users");
            var ordersAllowed = await client.GetAsync("/api/orders");

            // Assert
            usersAllowed.StatusCode.Should().Be(HttpStatusCode.OK);
            usersDenied.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            // Same client, different route: the budget is keyed per resource.
            ordersAllowed.StatusCode.Should().Be(HttpStatusCode.OK);

            factory.Downstream.Requests
                .Select(x => x.Uri.Host)
                .Should()
                .BeEquivalentTo(["zm.users.api", "zm.orders.api"]);
        }

        [Fact]
        public async Task Request_WhenRateLimiterThrows_Returns503()
        {
            // Arrange
            var clientId = NewClientId();

            using var factory = new GatewayWebApplicationFactory(
                _redis.ConnectionString,
                configureServices: services =>
                {
                    services.RemoveAll<IRateLimiter>();
                    services.AddSingleton<IRateLimiter, ThrowingRateLimiter>();
                });

            using var client = CreateClient(factory, clientId, "user");

            // Act
            var response = await client.GetAsync("/api/users");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task HealthLive_WithoutToken_ReturnsHealthy()
        {
            // Arrange
            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);
            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/live");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");

            // Mapped ahead of the proxy, so it answers locally.
            factory.Downstream.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task HealthReady_WhenRedisIsAvailable_ReturnsHealthy()
        {
            // Arrange
            using var factory = new GatewayWebApplicationFactory(_redis.ConnectionString);
            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
        }

        [Fact]
        public async Task HealthReady_WhenRedisIsUnavailable_ReturnsServiceUnavailable()
        {
            // Arrange
            using var factory = new GatewayWebApplicationFactory(UnreachableRedis);
            using var client = factory.CreateClient();

            // Act
            var response = await client.GetAsync("/health/ready");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
        }

        private const string UnreachableRedis =
            "127.0.0.1:1,connectTimeout=200,connectRetry=0,syncTimeout=200,asyncTimeout=200";

        private static string NewClientId() => Guid.NewGuid().ToString();

        private static Dictionary<string, string?> WithLimit(int limit) => new()
        {
            ["RateLimiting:Policies:free:Limit"] = limit.ToString()
        };

        private static HttpClient CreateClient(GatewayWebApplicationFactory factory, string clientId, params string[] roles)
            => CreateClientWithToken(factory, JwtBuilder.Create(clientId, roles));

        private static HttpClient CreateClientWithToken(GatewayWebApplicationFactory factory, string token)
        {
            var client = factory.CreateClient();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return client;
        }

        private sealed class ThrowingRateLimiter : IRateLimiter
        {
            public Task<RateLimitOutcome?> ConsumeAsync(
                string clientKey,
                string? resource = null,
                CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Rate limiting store is unavailable.");
        }
    }
}

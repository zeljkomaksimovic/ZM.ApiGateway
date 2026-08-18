using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ZM.ApiGateway.Api.UnitTests.Builders;
using ZM.RateLimiter.Core.Enums;
using ZM.RateLimiter.Core.Options;
using ZM.RateLimiter.Core.Policies;

namespace ZM.ApiGateway.Api.UnitTests.Policies
{
    public class ConfigurationRateLimitPolicyProviderTests
    {
        [Theory]
        [AutoMoqInlineData]
        public async Task GetPolicyAsync_WhenClientHasPolicy_ReturnsConfiguredPolicy(
            [Frozen] Mock<IOptionsMonitor<RateLimitingOptions>> optionsMonitor,
            ConfigurationRateLimitPolicyProvider provider)
        {
            //Arrange
            var clientKey = "123";

            optionsMonitor
                .SetupGet(x => x.CurrentValue)
                .Returns(RateLimitingOptionsBuilder.WithClientPolicy(clientKey));

            //Act
            var policy = await provider.GetPolicyAsync(clientKey);

            //Assert
            policy.Should().NotBeNull();
            policy!.Name.Should().Be("pro");
            policy.Algorithm.Should().Be(RateLimitingAlgorithmType.SlidingWindow);
            policy.Limit.Should().Be(1000);
            policy.Window.Should().Be(TimeSpan.FromMinutes(5));
        }

        [Theory]
        [AutoMoqInlineData]
        public async Task GetPolicyAsync_WhenClientHasNoPolicy_ReturnsDefaultPolicy(
            [Frozen] Mock<IOptionsMonitor<RateLimitingOptions>> optionsMonitor,
            ConfigurationRateLimitPolicyProvider provider)
        {
            //Arrange
            var clientKey = "unmapped-client";

            optionsMonitor
                .SetupGet(x => x.CurrentValue)
                .Returns(RateLimitingOptionsBuilder.WithoutClientPolicy());

            //Act
            var policy = await provider.GetPolicyAsync(clientKey);

            //Assert
            policy.Should().NotBeNull();
            policy!.Name.Should().Be("free");
            policy.Algorithm.Should().Be(RateLimitingAlgorithmType.FixedWindow);
            policy.Limit.Should().Be(60);
            policy.Window.Should().Be(TimeSpan.FromMinutes(1));
        }

        [Theory]
        [AutoMoqInlineData((string?)null)]
        [AutoMoqInlineData("")]
        [AutoMoqInlineData("   ")]
        public async Task GetPolicyAsync_WhenClientHasNoPolicyAndNoDefaultPolicy_ReturnsNull(
            string? defaultPolicy,
            [Frozen] Mock<IOptionsMonitor<RateLimitingOptions>> optionsMonitor,
            ConfigurationRateLimitPolicyProvider provider)
        {
            //Arrange
            var clientKey = "unmapped-client";

            optionsMonitor
                .SetupGet(x => x.CurrentValue)
                .Returns(RateLimitingOptionsBuilder.WithoutClientPolicy(defaultPolicy));

            //Act
            var policy = await provider.GetPolicyAsync(clientKey);

            //Assert
            policy.Should().BeNull();
        }

        [Theory]
        [AutoMoqInlineData]
        public async Task GetPolicyAsync_WhenClientPolicyReferencesUnknownPolicy_ReturnsNull(
            [Frozen] Mock<IOptionsMonitor<RateLimitingOptions>> optionsMonitor,
            ConfigurationRateLimitPolicyProvider provider)
        {
            //Arrange
            var clientKey = "123";

            optionsMonitor
                .SetupGet(x => x.CurrentValue)
                .Returns(RateLimitingOptionsBuilder.WithClientPolicy(clientKey, "gold"));

            //Act
            var policy = await provider.GetPolicyAsync(clientKey);

            //Assert
            policy.Should().BeNull();
        }
    }
}

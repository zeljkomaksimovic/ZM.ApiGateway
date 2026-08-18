using ZM.RateLimiter.Core.Enums;
using ZM.RateLimiter.Core.Options;

namespace ZM.ApiGateway.Api.UnitTests.Builders
{
    internal static class RateLimitingOptionsBuilder
    {
        public static RateLimitingOptions WithClientPolicy(string clientKey, string policyName = "pro")
        {
            var options = Create("free");

            options.ClientPolicies[clientKey] = policyName;

            return options;
        }

        public static RateLimitingOptions WithoutClientPolicy(string? defaultPolicy = "free")
        {
            var options = Create(defaultPolicy);

            options.ClientPolicies["some-other-client"] = "pro";

            return options;
        }

        private static RateLimitingOptions Create(string? defaultPolicy) => new()
        {
            DefaultPolicy = defaultPolicy,
            Policies =
            {
                ["free"] = new RateLimitPolicyOptions
                {
                    Algorithm = RateLimitingAlgorithmType.FixedWindow,
                    Limit = 60,
                    Window = TimeSpan.FromMinutes(1)
                },
                ["pro"] = new RateLimitPolicyOptions
                {
                    Algorithm = RateLimitingAlgorithmType.SlidingWindow,
                    Limit = 1000,
                    Window = TimeSpan.FromMinutes(5)
                }
            }
        };
    }
}

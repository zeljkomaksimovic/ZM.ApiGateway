using Yarp.ReverseProxy.Forwarder;
using ZM.ApiGateway.Api.ReverseProxy;
using ZM.RateLimiter.Core.DependencyInjection;
using ZM.RateLimiter.Redis.DependencyInjection;

namespace ZM.ApiGateway.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddReverseProxy()
                .LoadFromConfig(configuration.GetSection("ReverseProxy"));

            // Required by the per-route "Timeout" in ReverseProxy config; YARP throws without it.
            services.AddRequestTimeouts();

            services.AddJwtAuthentication(configuration);
            services.AddGatewayAuthorization();

            services.AddRateLimiterCore(configuration);
            services.AddRedisRateLimiter(configuration);

            // Registers the keyed "gateway-retry" pipeline that ResilientForwarderHttpClientFactory takes.
            services.AddGatewayResilience();

            RegisterForwarderHttpClientFactory(services);

            return services;
        }

        private static void RegisterForwarderHttpClientFactory(IServiceCollection services)
        {
            services.AddSingleton<IForwarderHttpClientFactory, ResilientForwarderHttpClientFactory>();
        }
    }
}

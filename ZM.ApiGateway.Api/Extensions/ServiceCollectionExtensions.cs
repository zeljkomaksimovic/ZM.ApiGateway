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

            services.AddJwtAuthentication(configuration);
            services.AddGatewayAuthorization();

            services.AddRateLimiterCore(configuration);
            services.AddRedisRateLimiter(configuration);

            return services;
        }
    }
}

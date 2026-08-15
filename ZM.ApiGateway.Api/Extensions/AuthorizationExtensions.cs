namespace ZM.ApiGateway.Api.Extensions
{
    public static class AuthorizationExtensions
    {
        public static IServiceCollection AddGatewayAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy("admin", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("admin");
                });

                options.AddPolicy("user", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireRole("user", "admin");
                });
            });

            return services;
        }
    }
}

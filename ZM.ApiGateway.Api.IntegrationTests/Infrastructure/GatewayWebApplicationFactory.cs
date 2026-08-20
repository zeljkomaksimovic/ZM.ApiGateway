using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Forwarder;
using ZM.ApiGateway.Api.IntegrationTests.Builders;

namespace ZM.ApiGateway.Api.IntegrationTests.Infrastructure
{
    public sealed class GatewayWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _redisConnectionString;
        private readonly IDictionary<string, string?>? _configOverrides;
        private readonly Action<IServiceCollection>? _configureServices;

        public GatewayWebApplicationFactory(
            string redisConnectionString,
            IDictionary<string, string?>? configOverrides = null,
            Action<IServiceCollection>? configureServices = null)
        {
            _redisConnectionString = redisConnectionString;
            _configOverrides = configOverrides;
            _configureServices = configureServices;
        }

        public RecordingHandler Downstream { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["Redis:ConnectionString"] = _redisConnectionString,

                    ["Jwt:Issuer"] = JwtBuilder.Issuer,
                    ["Jwt:Audience"] = JwtBuilder.Audience,
                    ["Jwt:SecretKey"] = JwtBuilder.SecretKey
                };

                if (_configOverrides is not null)
                {
                    foreach (var (key, value) in _configOverrides)
                    {
                        settings[key] = value;
                    }
                }

                config.AddInMemoryCollection(settings);
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new StubForwarderHttpClientFactory(Downstream));

                _configureServices?.Invoke(services);
            });
        }
    }
}

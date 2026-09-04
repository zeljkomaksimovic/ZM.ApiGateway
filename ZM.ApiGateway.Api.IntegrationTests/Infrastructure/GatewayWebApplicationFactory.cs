using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Yarp.ReverseProxy.Forwarder;
using ZM.ApiGateway.Api.IntegrationTests.Builders;
using ZM.ApiGateway.Api.Resilience;

namespace ZM.ApiGateway.Api.IntegrationTests.Infrastructure
{
    public sealed class GatewayWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _redisConnectionString;
        private readonly IDictionary<string, string?>? _configOverrides;
        private readonly Action<IServiceCollection>? _configureServices;
        private readonly bool _useResilientForwarder;

        public GatewayWebApplicationFactory(
            string redisConnectionString,
            IDictionary<string, string?>? configOverrides = null,
            Action<IServiceCollection>? configureServices = null,
            bool useResilientForwarder = false)
        {
            _redisConnectionString = redisConnectionString;
            _configOverrides = configOverrides;
            _configureServices = configureServices;
            _useResilientForwarder = useResilientForwarder;
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
                services.AddSingleton<IForwarderHttpClientFactory>(provider =>
                {
                    if (!_useResilientForwarder)
                    {
                        return new StubForwarderHttpClientFactory(Downstream);
                    }

                    // Keep the real ResilienceHandler and the configured "gateway-retry" pipeline,
                    // replacing only the socket underneath, so retries are exercised through YARP.
                    var pipeline = provider
                        .GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>("gateway-retry");

                    return new StubForwarderHttpClientFactory(new ResilienceHandler(pipeline, Downstream));
                });

                _configureServices?.Invoke(services);
            });
        }
    }
}

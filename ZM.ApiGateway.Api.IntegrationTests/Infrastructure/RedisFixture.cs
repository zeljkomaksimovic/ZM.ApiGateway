using Testcontainers.Redis;

namespace ZM.ApiGateway.Api.IntegrationTests.Infrastructure
{
    public sealed class RedisFixture : IAsyncLifetime
    {
        private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

        public string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }
    }
}

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using ZM.ApiGateway.Api.Resilience;

namespace ZM.ApiGateway.Api.UnitTests.Resilience
{
    public sealed class ResilienceHandlerTests : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

        public ResilienceHandlerTests()
        {
            var services = new ServiceCollection();

            services.AddGatewayResilience();

            _provider = services.BuildServiceProvider();
            _pipeline = _provider.GetRequiredKeyedService<ResiliencePipeline<HttpResponseMessage>>("gateway-retry");
        }

        [Fact]
        public async Task SendAsync_WhenGetFailsThenSucceeds_RetriesAndReturnsSuccess()
        {
            //Arrange
            var downstream = new SequencedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

            using var invoker = new HttpMessageInvoker(new ResilienceHandler(_pipeline, downstream));
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://downstream/api/users");

            //Act
            using var response = await invoker.SendAsync(request, CancellationToken.None);

            //Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            downstream.Invocations.Should().Be(2);
        }

        [Fact]
        public async Task SendAsync_WhenMethodIsNotGetOrHead_DoesNotRetry()
        {
            //Arrange
            var downstream = new SequencedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

            using var invoker = new HttpMessageInvoker(new ResilienceHandler(_pipeline, downstream));
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://downstream/api/users");

            //Act
            using var response = await invoker.SendAsync(request, CancellationToken.None);

            //Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            downstream.Invocations.Should().Be(1);
        }

        public void Dispose() => _provider.Dispose();

        private sealed class SequencedHandler : HttpMessageHandler
        {
            private readonly Queue<HttpStatusCode> _statuses;

            public SequencedHandler(params HttpStatusCode[] statuses)
            {
                _statuses = new Queue<HttpStatusCode>(statuses);
            }

            public int Invocations { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Invocations++;

                var status = _statuses.Count > 0
                    ? _statuses.Dequeue()
                    : HttpStatusCode.OK;

                return Task.FromResult(new HttpResponseMessage(status));
            }
        }
    }
}

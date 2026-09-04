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

        // Pins connection hygiene we rely on but do not implement: Polly disposes an outcome it
        // discards when the result is IDisposable, so the abandoned response releases its connection
        // without the handler doing anything. Guards against a Polly upgrade quietly changing that.
        [Fact]
        public async Task SendAsync_WhenRetrying_TheDiscardedResponseIsReleased()
        {
            //Arrange
            var downstream = new SequencedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

            using var invoker = new HttpMessageInvoker(new ResilienceHandler(_pipeline, downstream));
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://downstream/api/users");

            //Act
            using var response = await invoker.SendAsync(request, CancellationToken.None);

            //Assert
            downstream.Responses.Should().HaveCount(2);

            await FluentActions
                .Awaiting(() => downstream.Responses[0].Content.ReadAsStringAsync())
                .Should()
                .ThrowAsync<ObjectDisposedException>();

            // The one actually handed back to the caller must survive.
            (await response.Content.ReadAsStringAsync()).Should().Be("payload");
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

            public int Invocations => Responses.Count;

            /// <summary>Every response handed out, so tests can inspect the ones the pipeline discarded.</summary>
            public List<HttpResponseMessage> Responses { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var status = _statuses.Count > 0
                    ? _statuses.Dequeue()
                    : HttpStatusCode.OK;

                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent("payload")
                };

                Responses.Add(response);

                return Task.FromResult(response);
            }
        }
    }
}

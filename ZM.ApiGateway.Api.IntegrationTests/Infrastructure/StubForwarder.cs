using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

namespace ZM.ApiGateway.Api.IntegrationTests.Infrastructure
{
    public sealed record ForwardedRequest(string Method, Uri Uri, string? Authorization);

    public sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<ForwardedRequest> _requests = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<ForwardedRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return _requests.ToArray();
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var forwarded = new ForwardedRequest(
                request.Method.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString());

            lock (_gate)
            {
                _requests.Add(forwarded);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"summary":"Warm"}]""", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    public sealed class StubForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubForwarderHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => new(_handler, disposeHandler: false);
    }
}

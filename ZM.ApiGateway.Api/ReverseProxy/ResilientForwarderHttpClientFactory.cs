using Polly;
using System.Diagnostics;
using System.Net;
using Yarp.ReverseProxy.Forwarder;
using ZM.ApiGateway.Api.Resilience;

namespace ZM.ApiGateway.Api.ReverseProxy;

public sealed class ResilientForwarderHttpClientFactory
    : IForwarderHttpClientFactory
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilientForwarderHttpClientFactory([FromKeyedServices("gateway-retry")] ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _pipeline = pipeline;
    }

    public HttpMessageInvoker CreateClient(
        ForwarderHttpClientContext context)
    {
        var socketsHandler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            EnableMultipleHttp2Connections = true,
            ActivityHeadersPropagator =
                new ReverseProxyPropagator(
                    DistributedContextPropagator.Current),
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };

        var resilienceHandler = new ResilienceHandler(
            _pipeline,
            socketsHandler);

        return new HttpMessageInvoker(
            resilienceHandler,
            disposeHandler: true);
    }
}
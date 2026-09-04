using Polly;

namespace ZM.ApiGateway.Api.Resilience;

public sealed class ResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public ResilienceHandler(ResiliencePipeline<HttpResponseMessage> pipeline, HttpMessageHandler innerHandler) : base(innerHandler)
    {
        _pipeline = pipeline;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get &&
            request.Method != HttpMethod.Head)
        {
            return base.SendAsync(request, cancellationToken);
        }

        return ExecuteAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> ExecuteAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _pipeline.ExecuteAsync(
            async token =>
                await base.SendAsync(request, token),
            cancellationToken);
    }
}
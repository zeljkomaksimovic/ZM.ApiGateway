using System.Net;
using Polly;
using Polly.Retry;

public static class ResilienceExtensions
{
    public static IServiceCollection AddGatewayResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(
            "gateway-retry",
            static builder =>
            {
                builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromMilliseconds(200),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,

                    ShouldHandle = args => args.Outcome switch
                    {
                        { Exception: HttpRequestException } =>
                            PredicateResult.True(),

                        { Result.StatusCode: HttpStatusCode.BadGateway } =>
                            PredicateResult.True(),

                        { Result.StatusCode: HttpStatusCode.ServiceUnavailable } =>
                            PredicateResult.True(),

                        { Result.StatusCode: HttpStatusCode.GatewayTimeout } =>
                            PredicateResult.True(),

                        _ => PredicateResult.False()
                    }
                });
            });

        return services;
    }
}
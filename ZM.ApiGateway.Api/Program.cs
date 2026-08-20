using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ZM.ApiGateway.Api.Extensions;
using ZM.ApiGateway.Api.HealthChecks;
using ZM.ApiGateway.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGatewayServices(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false});
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapReverseProxy(proxyPipeline => 
{
    proxyPipeline.UseMiddleware<RateLimitingMiddleware>();
});

app.Run();

public partial class Program { }

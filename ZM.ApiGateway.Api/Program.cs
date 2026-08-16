using ZM.ApiGateway.Api.Extensions;
using ZM.ApiGateway.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddGatewayServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.MapReverseProxy(proxyPipeline => 
{
    proxyPipeline.UseMiddleware<RateLimitingMiddleware>();
});

app.Run();

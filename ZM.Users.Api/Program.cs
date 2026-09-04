var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Stable ids so they can be quoted in ZM.Users.Api.http and referenced by ZM.Orders.Api's seed data.
var users = new List<User>
{
    new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Ada Lovelace", "ada@zm.example"),
    new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Grace Hopper", "grace@zm.example"),
    new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Alan Turing", "alan@zm.example")
};

app.MapGet("/api/users", () => users)
    .WithName("GetUsers");

app.MapGet("/api/users/{id:guid}", (Guid id) =>
    users.FirstOrDefault(x => x.Id == id) is { } user
        ? Results.Ok(user)
        : Results.NotFound())
    .WithName("GetUserById");

app.Run();

internal record User(Guid Id, string Name, string Email);

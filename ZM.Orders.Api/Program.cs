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

// UserIds match ZM.Users.Api's seed data so a user can be followed through to their orders.
var orders = new List<Order>
{
    new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Shipped",
        129.99m,
        new DateOnly(2026, 7, 14)),
    new(
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "Pending",
        49.50m,
        new DateOnly(2026, 8, 2)),
    new(
        Guid.Parse("10000000-0000-0000-0000-000000000003"),
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Cancelled",
        310.00m,
        new DateOnly(2026, 8, 17))
};

app.MapGet("/api/orders", () => orders)
    .WithName("GetOrders");

app.MapGet("/api/orders/{id:guid}", (Guid id) =>
    orders.FirstOrDefault(x => x.Id == id) is { } order
        ? Results.Ok(order)
        : Results.NotFound())
    .WithName("GetOrderById");

app.Run();

internal record Order(Guid Id, Guid UserId, string Status, decimal Total, DateOnly PlacedOn);

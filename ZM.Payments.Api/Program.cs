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

// OrderIds match ZM.Orders.Api's seed data so an order can be followed through to its payment.
var payments = new List<Payment>
{
    new(
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "Settled",
        129.99m,
        "Card"),
    new(
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        "Authorized",
        49.50m,
        "Card"),
    new(
        Guid.Parse("20000000-0000-0000-0000-000000000003"),
        Guid.Parse("10000000-0000-0000-0000-000000000003"),
        "Refunded",
        310.00m,
        "BankTransfer")
};

app.MapGet("/api/payments", () => payments)
    .WithName("GetPayments");

app.MapGet("/api/payments/{id:guid}", (Guid id) =>
    payments.FirstOrDefault(x => x.Id == id) is { } payment
        ? Results.Ok(payment)
        : Results.NotFound())
    .WithName("GetPaymentById");

app.Run();

internal record Payment(Guid Id, Guid OrderId, string Status, decimal Amount, string Method);

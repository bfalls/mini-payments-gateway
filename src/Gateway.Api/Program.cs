using Gateway.Api.Middleware;
using Gateway.Api.Modules;
using Gateway.Data;
using Microsoft.EntityFrameworkCore;

var b = WebApplication.CreateBuilder(args);

// EF Core - InMemory provider for quick local runs
b.Services.AddDbContext<GatewayDbContext>(opt => opt.UseInMemoryDatabase("payments"));

// Swagger
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen();

var app = b.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

// Idempotency guard wraps POST /payments/charge
app.UseMiddleware<IdempotencyMiddleware>();

// Payments endpoints
app.MapPaymentsEndpoints();

app.Run();

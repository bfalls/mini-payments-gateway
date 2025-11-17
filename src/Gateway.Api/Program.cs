using Gateway.Api.Middleware;
using Gateway.Api.Modules;
using Gateway.Data;
using Microsoft.EntityFrameworkCore;

var b = WebApplication.CreateBuilder(args);

// Database configuration: choose between Postgres and EF Core InMemory.
// InMemory is used when:
//  - UseInMemoryDb=true is set (env var or config), OR
//  - there is no Pg connection string configured.
var useInMemory = b.Configuration.GetValue<bool>("UseInMemoryDb")
                  || string.IsNullOrWhiteSpace(
b.Configuration.GetConnectionString("Pg"));

if (useInMemory)
{
    b.Services.AddDbContext<GatewayDbContext>(opt => opt.UseInMemoryDatabase("payments"));
}
else
{
    var cs = b.Configuration.GetConnectionString("Pg");
    b.Services.AddDbContext<GatewayDbContext>(opt => opt.UseNpgsql(cs));
}

// Swagger
b.Services.AddEndpointsApiExplorer();
b.Services.AddSwaggerGen();

var app = b.Build();

if (app.Configuration.GetValue<bool>("AutoMigrate") || app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch when (attempt < 5)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

// Idempotency guard wraps POST /payments/*
app.UseMiddleware<IdempotencyMiddleware>();

// Payments endpoints
app.MapPaymentsEndpoints();

app.Run();

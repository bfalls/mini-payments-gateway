using Gateway.Data;
using Gateway.Domain;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

var b = Host.CreateApplicationBuilder(args);

// DB (Postgres)
var cs = b.Configuration.GetConnectionString("Pg");
b.Services.AddDbContext<GatewayDbContext>(opt => opt.UseNpgsql(cs));

// HTTP client for PSP
b.Services.AddHttpClient("psp", c =>
{
    c.BaseAddress = new Uri(b.Configuration["Psp:BaseUrl"] ?? "http://localhost:5005");
});

// Background worker
b.Services.AddHostedService<OutboxWorker>();

await b.Build().RunAsync();

public sealed class OutboxWorker(ILogger<OutboxWorker> log, IServiceProvider sp, IConfiguration cfg)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(cfg.GetValue("Worker:PollIntervalSeconds", 2));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

                var msg = await db.OutboxMessages
                    .Where(o => !o.Dispatched)
                    .OrderBy(o => o.CreatedUtc)
                    .FirstOrDefaultAsync(ct);

                if (msg is null)
                {
                    await Task.Delay(delay, ct);
                    continue;
                }

                var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == msg.AggregateId, ct);
                if (payment is null)
                {
                    msg.Dispatched = true;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("psp");

                var sourceToken = System.Text.Json.JsonDocument
                    .Parse(msg.Payload).RootElement.GetProperty("sourceToken").GetString() ?? "";

                var req = new AuthRequest(payment.Id, payment.Amount, payment.Currency, sourceToken);
                var resp = await http.PostAsJsonAsync("/psp/authorize", req, ct);
                var body = await resp.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);

                if (body is { Authorized: true, AuthCode: not null })
                    payment.MarkAuthorized(body.AuthCode);
                else if (body is { Authorized: false })
                    payment.MarkDeclined();
                else
                    payment.MarkError();

                msg.Dispatched = true;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Worker loop error");
                await Task.Delay(delay, ct);
            }
        }
    }
}

public record AuthRequest(Guid PaymentId, long Amount, string Currency, string SourceToken);
public record AuthResponse(bool Authorized, string? AuthCode, string? Reason);

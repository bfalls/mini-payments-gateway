using Gateway.Data;
using Gateway.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;

var b = Host.CreateApplicationBuilder(args);

var pgHost = Environment.GetEnvironmentVariable("WORKER_PG__HOST") ?? "localhost";
var pgPort = Environment.GetEnvironmentVariable("WORKER_PG__PORT") ?? "5432";
var pgDb = Environment.GetEnvironmentVariable("WORKER_PG__DATABASE") ?? "gateway";
var pgUser = Environment.GetEnvironmentVariable("WORKER_PG__USERNAME") ?? "postgres";
var pgPassword = Environment.GetEnvironmentVariable("WORKER_PG__PASSWORD") ?? "postgres";

// DB (Postgres)
var connectionString =
    $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";
var explicitConnectionString =
    Environment.GetEnvironmentVariable("WORKER_PG__CONNECTIONSTRING");
var cs =
    explicitConnectionString
    ?? b.Configuration.GetConnectionString("Pg")
    ?? connectionString;
b.Services.AddDbContext<GatewayDbContext>(opt => opt.UseNpgsql(cs));

// HTTP client for PSP
var pspBaseUrl =
    Environment.GetEnvironmentVariable("WORKER_PSP__BASEURL")
    ?? b.Configuration["Psp:BaseUrl"]
    ?? "http://localhost:5279";
b.Services.AddHttpClient("psp", c =>
{
    c.BaseAddress = new Uri(pspBaseUrl);
});

// Background worker
b.Services.AddHostedService<OutboxWorker>();

await b.Build().RunAsync();

public sealed class OutboxWorker(ILogger<OutboxWorker> log, IServiceProvider sp, IConfiguration cfg)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var pollSeconds = cfg.GetValue<int?>("Worker:PollIntervalSeconds") ?? 2;
        var delay = TimeSpan.FromSeconds(pollSeconds);
        
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

                if (string.Equals(msg.Type, "Authorize", StringComparison.OrdinalIgnoreCase))
                {
                    var http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("psp");

                    var sourceToken = JsonDocument
                        .Parse(msg.Payload).RootElement.GetProperty("sourceToken").GetString() ?? string.Empty;

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
                    continue;
                }

                if (string.Equals(msg.Type, "CryptoConfirm", StringComparison.OrdinalIgnoreCase))
                {
                    var root = JsonDocument.Parse(msg.Payload).RootElement;
                    var txHash = root.TryGetProperty("txHash", out var txProp) ? txProp.GetString() : null;

                    var cryptoTxQuery = db.CryptoTransactions.AsQueryable().Where(c => c.PaymentId == payment.Id);
                    if (!string.IsNullOrWhiteSpace(txHash))
                    {
                        cryptoTxQuery = cryptoTxQuery.Where(c => c.TxHash == txHash);
                    }

                    var cryptoTx = await cryptoTxQuery.FirstOrDefaultAsync(ct);
                    if (cryptoTx is null)
                    {
                        msg.Dispatched = true;
                        await db.SaveChangesAsync(ct);
                        continue;
                    }

                    cryptoTx.MarkConfirmed(3);
                    payment.MarkAuthorized(cryptoTx.TxHash);
                    msg.Dispatched = true;
                    await db.SaveChangesAsync(ct);
                    continue;
                }

                // Unknown types shouldn't block the queue
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

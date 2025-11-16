using Gateway.Data;
using Gateway.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Api.Modules;

// I'll put in some useful comments soon.

public static class PaymentsModule
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /payments/charge
        app.MapPost("/payments/charge", async (ChargeRequest req, GatewayDbContext db, HttpContext http) =>
        {
            if (http.Request.Headers["x-api-key"] != http.RequestServices.GetRequiredService<IConfiguration>()["ApiKey"])
                return Results.Unauthorized();

            var payment = Payment.Create(req.Amount, req.Currency, req.MerchantRef);
            db.Payments.Add(payment);

            // outbox message placeholder
            db.OutboxMessages.Add(OutboxMessage.ForPaymentAuth(payment.Id, req.SourceToken));

            await db.SaveChangesAsync();
            return Results.Accepted($"/payments/{payment.Id}",
                new { paymentId = payment.Id, status = payment.Status.ToString() });
        })
        .WithName("ChargePayment");

        // POST /payments/crypto-charge
        app.MapPost("/payments/crypto-charge", async (CryptoChargeRequest req, GatewayDbContext db, HttpContext http) =>
        {
            if (http.Request.Headers["x-api-key"] != http.RequestServices.GetRequiredService<IConfiguration>()["ApiKey"])
                return Results.Unauthorized();

            var currency = $"CRYPTO-{req.CryptoCurrency}";
            var payment = Payment.Create(req.Amount, currency, req.MerchantRef);
            var txHash = $"0x{Guid.NewGuid():N}";
            var cryptoTx = CryptoTransaction.Create(payment.Id, req.CryptoCurrency, req.Network, req.FromWallet, txHash);

            db.Payments.Add(payment);
            db.CryptoTransactions.Add(cryptoTx);
            db.OutboxMessages.Add(OutboxMessage.ForCryptoConfirm(payment.Id, txHash, req.Network));

            await db.SaveChangesAsync();
            return Results.Accepted($"/payments/{payment.Id}",
                new { paymentId = payment.Id, status = payment.Status.ToString(), txHash, network = req.Network });
        })
        .WithName("CryptoChargePayment");

        // GET /payments/{id}
        app.MapGet("/payments/{id:guid}", async (Guid id, GatewayDbContext db, HttpContext http) =>
        {
            if (http.Request.Headers["x-api-key"] != http.RequestServices.GetRequiredService<IConfiguration>()["ApiKey"])
                return Results.Unauthorized();

            var p = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();

            var view = new PaymentView(p.Id, p.Status, p.Amount, p.Currency, p.MerchantRef, p.AuthCode);
            return Results.Ok(view);
        })
        .WithName("GetPayment");

        // GET /payments/{id}/crypto
        app.MapGet("/payments/{id:guid}/crypto", async (Guid id, GatewayDbContext db, HttpContext http) =>
        {
            if (http.Request.Headers["x-api-key"] != http.RequestServices.GetRequiredService<IConfiguration>()["ApiKey"])
                return Results.Unauthorized();

            var tx = await db.CryptoTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == id);
            if (tx is null) return Results.NotFound();

            var view = new CryptoTransactionView(tx.PaymentId, tx.CryptoCurrency, tx.Network, tx.FromWallet, tx.TxHash,
                tx.Status, tx.Confirmations, tx.CreatedUtc, tx.ConfirmedUtc);
            return Results.Ok(view);
        })
        .WithName("GetCryptoTransaction");

        return app;
    }
}

using Gateway.Data;
using Gateway.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Api.Modules;

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

            // outbox message placeholder (worker uses later)
            db.OutboxMessages.Add(OutboxMessage.ForPaymentAuth(payment.Id, req.SourceToken));

            await db.SaveChangesAsync();
            return Results.Accepted($"/payments/{payment.Id}",
                new { paymentId = payment.Id, status = payment.Status.ToString() });
        })
        .WithName("ChargePayment");

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

        return app;
    }
}

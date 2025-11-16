using System.Text.Json;

namespace Gateway.Data;

/*
 Transactional Outbox Pattern.
 Payments write their intent (Authorize) into the DB in the same transaction.
 A background worker later reads and dispatches to the PSP, marking Dispatched=true.
 This eliminates double-charge and lost-request classes of bugs when APIs or
 networks hiccup, and gives durable, retryable integration points.
 */
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Aggregate this message is about (here: Payment.Id)
    public Guid AggregateId { get; set; }

    // e.g., "Authorize"
    public string Type { get; set; } = "Authorize";

    // JSON payload to send to PSP
    public string Payload { get; set; } = "{}";

    // false until successfully dispatched
    public bool Dispatched { get; set; } = false;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public static OutboxMessage ForPaymentAuth(Guid paymentId, string sourceToken)
        => new()
        {
            AggregateId = paymentId,
            Type = "Authorize",
            Payload = $"{{\"sourceToken\":\"{sourceToken}\"}}"
        };

    public static OutboxMessage ForCryptoConfirm(Guid paymentId, string txHash, string network)
        => new()
        {
            AggregateId = paymentId,
            Type = "CryptoConfirm",
            Payload = JsonSerializer.Serialize(new { txHash, network })
        };
}


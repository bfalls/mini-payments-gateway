namespace Gateway.Data;

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
}

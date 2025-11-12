namespace Gateway.Domain;

public sealed class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    // I used `long` to store the smallest currency unit (cents) to avoid `floats`
    public long Amount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public string MerchantRef { get; private set; } = string.Empty;

    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? AuthCode { get; private set; }

    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; private set; } = DateTime.UtcNow;

    // for serializers/EF later
    private Payment() { }

    public static Payment Create(long amount, string currency, string merchantRef)
        => new()
        {
            Amount = amount,
            Currency = currency,
            MerchantRef = merchantRef
        };

    public void MarkAuthorized(string authCode)
    {
        Status = PaymentStatus.Authorized;
        AuthCode = authCode;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void MarkDeclined()
    {
        Status = PaymentStatus.Declined;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void MarkError()
    {
        Status = PaymentStatus.Error;
        UpdatedUtc = DateTime.UtcNow;
    }
}

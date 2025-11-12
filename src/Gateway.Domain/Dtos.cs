namespace Gateway.Domain;

// Input to POST /payments/charge
public record ChargeRequest(long Amount, string Currency, string SourceToken, string MerchantRef);

// Output from GET /payments/{id}
public record PaymentView(
    Guid PaymentId,
    PaymentStatus Status,
    long Amount,
    string Currency,
    string MerchantRef,
    string? AuthCode
);

namespace Gateway.Domain;

// Input to POST /payments/charge
public record ChargeRequest(long Amount, string Currency, string SourceToken, string MerchantRef);

// Input to POST /payments/crypto-charge
public record CryptoChargeRequest(
    long Amount,
    string CryptoCurrency,
    string Network,
    string FromWallet,
    string MerchantRef);

// Output from GET /payments/{id}
public record PaymentView(
    Guid PaymentId,
    PaymentStatus Status,
    long Amount,
    string Currency,
    string MerchantRef,
    string? AuthCode
);

// Output for crypto transaction status lookups
public record CryptoTransactionView(
    Guid PaymentId,
    string CryptoCurrency,
    string Network,
    string FromWallet,
    string TxHash,
    CryptoTransactionStatus Status,
    int Confirmations,
    DateTime CreatedUtc,
    DateTime? ConfirmedUtc
);

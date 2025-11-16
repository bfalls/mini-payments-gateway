namespace Gateway.Domain;

public sealed class CryptoTransaction
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PaymentId { get; private set; }
    public string CryptoCurrency { get; private set; } = string.Empty;
    public string Network { get; private set; } = string.Empty;
    public string FromWallet { get; private set; } = string.Empty;
    public string TxHash { get; private set; } = string.Empty;
    public CryptoTransactionStatus Status { get; private set; } = CryptoTransactionStatus.Pending;
    public int Confirmations { get; private set; }
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ConfirmedUtc { get; private set; }

    private CryptoTransaction() { }

    public static CryptoTransaction Create(
        Guid paymentId,
        string cryptoCurrency,
        string network,
        string fromWallet,
        string txHash)
        => new()
        {
            PaymentId = paymentId,
            CryptoCurrency = cryptoCurrency,
            Network = network,
            FromWallet = fromWallet,
            TxHash = txHash
        };

    public void MarkConfirmed(int confirmations)
    {
        Status = CryptoTransactionStatus.Confirmed;
        Confirmations = confirmations;
        ConfirmedUtc = DateTime.UtcNow;
    }
}

public enum CryptoTransactionStatus
{
    Pending = 0,
    Confirmed = 1
}

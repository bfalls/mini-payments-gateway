namespace Gateway.Data;

public sealed class IdempotencyRecord
{
    // Derived, canonicalized idempotency key (SHA-256 of normalized payload)
    public string Key { get; set; } = string.Empty;

    // Duplicate column kept for existing schema; stores the same derived hash
    public string BodyHash { get; set; } = string.Empty;

    // Canonical response to replay on duplicates
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = "{}";

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public IdempotencyRecord() { }

    public IdempotencyRecord(string key, string bodyHash, int statusCode, string responseBody)
    {
        Key = key;
        BodyHash = bodyHash;
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gateway.Domain;
using Microsoft.AspNetCore.Http;

namespace Gateway.Api.Idempotency;

public static class IdempotencyKeyDeriver
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static (string derivedKey, string canonicalJson) FromChargeRequest(ChargeRequest request)
    {
        var canonical = Canonicalize(request);
        var json = SerializeCanonical(canonical);
        return (ComputeHash(json), json);
    }

    public static (string derivedKey, string canonicalJson) FromCryptoChargeRequest(CryptoChargeRequest request)
    {
        var canonical = Canonicalize(request);
        var json = SerializeCanonical(canonical);
        return (ComputeHash(json), json);
    }

    public static bool TryDerive(PathString path, string body, out string derivedKey, out string canonicalJson)
    {
        derivedKey = string.Empty;
        canonicalJson = string.Empty;

        if (path.StartsWithSegments("/payments/charge", StringComparison.OrdinalIgnoreCase))
        {
            var charge = JsonSerializer.Deserialize<ChargeRequest>(body, DeserializeOptions);
            if (charge is null) return false;

            (derivedKey, canonicalJson) = FromChargeRequest(charge);
            return true;
        }

        if (path.StartsWithSegments("/payments/crypto-charge", StringComparison.OrdinalIgnoreCase))
        {
            var crypto = JsonSerializer.Deserialize<CryptoChargeRequest>(body, DeserializeOptions);
            if (crypto is null) return false;

            (derivedKey, canonicalJson) = FromCryptoChargeRequest(crypto);
            return true;
        }

        return false;
    }

    private static CanonicalPaymentPayload Canonicalize(ChargeRequest request)
    {
        return new CanonicalPaymentPayload(
            Type: "fiat",
            Amount: request.Amount,
            MerchantRef: Normalize(request.MerchantRef),
            Currency: NormalizeToUpper(request.Currency),
            SourceToken: Normalize(request.SourceToken),
            CryptoCurrency: null,
            Network: null,
            FromWallet: null
        );
    }

    private static CanonicalPaymentPayload Canonicalize(CryptoChargeRequest request)
    {
        return new CanonicalPaymentPayload(
            Type: "crypto",
            Amount: request.Amount,
            MerchantRef: Normalize(request.MerchantRef),
            Currency: null,
            SourceToken: null,
            CryptoCurrency: NormalizeToUpper(request.CryptoCurrency),
            Network: NormalizeToUpper(request.Network),
            FromWallet: Normalize(request.FromWallet)
        );
    }

    private static string SerializeCanonical(CanonicalPaymentPayload canonical)
    {
        var map = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = canonical.Type,
            ["amount"] = canonical.Amount,
            ["merchantRef"] = canonical.MerchantRef
        };

        AddIfNotNull(map, "currency", canonical.Currency);
        AddIfNotNull(map, "sourceToken", canonical.SourceToken);
        AddIfNotNull(map, "cryptoCurrency", canonical.CryptoCurrency);
        AddIfNotNull(map, "network", canonical.Network);
        AddIfNotNull(map, "fromWallet", canonical.FromWallet);

        return JsonSerializer.Serialize(map, SerializeOptions);
    }

    private static void AddIfNotNull(IDictionary<string, object?> map, string key, string? value)
    {
        if (value is not null)
        {
            map[key] = value;
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return Regex.Replace(trimmed, "\\s+", " ");
    }

    private static string NormalizeToUpper(string value) => Normalize(value).ToUpperInvariant();

    private static string ComputeHash(string canonicalJson)
    {
        var bytes = Encoding.UTF8.GetBytes(canonicalJson);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

public sealed record CanonicalPaymentPayload(
    string Type,
    long Amount,
    string MerchantRef,
    string? Currency,
    string? SourceToken,
    string? CryptoCurrency,
    string? Network,
    string? FromWallet
);

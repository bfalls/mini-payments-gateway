var b = WebApplication.CreateBuilder(args);
var app = b.Build();

app.MapPost("/psp/authorize", (AuthRequest req) =>
{
    if (req.SourceToken.Contains("decline", StringComparison.OrdinalIgnoreCase))
        return Results.Ok(new AuthResponse(false, null, "DECLINED"));

    var authCode = "AUTH-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    return Results.Ok(new AuthResponse(true, authCode, null));
})
.WithName("Authorize");

app.Run();

public record AuthRequest(Guid PaymentId, long Amount, string Currency, string SourceToken);
public record AuthResponse(bool Authorized, string? AuthCode, string? Reason);

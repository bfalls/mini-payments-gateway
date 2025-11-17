using Gateway.Api.Idempotency;
using Gateway.Data;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;

namespace Gateway.Api.Middleware;

public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    private static readonly PathString[] GuardedPaths =
    [
        new PathString("/payments/charge"),
        new PathString("/payments/crypto-charge")
    ];

    public async Task Invoke(HttpContext ctx, GatewayDbContext db)
    {
        // Only guard specific POST /payments/* endpoints
        if (ctx.Request.Method != HttpMethods.Post ||
            !GuardedPaths.Any(p => ctx.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await next(ctx);
            return;
        }

        // Read body (and reset stream so the endpoint can read it too)
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        if (!IdempotencyKeyDeriver.TryDerive(ctx.Request.Path, body, out var derivedKey, out _))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Unable to derive idempotency key for this request.");
            return;
        }

        var clientKey = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (!string.IsNullOrWhiteSpace(clientKey) && !string.Equals(clientKey, derivedKey, StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Idempotency-Key header does not match the derived key for this payload.");
            return;
        }

        // If we’ve seen this derived key, short-circuit with the canonical response
        var existing = await db.IdempotencyRecords.FindAsync(derivedKey, derivedKey);
        if (existing is not null)
        {
            ctx.Response.StatusCode = existing.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        // Capture downstream response so we can persist it
        var original = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        await next(ctx); // let the endpoint run

        buffer.Position = 0;
        var responseBody = await new StreamReader(buffer).ReadToEndAsync();

        buffer.Position = 0;
        await buffer.CopyToAsync(original);
        ctx.Response.Body = original;

        db.IdempotencyRecords.Add(new IdempotencyRecord(derivedKey, derivedKey, ctx.Response.StatusCode, responseBody));
        await db.SaveChangesAsync();
    }
}

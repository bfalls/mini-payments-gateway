using System.Security.Cryptography;
using System.Text;
using Gateway.Data;
using Microsoft.AspNetCore.Http;
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

        var key = ctx.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsync("Missing Idempotency-Key");
            return;
        }

        // Read body (and reset stream so the endpoint can read it too)
        ctx.Request.EnableBuffering();
        using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        ctx.Request.Body.Position = 0;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));

        // If we’ve seen this key+body, short-circuit with the canonical response
        var existing = await db.IdempotencyRecords.FindAsync(key, hash);
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

        db.IdempotencyRecords.Add(new IdempotencyRecord(key, hash, ctx.Response.StatusCode, responseBody));
        await db.SaveChangesAsync();
    }
}

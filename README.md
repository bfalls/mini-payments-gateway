# Mini Payments Gateway (C# / .NET 8)
Idempotent payments API with Outbox pattern, worker dispatch, and a PSP stub. Built to demo reliability patterns for high-volume payments.

## Projects
- Gateway.Api — Minimal API (+ Swagger)
- Gateway.Domain — Entities/DTOs
- Gateway.Data — EF Core DbContext (+ Outbox/Idempotency)
- Gateway.Worker — Background dispatcher
- PspStub.Api — Fake PSP for auth/chaos

## Quick start
dotnet build

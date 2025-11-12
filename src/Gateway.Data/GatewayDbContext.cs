using Gateway.Domain;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Gateway.Data;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Payments
        b.Entity<Payment>().HasKey(p => p.Id);
        b.Entity<Payment>().Property(p => p.Currency).HasMaxLength(10).IsRequired();
        b.Entity<Payment>().Property(p => p.MerchantRef).HasMaxLength(128).IsRequired();
        b.Entity<Payment>().HasIndex(p => p.CreatedUtc);

        // Outbox
        b.Entity<OutboxMessage>().HasKey(o => o.Id);
        b.Entity<OutboxMessage>().Property(o => o.Type).HasMaxLength(64).IsRequired();
        b.Entity<OutboxMessage>().Property(o => o.Payload).IsRequired();
        b.Entity<OutboxMessage>().HasIndex(o => new { o.Dispatched, o.CreatedUtc });

        // Idempotency (composite key)
        b.Entity<IdempotencyRecord>().HasKey(i => new { i.Key, i.BodyHash });
        b.Entity<IdempotencyRecord>().Property(i => i.Key).HasMaxLength(128).IsRequired();
        b.Entity<IdempotencyRecord>().Property(i => i.BodyHash).HasMaxLength(64).IsRequired();
        b.Entity<IdempotencyRecord>().Property(i => i.ResponseBody).IsRequired();
        b.Entity<IdempotencyRecord>().HasIndex(i => i.CreatedUtc);
    }
}

using Gateway.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gateway.Data;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options) : DbContext(options)
{
    // Make the model visible to EF
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("public");

        // Payments
        modelBuilder.Entity<Gateway.Domain.Payment>(e =>
        {
            e.ToTable("Payments");
            e.HasKey(p => p.Id);
            e.Property(p => p.Currency).HasMaxLength(3).IsRequired();
            e.Property(p => p.MerchantRef).HasMaxLength(200);
            e.Property(p => p.Status).IsRequired();
            e.Property(p => p.Amount).IsRequired();
            e.Property(p => p.CreatedUtc).IsRequired();
            e.Property(p => p.UpdatedUtc).IsRequired();
            e.HasIndex(p => p.MerchantRef);
        });

        // Outbox
        modelBuilder.Entity<Gateway.Data.OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(o => o.Id);
            e.Property(o => o.Type).HasMaxLength(100).IsRequired();
            e.Property(o => o.Payload).IsRequired();
            e.Property(o => o.CreatedUtc).IsRequired();
            e.Property(o => o.Dispatched).IsRequired();
            e.HasIndex(o => new { o.Dispatched, o.CreatedUtc });
        });

        // Idempotency (composite PK)
        modelBuilder.Entity<Gateway.Data.IdempotencyRecord>(e =>
        {
            e.ToTable("IdempotencyRecords");
            e.HasKey(i => new { i.Key, i.BodyHash });
            e.Property(i => i.Key).HasMaxLength(200).IsRequired();
            e.Property(i => i.BodyHash).HasMaxLength(64).IsRequired(); // SHA-256 hex
            e.Property(i => i.StatusCode).IsRequired();
            e.Property(i => i.ResponseBody).IsRequired();
            e.Property(i => i.CreatedUtc).IsRequired();
        });
    }
}

using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Models;

namespace TenantCatalogService.Data;

public class TenantCatalogDbContext : DbContext
{
    public TenantCatalogDbContext(DbContextOptions<TenantCatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<ResourceUsage> ResourceUsages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.OwnsOne(e => e.DbConfig, dbConfig =>
            {
                dbConfig.Property(d => d.Mode).IsRequired().HasMaxLength(20);
                dbConfig.Property(d => d.Server).IsRequired().HasMaxLength(200);
                dbConfig.Property(d => d.Database).IsRequired().HasMaxLength(100);
                dbConfig.Property(d => d.Schema).HasMaxLength(100);
                dbConfig.Property(d => d.CredentialRef).IsRequired().HasMaxLength(200);
            });
            
            entity.OwnsOne(e => e.Throttling, throttling =>
            {
                throttling.Property(t => t.Rps).IsRequired();
            });
            
            entity.OwnsOne(e => e.Slo, slo =>
            {
                slo.Property(s => s.Availability).HasMaxLength(20);
                slo.Property(s => s.P95LatencyMs).IsRequired();
            });
            
            entity.HasIndex(e => e.Status);
        });
        
        modelBuilder.Entity<ResourceUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.CpuCores).IsRequired();
            entity.Property(e => e.MemoryGb).IsRequired();
            entity.Property(e => e.StorageGb).IsRequired();
            entity.Property(e => e.NetworkGb).IsRequired();
            
            entity.HasIndex(e => new { e.TenantId, e.Timestamp });
        });
    }
}

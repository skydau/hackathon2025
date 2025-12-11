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
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            // 配置枚举存储为字符串
            entity.Property(e => e.Status)
                  .IsRequired()
                  .HasMaxLength(50)
                  .HasConversion<string>();
            
            // 数据库配置字段
            entity.Property(e => e.DbServer).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DbDatabase).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DbUsername).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DbPasswordRef).IsRequired().HasMaxLength(200);
            
            // 限流配置字段
            entity.Property(e => e.ThrottlingRps);
            
            // SLO配置字段
            entity.Property(e => e.SloAvailability).HasMaxLength(20);
            entity.Property(e => e.SloP95LatencyMs);
            
            // 时间戳字段
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            // 索引
            entity.HasIndex(e => e.Status);
        });
        
        modelBuilder.Entity<ResourceUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CpuUsage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.MemoryUsageMB).HasColumnType("decimal(18,4)");
            entity.Property(e => e.StorageUsageGB).HasColumnType("decimal(18,4)");
            entity.Property(e => e.NetworkUsageGB).HasColumnType("decimal(18,4)");
            entity.Property(e => e.RecordedAt).IsRequired();
            
            // 索引
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.RecordedAt);
            
            // 外键关系
            entity.HasOne<Tenant>()
                  .WithMany()
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

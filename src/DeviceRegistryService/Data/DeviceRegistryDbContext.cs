using Microsoft.EntityFrameworkCore;
using DeviceRegistryService.Models;

namespace DeviceRegistryService.Data;

public class DeviceRegistryDbContext : DbContext
{
    public DeviceRegistryDbContext(DbContextOptions<DeviceRegistryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(e => e.SerialNumber);
            entity.Property(e => e.SerialNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DeviceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RegisteredAt).IsRequired();
            
            entity.HasIndex(e => e.TenantId);
        });
    }
}

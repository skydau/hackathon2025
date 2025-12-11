using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using Xunit;

namespace TenantCatalogService.Tests;

/// <summary>
/// 测试 TenantStatus 枚举的数据库映射
/// </summary>
public class TenantStatusMappingTests : IDisposable
{
    private readonly TenantCatalogDbContext _context;
    private readonly string _dbName;

    public TenantStatusMappingTests()
    {
        _dbName = $"TenantStatusTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new TenantCatalogDbContext(options);
    }

    [Fact]
    public async Task TenantStatus_ShouldBeStoredAsString()
    {
        // Arrange - 准备测试数据，测试所有枚举值
        var tenants = new[]
        {
            new Tenant
            {
                Id = "tenant-provisioning",
                DisplayName = "配置中的租户",
                Status = TenantStatus.Provisioning,
                DbServer = "server1.com",
                DbDatabase = "DB1",
                DbUsername = "user1",
                DbPasswordRef = "ref1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "tenant-enabled",
                DisplayName = "已启用的租户",
                Status = TenantStatus.Enabled,
                DbServer = "server2.com",
                DbDatabase = "DB2",
                DbUsername = "user2",
                DbPasswordRef = "ref2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "tenant-disabled",
                DisplayName = "已禁用的租户",
                Status = TenantStatus.Disabled,
                DbServer = "server3.com",
                DbDatabase = "DB3",
                DbUsername = "user3",
                DbPasswordRef = "ref3",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "tenant-decommissioned",
                DisplayName = "已停用的租户",
                Status = TenantStatus.Decommissioned,
                DbServer = "server4.com",
                DbDatabase = "DB4",
                DbUsername = "user4",
                DbPasswordRef = "ref4",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        // Act - 保存所有租户
        _context.Tenants.AddRange(tenants);
        await _context.SaveChangesAsync();

        // 重新查询所有租户
        var retrievedTenants = await _context.Tenants.ToListAsync();

        // Assert - 验证所有状态都正确保存和读取
        Assert.Equal(4, retrievedTenants.Count);
        
        var provisioningTenant = retrievedTenants.First(t => t.Id == "tenant-provisioning");
        Assert.Equal(TenantStatus.Provisioning, provisioningTenant.Status);
        
        var enabledTenant = retrievedTenants.First(t => t.Id == "tenant-enabled");
        Assert.Equal(TenantStatus.Enabled, enabledTenant.Status);
        
        var disabledTenant = retrievedTenants.First(t => t.Id == "tenant-disabled");
        Assert.Equal(TenantStatus.Disabled, disabledTenant.Status);
        
        var decommissionedTenant = retrievedTenants.First(t => t.Id == "tenant-decommissioned");
        Assert.Equal(TenantStatus.Decommissioned, decommissionedTenant.Status);
    }

    [Fact]
    public async Task TenantStatus_QueryByStatus_ShouldWork()
    {
        // Arrange - 准备测试数据
        var tenants = new[]
        {
            new Tenant
            {
                Id = "enabled-1",
                DisplayName = "启用租户1",
                Status = TenantStatus.Enabled,
                DbServer = "server1.com",
                DbDatabase = "DB1",
                DbUsername = "user1",
                DbPasswordRef = "ref1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "enabled-2",
                DisplayName = "启用租户2",
                Status = TenantStatus.Enabled,
                DbServer = "server2.com",
                DbDatabase = "DB2",
                DbUsername = "user2",
                DbPasswordRef = "ref2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "disabled-1",
                DisplayName = "禁用租户1",
                Status = TenantStatus.Disabled,
                DbServer = "server3.com",
                DbDatabase = "DB3",
                DbUsername = "user3",
                DbPasswordRef = "ref3",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _context.Tenants.AddRange(tenants);
        await _context.SaveChangesAsync();

        // Act - 按状态查询
        var enabledTenants = await _context.Tenants
            .Where(t => t.Status == TenantStatus.Enabled)
            .ToListAsync();

        var disabledTenants = await _context.Tenants
            .Where(t => t.Status == TenantStatus.Disabled)
            .ToListAsync();

        // Assert - 验证查询结果
        Assert.Equal(2, enabledTenants.Count);
        Assert.All(enabledTenants, t => Assert.Equal(TenantStatus.Enabled, t.Status));
        
        Assert.Single(disabledTenants);
        Assert.Equal(TenantStatus.Disabled, disabledTenants.First().Status);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
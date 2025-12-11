using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using Xunit;

namespace TenantCatalogService.Tests;

/// <summary>
/// 测试资源使用模型的字段映射和计算属性
/// </summary>
public class ResourceUsageModelTests : IDisposable
{
    private readonly TenantCatalogDbContext _context;
    private readonly string _dbName;

    public ResourceUsageModelTests()
    {
        _dbName = $"TestResourceUsageDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new TenantCatalogDbContext(options);
    }

    [Fact]
    public void ResourceUsage_DatabaseFields_MapCorrectly()
    {
        // Arrange - 准备测试数据
        var resourceUsage = new ResourceUsage
        {
            TenantId = "test-tenant-001",
            CpuUsage = 2.5m,
            MemoryUsageMB = 4096m,
            StorageUsageGB = 100.5m,
            NetworkUsageGB = 10.25m,
            RecordedAt = DateTime.UtcNow
        };

        // Act - 保存到数据库
        _context.ResourceUsages.Add(resourceUsage);
        _context.SaveChanges();

        // 从数据库重新读取
        var savedUsage = _context.ResourceUsages.First(r => r.TenantId == "test-tenant-001");

        // Assert - 验证数据库字段正确保存
        Assert.Equal("test-tenant-001", savedUsage.TenantId);
        Assert.Equal(2.5m, savedUsage.CpuUsage);
        Assert.Equal(4096m, savedUsage.MemoryUsageMB);
        Assert.Equal(100.5m, savedUsage.StorageUsageGB);
        Assert.Equal(10.25m, savedUsage.NetworkUsageGB);
        Assert.True(savedUsage.Id > 0); // 自增ID应该大于0
    }

    [Fact]
    public void ResourceUsage_ComputedProperties_WorkCorrectly()
    {
        // Arrange - 准备测试数据
        var resourceUsage = new ResourceUsage
        {
            TenantId = "test-tenant-002",
            CpuUsage = 4.0m,
            MemoryUsageMB = 8192m, // 8GB in MB
            StorageUsageGB = 250.75m,
            NetworkUsageGB = 5.5m,
            RecordedAt = DateTime.UtcNow
        };

        // Act & Assert - 验证计算属性
        Assert.Equal(resourceUsage.RecordedAt, resourceUsage.Timestamp);
        Assert.Equal(4.0, resourceUsage.CpuCores);
        Assert.Equal(8.0, resourceUsage.MemoryGb, 1); // 8192MB = 8GB，允许1位小数误差
        Assert.Equal(250.75, resourceUsage.StorageGb);
        Assert.Equal(5.5, resourceUsage.NetworkGb);
    }

    [Fact]
    public void ResourceUsage_ComputedProperties_SettersWork()
    {
        // Arrange - 准备测试数据
        var resourceUsage = new ResourceUsage
        {
            TenantId = "test-tenant-003"
        };

        // Act - 通过计算属性设置值
        resourceUsage.Timestamp = DateTime.UtcNow;
        resourceUsage.CpuCores = 6.5;
        resourceUsage.MemoryGb = 16.0; // 16GB
        resourceUsage.StorageGb = 500.25;
        resourceUsage.NetworkGb = 12.75;

        // Assert - 验证数据库字段被正确设置
        Assert.Equal(resourceUsage.Timestamp, resourceUsage.RecordedAt);
        Assert.Equal(6.5m, resourceUsage.CpuUsage);
        Assert.Equal(16384m, resourceUsage.MemoryUsageMB); // 16GB = 16384MB
        Assert.Equal(500.25m, resourceUsage.StorageUsageGB);
        Assert.Equal(12.75m, resourceUsage.NetworkUsageGB);
    }

    [Fact]
    public void ResourceUsage_NullValues_HandleCorrectly()
    {
        // Arrange - 准备测试数据（可选字段为空）
        var resourceUsage = new ResourceUsage
        {
            TenantId = "test-tenant-004",
            RecordedAt = DateTime.UtcNow
            // 其他字段为 null
        };

        // Act - 保存到数据库
        _context.ResourceUsages.Add(resourceUsage);
        _context.SaveChanges();

        // 从数据库重新读取
        var savedUsage = _context.ResourceUsages.First(r => r.TenantId == "test-tenant-004");

        // Assert - 验证 null 值处理
        Assert.Null(savedUsage.CpuUsage);
        Assert.Null(savedUsage.MemoryUsageMB);
        Assert.Null(savedUsage.StorageUsageGB);
        Assert.Null(savedUsage.NetworkUsageGB);

        // 验证计算属性正确处理 null 值
        Assert.Equal(0.0, savedUsage.CpuCores);
        Assert.Equal(0.0, savedUsage.MemoryGb);
        Assert.Equal(0.0, savedUsage.StorageGb);
        Assert.Equal(0.0, savedUsage.NetworkGb);
    }

    [Fact]
    public void ResourceUsage_MemoryConversion_IsAccurate()
    {
        // Arrange & Act - 测试内存单位转换的准确性
        var resourceUsage = new ResourceUsage();
        
        // 设置不同的GB值，验证MB转换
        var testCases = new[]
        {
            (gb: 1.0, expectedMb: 1024m),
            (gb: 0.5, expectedMb: 512m),
            (gb: 2.5, expectedMb: 2560m),
            (gb: 16.0, expectedMb: 16384m)
        };

        foreach (var (gb, expectedMb) in testCases)
        {
            // Act
            resourceUsage.MemoryGb = gb;
            
            // Assert
            Assert.Equal(expectedMb, resourceUsage.MemoryUsageMB);
            
            // 反向验证
            resourceUsage.MemoryUsageMB = expectedMb;
            Assert.Equal(gb, resourceUsage.MemoryGb);
        }
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
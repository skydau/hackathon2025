using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using Xunit;

namespace TenantCatalogService.Tests;

/// <summary>
/// 测试租户模型的扁平化字段映射
/// </summary>
public class TenantModelMappingTests : IDisposable
{
    private readonly TenantCatalogDbContext _context;
    private readonly string _dbName;

    public TenantModelMappingTests()
    {
        _dbName = $"TestTenantMappingDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new TenantCatalogDbContext(options);
    }

    [Fact]
    public void Tenant_FlattenedFields_MapCorrectly()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-tenant-001",
            DisplayName = "测试医院",
            Status = TenantStatus.Provisioning,
            DbServer = "host.minikube.internal,1433",
            DbDatabase = "TestHospital_DB",
            DbUsername = "sa",
            DbPasswordRef = "keyvault/test-hospital-db-password",
            ThrottlingRps = 100,
            SloAvailability = "99.9%",
            SloP95LatencyMs = 1000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - 保存到数据库
        _context.Tenants.Add(tenant);
        _context.SaveChanges();

        // 从数据库重新读取
        var savedTenant = _context.Tenants.First(t => t.Id == "test-tenant-001");

        // Assert - 验证扁平化字段正确保存
        Assert.Equal("test-tenant-001", savedTenant.Id);
        Assert.Equal("测试医院", savedTenant.DisplayName);
        Assert.Equal(TenantStatus.Provisioning, savedTenant.Status);
        Assert.Equal("host.minikube.internal,1433", savedTenant.DbServer);
        Assert.Equal("TestHospital_DB", savedTenant.DbDatabase);
        Assert.Equal("sa", savedTenant.DbUsername);
        Assert.Equal("keyvault/test-hospital-db-password", savedTenant.DbPasswordRef);
        Assert.Equal(100, savedTenant.ThrottlingRps);
        Assert.Equal("99.9%", savedTenant.SloAvailability);
        Assert.Equal(1000, savedTenant.SloP95LatencyMs);
    }

    [Fact]
    public void Tenant_ComputedProperties_WorkCorrectly()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-tenant-002",
            DisplayName = "测试诊所",
            Status = TenantStatus.Enabled,
            DbServer = "localhost",
            DbDatabase = "TestClinic_DB",
            DbUsername = "testuser",
            DbPasswordRef = "keyvault/test-clinic-password",
            ThrottlingRps = 200,
            SloAvailability = "99.5%",
            SloP95LatencyMs = 500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert - 验证计算属性
        Assert.NotNull(tenant.DbConfig);
        Assert.Equal("perDatabase", tenant.DbConfig.Mode); // 默认模式
        Assert.Equal("localhost", tenant.DbConfig.Server);
        Assert.Equal("TestClinic_DB", tenant.DbConfig.Database);
        Assert.Equal("keyvault/test-clinic-password", tenant.DbConfig.CredentialRef);

        Assert.NotNull(tenant.Throttling);
        Assert.Equal(200, tenant.Throttling.Rps);

        Assert.NotNull(tenant.Slo);
        Assert.Equal("99.5%", tenant.Slo.Availability);
        Assert.Equal(500, tenant.Slo.P95LatencyMs);
    }

    [Fact]
    public void Tenant_ComputedProperties_SettersWork()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-tenant-003",
            DisplayName = "测试医疗中心",
            Status = TenantStatus.Enabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - 通过计算属性设置值
        tenant.DbConfig = new DatabaseConfig
        {
            Mode = "perDatabase",
            Server = "remote-server.com",
            Database = "MedicalCenter_DB",
            CredentialRef = "keyvault/medical-center-password"
        };

        tenant.Throttling = new ThrottlingConfig { Rps = 300 };

        tenant.Slo = new SLOConfig 
        { 
            Availability = "99.99%", 
            P95LatencyMs = 200 
        };

        // Assert - 验证扁平化字段被正确设置
        Assert.Equal("remote-server.com", tenant.DbServer);
        Assert.Equal("MedicalCenter_DB", tenant.DbDatabase);
        Assert.Equal("keyvault/medical-center-password", tenant.DbPasswordRef);
        Assert.Equal(300, tenant.ThrottlingRps);
        Assert.Equal("99.99%", tenant.SloAvailability);
        Assert.Equal(200, tenant.SloP95LatencyMs);
    }

    [Fact]
    public void Tenant_NullableFields_HandleCorrectly()
    {
        // Arrange - 准备测试数据（可选字段为空）
        var tenant = new Tenant
        {
            Id = "test-tenant-004",
            DisplayName = "最小配置医院",
            Status = TenantStatus.Provisioning,
            DbServer = "localhost",
            DbDatabase = "MinimalHospital_DB",
            DbUsername = "user",
            DbPasswordRef = "keyvault/minimal-password",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
            // ThrottlingRps, SloAvailability, SloP95LatencyMs 为 null
        };

        // Act - 保存到数据库
        _context.Tenants.Add(tenant);
        _context.SaveChanges();

        // 从数据库重新读取
        var savedTenant = _context.Tenants.First(t => t.Id == "test-tenant-004");

        // Assert - 验证可选字段处理
        Assert.Null(savedTenant.ThrottlingRps);
        Assert.Null(savedTenant.SloAvailability);
        Assert.Null(savedTenant.SloP95LatencyMs);

        // 验证计算属性正确处理 null 值
        Assert.Null(savedTenant.Throttling);
        Assert.Null(savedTenant.Slo);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
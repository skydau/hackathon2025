using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using TenantCatalogService.Repositories;
using Xunit;

namespace TenantCatalogService.Tests;

/// <summary>
/// 简单的租户操作测试，验证扁平化模型的基本功能
/// </summary>
public class SimpleTenantTests : IDisposable
{
    private readonly TenantCatalogDbContext _context;
    private readonly TenantRepository _repository;
    private readonly string _dbName;

    public SimpleTenantTests()
    {
        _dbName = $"SimpleTenantTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new TenantCatalogDbContext(options);
        _repository = new TenantRepository(_context);
    }

    [Fact]
    public async Task CreateTenant_WithFlattenedFields_ShouldSucceed()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-hospital-001",
            DisplayName = "测试医院",
            Status = TenantStatus.Provisioning,
            DbServer = "host.minikube.internal,1433",
            DbDatabase = "TestHospital_DB",
            DbUsername = "sa",
            DbPasswordRef = "keyvault/test-hospital-password",
            ThrottlingRps = 100,
            SloAvailability = "99.9%",
            SloP95LatencyMs = 1000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - 创建租户
        var createdTenant = await _repository.CreateAsync(tenant);

        // Assert - 验证创建成功
        Assert.NotNull(createdTenant);
        Assert.Equal("test-hospital-001", createdTenant.Id);
        Assert.Equal("测试医院", createdTenant.DisplayName);
        Assert.Equal(TenantStatus.Provisioning, createdTenant.Status);
        
        // 验证扁平化字段
        Assert.Equal("host.minikube.internal,1433", createdTenant.DbServer);
        Assert.Equal("TestHospital_DB", createdTenant.DbDatabase);
        Assert.Equal("sa", createdTenant.DbUsername);
        Assert.Equal("keyvault/test-hospital-password", createdTenant.DbPasswordRef);
        Assert.Equal(100, createdTenant.ThrottlingRps);
        Assert.Equal("99.9%", createdTenant.SloAvailability);
        Assert.Equal(1000, createdTenant.SloP95LatencyMs);
    }

    [Fact]
    public async Task GetTenant_ById_ShouldReturnCorrectTenant()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-clinic-002",
            DisplayName = "测试诊所",
            Status = TenantStatus.Enabled,
            DbServer = "localhost",
            DbDatabase = "TestClinic_DB",
            DbUsername = "clinicuser",
            DbPasswordRef = "keyvault/clinic-password",
            ThrottlingRps = 200,
            SloAvailability = "99.5%",
            SloP95LatencyMs = 500,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(tenant);

        // Act - 获取租户
        var retrievedTenant = await _repository.GetByIdAsync("test-clinic-002");

        // Assert - 验证获取成功
        Assert.NotNull(retrievedTenant);
        Assert.Equal("test-clinic-002", retrievedTenant.Id);
        Assert.Equal("测试诊所", retrievedTenant.DisplayName);
        Assert.Equal(TenantStatus.Enabled, retrievedTenant.Status);
        
        // 验证计算属性正常工作
        Assert.NotNull(retrievedTenant.DbConfig);
        Assert.Equal("localhost", retrievedTenant.DbConfig.Server);
        Assert.Equal("TestClinic_DB", retrievedTenant.DbConfig.Database);
        Assert.Equal("keyvault/clinic-password", retrievedTenant.DbConfig.CredentialRef);
        
        Assert.NotNull(retrievedTenant.Throttling);
        Assert.Equal(200, retrievedTenant.Throttling.Rps);
        
        Assert.NotNull(retrievedTenant.Slo);
        Assert.Equal("99.5%", retrievedTenant.Slo.Availability);
        Assert.Equal(500, retrievedTenant.Slo.P95LatencyMs);
    }

    [Fact]
    public async Task UpdateTenant_ShouldPersistChanges()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "test-medical-center-003",
            DisplayName = "测试医疗中心",
            Status = TenantStatus.Provisioning,
            DbServer = "old-server.com",
            DbDatabase = "OldDB",
            DbUsername = "olduser",
            DbPasswordRef = "keyvault/old-password",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(tenant);

        // Act - 更新租户
        tenant.Status = TenantStatus.Enabled;
        tenant.DbServer = "new-server.com";
        tenant.DbDatabase = "NewDB";
        tenant.ThrottlingRps = 300;
        tenant.SloAvailability = "99.99%";
        tenant.SloP95LatencyMs = 200;
        tenant.UpdatedAt = DateTime.UtcNow;

        var updatedTenant = await _repository.UpdateAsync(tenant);

        // Assert - 验证更新成功
        Assert.NotNull(updatedTenant);
        Assert.Equal(TenantStatus.Enabled, updatedTenant.Status);
        Assert.Equal("new-server.com", updatedTenant.DbServer);
        Assert.Equal("NewDB", updatedTenant.DbDatabase);
        Assert.Equal(300, updatedTenant.ThrottlingRps);
        Assert.Equal("99.99%", updatedTenant.SloAvailability);
        Assert.Equal(200, updatedTenant.SloP95LatencyMs);
    }

    [Fact]
    public async Task GetAllTenants_ShouldReturnAllTenants()
    {
        // Arrange - 准备测试数据
        var tenants = new[]
        {
            new Tenant
            {
                Id = "hospital-001",
                DisplayName = "第一医院",
                Status = TenantStatus.Enabled,
                DbServer = "server1.com",
                DbDatabase = "Hospital1_DB",
                DbUsername = "user1",
                DbPasswordRef = "keyvault/hospital1-password",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "clinic-002",
                DisplayName = "社区诊所",
                Status = TenantStatus.Provisioning,
                DbServer = "server2.com",
                DbDatabase = "Clinic2_DB",
                DbUsername = "user2",
                DbPasswordRef = "keyvault/clinic2-password",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var tenant in tenants)
        {
            await _repository.CreateAsync(tenant);
        }

        // Act - 获取所有租户
        var allTenants = await _repository.GetAllAsync();

        // Assert - 验证获取成功
        Assert.NotNull(allTenants);
        Assert.Equal(2, allTenants.Count());
        
        var tenantList = allTenants.ToList();
        Assert.Contains(tenantList, t => t.Id == "hospital-001");
        Assert.Contains(tenantList, t => t.Id == "clinic-002");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
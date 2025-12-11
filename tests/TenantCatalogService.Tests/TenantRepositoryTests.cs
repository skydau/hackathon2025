using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using TenantCatalogService.Repositories;
using Xunit;

namespace TenantCatalogService.Tests;

/// <summary>
/// 测试 TenantRepository 的数据库操作
/// </summary>
public class TenantRepositoryTests : IDisposable
{
    private readonly TenantCatalogDbContext _context;
    private readonly TenantRepository _repository;
    private readonly string _dbName;

    public TenantRepositoryTests()
    {
        _dbName = $"TenantRepositoryTestDb_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;
        _context = new TenantCatalogDbContext(options);
        _repository = new TenantRepository(_context);
    }

    [Fact]
    public async Task GetAllAsync_WithNoData_ShouldReturnEmptyList()
    {
        // Act - 获取所有租户（应该为空）
        var tenants = await _repository.GetAllAsync();

        // Assert - 验证返回空列表
        Assert.NotNull(tenants);
        Assert.Empty(tenants);
    }

    [Fact]
    public async Task GetAllAsync_WithData_ShouldReturnAllTenants()
    {
        // Arrange - 准备测试数据
        var testTenants = new[]
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
                ThrottlingRps = 100,
                SloAvailability = "99.9%",
                SloP95LatencyMs = 1000,
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
                ThrottlingRps = 50,
                SloAvailability = "99.5%",
                SloP95LatencyMs = 1500,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Tenant
            {
                Id = "medical-center-003",
                DisplayName = "医疗中心",
                Status = TenantStatus.Disabled,
                DbServer = "server3.com",
                DbDatabase = "MedicalCenter3_DB",
                DbUsername = "user3",
                DbPasswordRef = "keyvault/medical-center3-password",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        // 直接添加到 context 以避免通过 repository
        _context.Tenants.AddRange(testTenants);
        await _context.SaveChangesAsync();

        // Act - 获取所有租户
        var retrievedTenants = await _repository.GetAllAsync();

        // Assert - 验证返回所有租户
        Assert.NotNull(retrievedTenants);
        var tenantList = retrievedTenants.ToList();
        Assert.Equal(3, tenantList.Count);

        // 验证每个租户的数据
        var hospital = tenantList.First(t => t.Id == "hospital-001");
        Assert.Equal("第一医院", hospital.DisplayName);
        Assert.Equal(TenantStatus.Enabled, hospital.Status);
        Assert.Equal(100, hospital.ThrottlingRps);
        Assert.Equal("99.9%", hospital.SloAvailability);
        Assert.Equal(1000, hospital.SloP95LatencyMs);

        var clinic = tenantList.First(t => t.Id == "clinic-002");
        Assert.Equal("社区诊所", clinic.DisplayName);
        Assert.Equal(TenantStatus.Provisioning, clinic.Status);
        Assert.Equal(50, clinic.ThrottlingRps);

        var medicalCenter = tenantList.First(t => t.Id == "medical-center-003");
        Assert.Equal("医疗中心", medicalCenter.DisplayName);
        Assert.Equal(TenantStatus.Disabled, medicalCenter.Status);
        Assert.Null(medicalCenter.ThrottlingRps); // 这个租户没有设置限流
    }

    [Fact]
    public async Task CreateAsync_ShouldAddTenantToDatabase()
    {
        // Arrange - 准备测试数据
        var newTenant = new Tenant
        {
            Id = "new-tenant-001",
            DisplayName = "新建租户",
            Status = TenantStatus.Provisioning,
            DbServer = "new-server.com",
            DbDatabase = "NewTenant_DB",
            DbUsername = "newuser",
            DbPasswordRef = "keyvault/new-tenant-password",
            ThrottlingRps = 75,
            SloAvailability = "99.8%",
            SloP95LatencyMs = 800,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - 创建租户
        var createdTenant = await _repository.CreateAsync(newTenant);

        // Assert - 验证创建成功
        Assert.NotNull(createdTenant);
        Assert.Equal("new-tenant-001", createdTenant.Id);
        Assert.Equal("新建租户", createdTenant.DisplayName);
        Assert.Equal(TenantStatus.Provisioning, createdTenant.Status);

        // 验证数据库中确实有这个租户
        var allTenants = await _repository.GetAllAsync();
        Assert.Single(allTenants);
        Assert.Equal("new-tenant-001", allTenants.First().Id);
    }

    [Fact]
    public async Task GetByIdAsync_WithExistingTenant_ShouldReturnTenant()
    {
        // Arrange - 准备测试数据
        var tenant = new Tenant
        {
            Id = "existing-tenant",
            DisplayName = "现有租户",
            Status = TenantStatus.Enabled,
            DbServer = "existing-server.com",
            DbDatabase = "ExistingTenant_DB",
            DbUsername = "existinguser",
            DbPasswordRef = "keyvault/existing-password",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(tenant);

        // Act - 根据ID获取租户
        var retrievedTenant = await _repository.GetByIdAsync("existing-tenant");

        // Assert - 验证获取成功
        Assert.NotNull(retrievedTenant);
        Assert.Equal("existing-tenant", retrievedTenant.Id);
        Assert.Equal("现有租户", retrievedTenant.DisplayName);
        Assert.Equal(TenantStatus.Enabled, retrievedTenant.Status);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentTenant_ShouldReturnNull()
    {
        // Act - 尝试获取不存在的租户
        var retrievedTenant = await _repository.GetByIdAsync("non-existent-tenant");

        // Assert - 验证返回null
        Assert.Null(retrievedTenant);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
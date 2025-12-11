using FsCheck;
using FsCheck.Xunit;
using System.Net.Http.Json;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using Xunit;

namespace TenantCatalogService.Tests;

public class TenantPropertyTests : IClassFixture<TenantApiTestFixture>
{
    private readonly TenantApiTestFixture _factory;

    public TenantPropertyTests(TenantApiTestFixture factory)
    {
        _factory = factory;
    }

    // **Feature: multi-tenant-medical-platform, Property 1: 租户创建返回唯一ID**
    // **Validates: Requirements 1.1**
    [Property(MaxTest = 100)]
    public Property TenantCreationReturnsUniqueIds()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            (request) =>
            {
                var client = _factory.CreateClient();
                
                // Create first tenant
                var response1 = client.PostAsJsonAsync("/api/tenants", request).Result;
                response1.EnsureSuccessStatusCode();
                var tenant1 = response1.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Create second tenant with same data
                var response2 = client.PostAsJsonAsync("/api/tenants", request).Result;
                response2.EnsureSuccessStatusCode();
                var tenant2 = response2.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // IDs should be unique
                return tenant1 != null && tenant2 != null && tenant1.Id != tenant2.Id;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 2: 租户数据往返一致性**
    // **Validates: Requirements 1.2**
    [Property(MaxTest = 100)]
    public Property TenantDataRoundTripConsistency()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            (request) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant - 创建租户
                var createResponse = client.PostAsJsonAsync("/api/tenants", request).Result;
                createResponse.EnsureSuccessStatusCode();
                var createdTenant = createResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Get tenant - 获取租户
                var getResponse = client.GetAsync($"/api/tenants/{createdTenant!.Id}").Result;
                getResponse.EnsureSuccessStatusCode();
                var retrievedTenant = getResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Verify data consistency - 验证数据一致性
                return retrievedTenant != null &&
                       retrievedTenant.DisplayName == request.DisplayName &&
                       retrievedTenant.DbConfig.Mode == request.DbConfig.Mode &&
                       retrievedTenant.DbConfig.Server == request.DbConfig.Server &&
                       retrievedTenant.DbConfig.Database == request.DbConfig.Database &&
                       retrievedTenant.DbConfig.CredentialRef == request.DbConfig.CredentialRef &&
                       (request.Throttling == null || retrievedTenant.Throttling?.Rps == request.Throttling.Rps);
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 3: 租户状态更新反映正确**
    // **Validates: Requirements 1.3**
    [Property(MaxTest = 100)]
    public Property TenantStatusUpdateReflectsCorrectly()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            Gen.Elements(TenantStatus.Enabled, TenantStatus.Disabled, TenantStatus.Decommissioned).ToArbitrary(),
            (request, newStatus) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant
                var createResponse = client.PostAsJsonAsync("/api/tenants", request).Result;
                createResponse.EnsureSuccessStatusCode();
                var createdTenant = createResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Update status
                var updateRequest = new UpdateTenantRequest { Status = newStatus };
                var updateResponse = client.PatchAsJsonAsync($"/api/tenants/{createdTenant!.Id}", updateRequest).Result;
                updateResponse.EnsureSuccessStatusCode();
                
                // Get tenant again
                var getResponse = client.GetAsync($"/api/tenants/{createdTenant.Id}").Result;
                getResponse.EnsureSuccessStatusCode();
                var updatedTenant = getResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Verify status was updated
                return updatedTenant != null && updatedTenant.Status == newStatus;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 4: 新租户初始状态一致**
    // **Validates: Requirements 1.4**
    [Property(MaxTest = 100)]
    public Property NewTenantInitialStatusConsistent()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            (request) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant
                var createResponse = client.PostAsJsonAsync("/api/tenants", request).Result;
                createResponse.EnsureSuccessStatusCode();
                var createdTenant = createResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Verify initial status is Provisioning
                return createdTenant != null && createdTenant.Status == TenantStatus.Provisioning;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 5: 不存在的租户返回404**
    // **Validates: Requirements 1.5**
    [Property(MaxTest = 100)]
    public Property NonExistentTenantReturns404()
    {
        return Prop.ForAll(
            Arb.Generate<Guid>().ToArbitrary(),
            (randomGuid) =>
            {
                var client = _factory.CreateClient();
                
                // Try to get non-existent tenant
                var getResponse = client.GetAsync($"/api/tenants/{randomGuid}").Result;
                
                // Verify 404 response
                return getResponse.StatusCode == System.Net.HttpStatusCode.NotFound;
            });
    }

    private static Arbitrary<CreateTenantRequest> GenerateValidTenantRequest()
    {
        var gen = from displayName in Gen.Elements("Hospital A", "Hospital B", "Clinic C", "Medical Center D")
                  from mode in Gen.Elements("perDatabase", "perSchema")
                  from server in Gen.Elements("sqlserver1.local", "sqlserver2.local", "db.example.com")
                  from database in Gen.Elements("TenantDB1", "TenantDB2", "MedicalDB")
                  from credentialRef in Gen.Elements("keyvault/secret1", "keyvault/secret2", "azure/cred1")
                  from rps in Gen.Choose(1, 1000)
                  select new CreateTenantRequest
                  {
                      DisplayName = displayName,
                      DbConfig = new DatabaseConfig
                      {
                          Mode = mode,
                          Server = server,
                          Database = database,
                          CredentialRef = credentialRef
                      },
                      Throttling = new ThrottlingConfig { Rps = rps }
                  };
        
        return Arb.From(gen);
    }
}

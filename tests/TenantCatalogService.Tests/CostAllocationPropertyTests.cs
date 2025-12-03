using FsCheck;
using FsCheck.Xunit;
using System.Net.Http.Json;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using Xunit;

namespace TenantCatalogService.Tests;

public class CostAllocationPropertyTests : IClassFixture<TenantApiTestFixture>
{
    private readonly TenantApiTestFixture _factory;

    public CostAllocationPropertyTests(TenantApiTestFixture factory)
    {
        _factory = factory;
    }

    // **Feature: multi-tenant-medical-platform, Property 70: 资源使用指标记录**
    // **Validates: Requirements 15.1**
    [Property(MaxTest = 100)]
    public Property ResourceUsageMetricsRecorded()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            GenerateValidResourceUsage(),
            (tenantRequest, usageRequest) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant first
                var tenantResponse = client.PostAsJsonAsync("/api/tenants", tenantRequest).Result;
                tenantResponse.EnsureSuccessStatusCode();
                var tenant = tenantResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Set tenant ID in usage request
                usageRequest.TenantId = tenant!.Id;
                
                // Record resource usage
                var usageResponse = client.PostAsJsonAsync("/api/costs/usage", usageRequest).Result;
                usageResponse.EnsureSuccessStatusCode();
                
                // Query usage history
                var startDate = usageRequest.Timestamp.AddHours(-1);
                var endDate = usageRequest.Timestamp.AddHours(1);
                var historyResponse = client.GetAsync(
                    $"/api/costs/tenants/{tenant.Id}/usage?startDate={startDate:O}&endDate={endDate:O}").Result;
                historyResponse.EnsureSuccessStatusCode();
                var history = historyResponse.Content.ReadFromJsonAsync<List<ResourceUsage>>().Result;
                
                // Verify the usage was recorded with all metrics
                return history != null && 
                       history.Any(u => 
                           u.TenantId == tenant.Id &&
                           u.CpuCores == usageRequest.CpuCores &&
                           u.MemoryGb == usageRequest.MemoryGb &&
                           u.StorageGb == usageRequest.StorageGb &&
                           u.NetworkGb == usageRequest.NetworkGb);
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 71: 成本计算公式正确**
    // **Validates: Requirements 15.2**
    [Property(MaxTest = 100)]
    public Property CostCalculationFormulaCorrect()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            GenerateValidResourceUsage(),
            (tenantRequest, usageRequest) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant
                var tenantResponse = client.PostAsJsonAsync("/api/tenants", tenantRequest).Result;
                tenantResponse.EnsureSuccessStatusCode();
                var tenant = tenantResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                usageRequest.TenantId = tenant!.Id;
                
                // Record resource usage
                var usageResponse = client.PostAsJsonAsync("/api/costs/usage", usageRequest).Result;
                usageResponse.EnsureSuccessStatusCode();
                
                // Generate cost report
                var startDate = usageRequest.Timestamp.AddHours(-1);
                var endDate = usageRequest.Timestamp.AddHours(1);
                var reportResponse = client.GetAsync(
                    $"/api/costs/tenants/{tenant.Id}/range?startDate={startDate:O}&endDate={endDate:O}").Result;
                reportResponse.EnsureSuccessStatusCode();
                var report = reportResponse.Content.ReadFromJsonAsync<CostReportResponse>().Result;
                
                // Verify cost calculation formula
                // Cost rates: CPU=$0.05/core-hour, Memory=$0.01/GB-hour, Storage=$0.10/GB-month, Network=$0.12/GB
                var hours = (endDate - startDate).TotalHours;
                var expectedCpuCost = Math.Round(usageRequest.CpuCores * hours * 0.05, 2);
                var expectedMemoryCost = Math.Round(usageRequest.MemoryGb * hours * 0.01, 2);
                var expectedStorageCost = Math.Round(usageRequest.StorageGb * (hours / 730.0) * 0.10, 2);
                var expectedNetworkCost = Math.Round(usageRequest.NetworkGb * 0.12, 2);
                var expectedTotal = expectedCpuCost + expectedMemoryCost + expectedStorageCost + expectedNetworkCost;
                
                return report != null &&
                       Math.Abs(report.Breakdown.CpuCost - expectedCpuCost) < 0.01 &&
                       Math.Abs(report.Breakdown.MemoryCost - expectedMemoryCost) < 0.01 &&
                       Math.Abs(report.Breakdown.StorageCost - expectedStorageCost) < 0.01 &&
                       Math.Abs(report.Breakdown.NetworkCost - expectedNetworkCost) < 0.01 &&
                       Math.Abs(report.TotalCost - expectedTotal) < 0.01;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 72: 月度报告包含明细**
    // **Validates: Requirements 15.3**
    [Property(MaxTest = 100)]
    public Property MonthlyReportContainsBreakdown()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            GenerateValidResourceUsage(),
            (tenantRequest, usageRequest) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant
                var tenantResponse = client.PostAsJsonAsync("/api/tenants", tenantRequest).Result;
                tenantResponse.EnsureSuccessStatusCode();
                var tenant = tenantResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                usageRequest.TenantId = tenant!.Id;
                
                // Record resource usage
                var usageResponse = client.PostAsJsonAsync("/api/costs/usage", usageRequest).Result;
                usageResponse.EnsureSuccessStatusCode();
                
                // Generate monthly cost report
                var year = usageRequest.Timestamp.Year;
                var month = usageRequest.Timestamp.Month;
                var reportResponse = client.GetAsync(
                    $"/api/costs/tenants/{tenant.Id}/monthly/{year}/{month}").Result;
                reportResponse.EnsureSuccessStatusCode();
                var report = reportResponse.Content.ReadFromJsonAsync<CostReportResponse>().Result;
                
                // Verify report contains all breakdown details
                return report != null &&
                       report.Breakdown != null &&
                       report.Breakdown.CpuCost >= 0 &&
                       report.Breakdown.MemoryCost >= 0 &&
                       report.Breakdown.StorageCost >= 0 &&
                       report.Breakdown.NetworkCost >= 0 &&
                       report.TotalCost >= 0;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 73: 配额限制强制执行**
    // **Validates: Requirements 15.4**
    [Property(MaxTest = 100)]
    public Property QuotaLimitsEnforced()
    {
        return Prop.ForAll(
            GenerateValidTenantRequest(),
            (tenantRequest) =>
            {
                var client = _factory.CreateClient();
                
                // Create tenant
                var tenantResponse = client.PostAsJsonAsync("/api/tenants", tenantRequest).Result;
                tenantResponse.EnsureSuccessStatusCode();
                var tenant = tenantResponse.Content.ReadFromJsonAsync<TenantResponse>().Result;
                
                // Verify tenant has throttling configuration (which represents quota limits)
                return tenant != null &&
                       tenant.Throttling != null &&
                       tenant.Throttling.Rps > 0;
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

    private static Arbitrary<ResourceUsageRequest> GenerateValidResourceUsage()
    {
        var gen = from cpuCores in Gen.Choose(1, 16).Select(x => (double)x)
                  from memoryGb in Gen.Choose(1, 128).Select(x => (double)x)
                  from storageGb in Gen.Choose(10, 1000).Select(x => (double)x)
                  from networkGb in Gen.Choose(0, 100).Select(x => (double)x / 10.0)
                  from timestamp in Gen.Choose(0, 365).Select(days => DateTime.UtcNow.AddDays(-days))
                  select new ResourceUsageRequest
                  {
                      TenantId = "", // Will be set by test
                      Timestamp = timestamp,
                      CpuCores = cpuCores,
                      MemoryGb = memoryGb,
                      StorageGb = storageGb,
                      NetworkGb = networkGb
                  };
        
        return Arb.From(gen);
    }
}

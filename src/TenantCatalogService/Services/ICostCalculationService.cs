using TenantCatalogService.Models;

namespace TenantCatalogService.Services;

public interface ICostCalculationService
{
    Task<CostReport> GenerateMonthlyCostReportAsync(string tenantId, int year, int month);
    Task<CostReport> GenerateCostReportAsync(string tenantId, DateTime startDate, DateTime endDate);
    Task RecordResourceUsageAsync(ResourceUsage usage);
    Task<List<ResourceUsage>> GetResourceUsageHistoryAsync(string tenantId, DateTime startDate, DateTime endDate);
}

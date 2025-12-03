using TenantCatalogService.Models;

namespace TenantCatalogService.Repositories;

public interface IResourceUsageRepository
{
    Task<ResourceUsage> CreateAsync(ResourceUsage usage);
    Task<List<ResourceUsage>> GetByTenantAndDateRangeAsync(string tenantId, DateTime startDate, DateTime endDate);
}

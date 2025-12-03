using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;

namespace TenantCatalogService.Repositories;

public class ResourceUsageRepository : IResourceUsageRepository
{
    private readonly TenantCatalogDbContext _context;

    public ResourceUsageRepository(TenantCatalogDbContext context)
    {
        _context = context;
    }

    public async Task<ResourceUsage> CreateAsync(ResourceUsage usage)
    {
        _context.ResourceUsages.Add(usage);
        await _context.SaveChangesAsync();
        return usage;
    }

    public async Task<List<ResourceUsage>> GetByTenantAndDateRangeAsync(string tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.ResourceUsages
            .Where(u => u.TenantId == tenantId && u.Timestamp >= startDate && u.Timestamp < endDate)
            .OrderBy(u => u.Timestamp)
            .ToListAsync();
    }
}

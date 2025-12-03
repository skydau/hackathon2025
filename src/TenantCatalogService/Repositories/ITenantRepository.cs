using TenantCatalogService.Models;

namespace TenantCatalogService.Repositories;

public interface ITenantRepository
{
    Task<Tenant> CreateAsync(Tenant tenant);
    Task<Tenant?> GetByIdAsync(string id);
    Task<Tenant> UpdateAsync(Tenant tenant);
    Task<IEnumerable<Tenant>> GetAllAsync();
}

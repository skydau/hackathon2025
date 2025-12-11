using TenantCatalogService.Models;

namespace TenantCatalogService.Services;

public interface IKubernetesService
{
    Task<bool> CreateTenantCRDAsync(Tenant tenant);
    Task<bool> UpdateTenantCRDAsync(Tenant tenant);
    Task<bool> DeleteTenantCRDAsync(string tenantId);
}
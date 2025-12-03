using TenantDBRouter.Models;

namespace TenantDBRouter.Clients;

public interface ITenantCatalogClient
{
    Task<DatabaseConfig> GetDbConfigAsync(string tenantId, CancellationToken cancellationToken = default);
}

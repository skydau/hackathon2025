using System.Net.Http.Json;
using TenantDBRouter.Models;

namespace TenantDBRouter.Clients;

public class TenantCatalogClient : ITenantCatalogClient
{
    private readonly HttpClient _httpClient;

    public TenantCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<DatabaseConfig> GetDbConfigAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        var response = await _httpClient.GetAsync($"/api/tenants/{tenantId}/db-config", cancellationToken);
        response.EnsureSuccessStatusCode();

        var config = await response.Content.ReadFromJsonAsync<DatabaseConfig>(cancellationToken: cancellationToken);
        return config ?? throw new InvalidOperationException($"Failed to retrieve database configuration for tenant {tenantId}");
    }
}

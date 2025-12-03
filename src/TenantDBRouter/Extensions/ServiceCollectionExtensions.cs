using Microsoft.Extensions.DependencyInjection;
using TenantDBRouter.Clients;
using TenantDBRouter.Services;

namespace TenantDBRouter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTenantDBRouter(
        this IServiceCollection services, 
        string tenantCatalogBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(tenantCatalogBaseUrl))
        {
            throw new ArgumentException("Tenant Catalog base URL cannot be null or empty", nameof(tenantCatalogBaseUrl));
        }

        // Register TenantContextAccessor
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // Register TenantCatalogClient
        services.AddHttpClient<ITenantCatalogClient, TenantCatalogClient>(client =>
        {
            client.BaseAddress = new Uri(tenantCatalogBaseUrl);
        });

        // Register TenantDBRouter
        services.AddSingleton<TenantDBRouter>();

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace SharedLibrary.WorkloadIdentity;

/// <summary>
/// Extension methods for registering Workload Identity services
/// </summary>
public static class WorkloadIdentityServiceExtensions
{
    /// <summary>
    /// Adds Workload Identity services to the service collection
    /// </summary>
    public static IServiceCollection AddWorkloadIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register credential provider
        services.AddSingleton<AzureCredentialProvider>();

        // Register Key Vault secret provider if URL is configured
        var keyVaultUrl = configuration["KEY_VAULT_URL"] ?? 
                         configuration["Azure:KeyVault:Url"];
        
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddSingleton(sp =>
            {
                var credentialProvider = sp.GetRequiredService<AzureCredentialProvider>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KeyVaultSecretProvider>>();
                return new KeyVaultSecretProvider(credentialProvider, keyVaultUrl, logger);
            });
        }

        // Register SQL connection factory
        services.AddSingleton<SqlConnectionFactory>();

        return services;
    }

    /// <summary>
    /// Adds Workload Identity services with custom Key Vault URL
    /// </summary>
    public static IServiceCollection AddWorkloadIdentity(
        this IServiceCollection services,
        string keyVaultUrl)
    {
        // Register credential provider
        services.AddSingleton<AzureCredentialProvider>();

        // Register Key Vault secret provider
        services.AddSingleton(sp =>
        {
            var credentialProvider = sp.GetRequiredService<AzureCredentialProvider>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KeyVaultSecretProvider>>();
            return new KeyVaultSecretProvider(credentialProvider, keyVaultUrl, logger);
        });

        // Register SQL connection factory
        services.AddSingleton<SqlConnectionFactory>();

        return services;
    }
}

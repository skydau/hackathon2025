using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;

namespace SharedLibrary.WorkloadIdentity;

/// <summary>
/// Provides Azure credentials using Workload Identity for passwordless authentication
/// </summary>
public class AzureCredentialProvider
{
    private readonly ILogger<AzureCredentialProvider> _logger;
    private readonly TokenCredential _credential;

    public AzureCredentialProvider(ILogger<AzureCredentialProvider> logger)
    {
        _logger = logger;
        
        // DefaultAzureCredential automatically detects Workload Identity
        // when running in AKS with proper configuration
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            // Exclude interactive authentication methods
            ExcludeInteractiveBrowserCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false, // Keep for local development
            
            // Retry configuration
            Retry =
            {
                MaxRetries = 3,
                Delay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(10),
                Mode = RetryMode.Exponential
            }
        });

        _logger.LogInformation("Azure credential provider initialized with Workload Identity support");
    }

    /// <summary>
    /// Gets the TokenCredential for authenticating to Azure services
    /// </summary>
    public TokenCredential GetCredential() => _credential;

    /// <summary>
    /// Validates that Workload Identity is properly configured
    /// </summary>
    public bool IsWorkloadIdentityConfigured()
    {
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
        var tokenFile = Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE");

        var isConfigured = !string.IsNullOrEmpty(clientId) && 
                          !string.IsNullOrEmpty(tenantId) && 
                          !string.IsNullOrEmpty(tokenFile);

        if (isConfigured)
        {
            _logger.LogInformation(
                "Workload Identity configured - ClientId: {ClientId}, TenantId: {TenantId}, TokenFile: {TokenFile}",
                clientId, tenantId, tokenFile);
        }
        else
        {
            _logger.LogWarning("Workload Identity not configured, falling back to other credential types");
        }

        return isConfigured;
    }
}

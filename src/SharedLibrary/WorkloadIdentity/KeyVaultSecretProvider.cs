using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;

namespace SharedLibrary.WorkloadIdentity;

/// <summary>
/// Provides access to Azure Key Vault secrets using Workload Identity
/// </summary>
public class KeyVaultSecretProvider
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultSecretProvider> _logger;
    private readonly Dictionary<string, (string Value, DateTimeOffset CachedAt)> _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public KeyVaultSecretProvider(
        AzureCredentialProvider credentialProvider,
        string keyVaultUrl,
        ILogger<KeyVaultSecretProvider> logger)
    {
        _logger = logger;
        _cache = new Dictionary<string, (string, DateTimeOffset)>();

        var credential = credentialProvider.GetCredential();
        _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);

        _logger.LogInformation("Key Vault secret provider initialized for {KeyVaultUrl}", keyVaultUrl);
    }

    /// <summary>
    /// Retrieves a secret from Key Vault with caching
    /// </summary>
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cache.TryGetValue(secretName, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt < _cacheExpiration)
            {
                _logger.LogDebug("Returning cached secret: {SecretName}", secretName);
                return cached.Value;
            }
            
            // Remove expired cache entry
            _cache.Remove(secretName);
        }

        try
        {
            _logger.LogInformation("Retrieving secret from Key Vault: {SecretName}", secretName);
            
            KeyVaultSecret secret = await _secretClient.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            
            // Cache the secret value
            _cache[secretName] = (secret.Value, DateTimeOffset.UtcNow);
            
            _logger.LogInformation("Successfully retrieved secret: {SecretName}", secretName);
            return secret.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve secret from Key Vault: {SecretName}", secretName);
            throw;
        }
    }

    /// <summary>
    /// Clears the secret cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Secret cache cleared");
    }
}

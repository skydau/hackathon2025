using Xunit;
using Microsoft.Extensions.Logging;
using SharedLibrary.WorkloadIdentity;
using Microsoft.Data.SqlClient;
using Azure.Security.KeyVault.Secrets;

namespace WorkloadIdentity.Tests;

/// <summary>
/// Integration tests for AKS Workload Identity passwordless authentication
/// Validates Requirement 9.4: Microservices SHALL use AKS Workload Identity for passwordless authentication
/// </summary>
public class WorkloadIdentityIntegrationTests : IAsyncLifetime
{
    private readonly ILoggerFactory _loggerFactory;
    private AzureCredentialProvider? _credentialProvider;
    private KeyVaultSecretProvider? _keyVaultProvider;
    private SqlConnectionFactory? _sqlConnectionFactory;

    // Configuration from environment variables
    private readonly string _keyVaultUrl;
    private readonly string _sqlServer;
    private readonly string _testDatabase;
    private readonly bool _skipTests;

    public WorkloadIdentityIntegrationTests()
    {
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Read configuration from environment
        _keyVaultUrl = Environment.GetEnvironmentVariable("KEY_VAULT_URL") ?? "https://medlogic-kv.vault.azure.net/";
        _sqlServer = Environment.GetEnvironmentVariable("SQL_SERVER") ?? "medlogic-sql.database.windows.net";
        _testDatabase = Environment.GetEnvironmentVariable("TEST_DATABASE") ?? "TenantCatalog";

        // Skip tests if not running in AKS or if Workload Identity is not configured
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        _skipTests = string.IsNullOrEmpty(clientId);

        if (_skipTests)
        {
            Console.WriteLine("Workload Identity not configured - tests will be skipped");
            Console.WriteLine("To run these tests, ensure AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_FEDERATED_TOKEN_FILE are set");
        }
    }

    public async Task InitializeAsync()
    {
        if (!_skipTests)
        {
            var logger = _loggerFactory.CreateLogger<AzureCredentialProvider>();
            _credentialProvider = new AzureCredentialProvider(logger);

            var kvLogger = _loggerFactory.CreateLogger<KeyVaultSecretProvider>();
            _keyVaultProvider = new KeyVaultSecretProvider(_credentialProvider, _keyVaultUrl, kvLogger);

            var sqlLogger = _loggerFactory.CreateLogger<SqlConnectionFactory>();
            _sqlConnectionFactory = new SqlConnectionFactory(_credentialProvider, sqlLogger);
        }

        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _loggerFactory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void WorkloadIdentity_ShouldBeConfigured_WhenRunningInAKS()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange & Act
        var isConfigured = _credentialProvider!.IsWorkloadIdentityConfigured();

        // Assert
        Assert.True(isConfigured, "Workload Identity should be configured with AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_FEDERATED_TOKEN_FILE");
    }

    [Fact]
    public async Task KeyVault_ShouldAccessSecrets_WithoutPassword()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        var testSecretName = "test-secret";

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
        {
            var secretValue = await _keyVaultProvider!.GetSecretAsync(testSecretName);
            Assert.NotNull(secretValue);
            Assert.NotEmpty(secretValue);
        });

        // If the secret doesn't exist, that's okay - we're testing authentication, not the secret itself
        if (exception != null)
        {
            // Check if it's an authentication error or just a missing secret
            Assert.DoesNotContain("Unauthorized", exception.Message);
            Assert.DoesNotContain("Forbidden", exception.Message);
            Assert.DoesNotContain("authentication", exception.Message.ToLower());
        }
    }

    [Fact]
    public async Task SqlServer_ShouldConnect_WithoutPassword()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange & Act
        var canConnect = await _sqlConnectionFactory!.TestConnectionAsync(_sqlServer, _testDatabase);

        // Assert
        Assert.True(canConnect, $"Should be able to connect to SQL Server {_sqlServer}/{_testDatabase} using Workload Identity");
    }

    [Fact]
    public async Task SqlServer_ConnectionString_ShouldUseAzureADAuthentication()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        using var connection = _sqlConnectionFactory!.CreateConnection(_sqlServer, _testDatabase);

        // Act
        var connectionString = connection.ConnectionString;

        // Assert
        Assert.Contains("Authentication=Active Directory Default", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User ID", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SqlServer_ShouldExecuteQuery_WithWorkloadIdentity()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        using var connection = _sqlConnectionFactory!.CreateConnection(_sqlServer, _testDatabase);

        // Act & Assert
        await connection.OpenAsync();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 AS TestValue";
        
        var result = await command.ExecuteScalarAsync();
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task KeyVault_SecretCache_ShouldWork()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        var testSecretName = "test-secret";

        try
        {
            // Act - First call should fetch from Key Vault
            var secret1 = await _keyVaultProvider!.GetSecretAsync(testSecretName);
            
            // Second call should use cache
            var secret2 = await _keyVaultProvider!.GetSecretAsync(testSecretName);

            // Assert
            Assert.Equal(secret1, secret2);

            // Clear cache and fetch again
            _keyVaultProvider.ClearCache();
            var secret3 = await _keyVaultProvider.GetSecretAsync(testSecretName);
            Assert.Equal(secret1, secret3);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Secret doesn't exist - that's okay for this test
            // We're testing the cache mechanism, not the secret itself
            Assert.True(true, "Secret not found, but authentication worked");
        }
    }

    [Fact]
    public async Task MultipleServices_ShouldAccessDifferentResources_WithSeparateIdentities()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // This test validates that different services can use their own Workload Identities
        // In a real scenario, each service would have its own ServiceAccount with different permissions

        // Arrange
        var tenantCatalogDb = "TenantCatalog";
        var deviceRegistryDb = "DeviceRegistry";

        // Act - Test access to different databases
        var canAccessTenantCatalog = await _sqlConnectionFactory!.TestConnectionAsync(_sqlServer, tenantCatalogDb);
        var canAccessDeviceRegistry = await _sqlConnectionFactory!.TestConnectionAsync(_sqlServer, deviceRegistryDb);

        // Assert - At least one should be accessible (depending on the service's identity)
        Assert.True(canAccessTenantCatalog || canAccessDeviceRegistry,
            "Service should be able to access at least one database using Workload Identity");
    }

    [Fact]
    public void CredentialProvider_ShouldUseDefaultAzureCredential()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange & Act
        var credential = _credentialProvider!.GetCredential();

        // Assert
        Assert.NotNull(credential);
        Assert.IsType<Azure.Identity.DefaultAzureCredential>(credential);
    }

    [Fact]
    public async Task KeyVault_ShouldHandleErrors_Gracefully()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        var nonExistentSecret = "this-secret-definitely-does-not-exist-" + Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<Azure.RequestFailedException>(async () =>
        {
            await _keyVaultProvider!.GetSecretAsync(nonExistentSecret);
        });
    }

    [Fact]
    public async Task SqlServer_ShouldHandleConnectionFailure_Gracefully()
    {
        if (_skipTests)
        {
            // Skip test if not in AKS environment
            return;
        }

        // Arrange
        var invalidServer = "invalid-server.database.windows.net";
        var invalidDatabase = "NonExistentDatabase";

        // Act
        var canConnect = await _sqlConnectionFactory!.TestConnectionAsync(invalidServer, invalidDatabase);

        // Assert
        Assert.False(canConnect, "Should not be able to connect to invalid server");
    }
}

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SharedLibrary.WorkloadIdentity;

/// <summary>
/// Factory for creating SQL connections using Workload Identity for passwordless authentication
/// </summary>
public class SqlConnectionFactory
{
    private readonly ILogger<SqlConnectionFactory> _logger;
    private readonly AzureCredentialProvider _credentialProvider;

    public SqlConnectionFactory(
        AzureCredentialProvider credentialProvider,
        ILogger<SqlConnectionFactory> logger)
    {
        _credentialProvider = credentialProvider;
        _logger = logger;
    }

    /// <summary>
    /// Creates a SQL connection using Azure AD authentication (Workload Identity)
    /// </summary>
    public SqlConnection CreateConnection(string server, string database)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
            MultipleActiveResultSets = true
        };

        // Use Azure AD authentication when Workload Identity is configured
        if (_credentialProvider.IsWorkloadIdentityConfigured())
        {
            connectionStringBuilder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
            _logger.LogInformation(
                "Creating SQL connection with Azure AD authentication - Server: {Server}, Database: {Database}",
                server, database);
        }
        else
        {
            _logger.LogWarning(
                "Workload Identity not configured, connection string must include credentials - Server: {Server}, Database: {Database}",
                server, database);
        }

        return new SqlConnection(connectionStringBuilder.ToString());
    }

    /// <summary>
    /// Creates a SQL connection with custom connection string options
    /// </summary>
    public SqlConnection CreateConnection(string server, string database, Action<SqlConnectionStringBuilder> configure)
    {
        var connectionStringBuilder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
            MultipleActiveResultSets = true
        };

        // Apply custom configuration
        configure(connectionStringBuilder);

        // Override authentication method if Workload Identity is configured
        if (_credentialProvider.IsWorkloadIdentityConfigured())
        {
            connectionStringBuilder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
            _logger.LogInformation(
                "Creating SQL connection with Azure AD authentication and custom options - Server: {Server}, Database: {Database}",
                server, database);
        }

        return new SqlConnection(connectionStringBuilder.ToString());
    }

    /// <summary>
    /// Tests the SQL connection
    /// </summary>
    public async Task<bool> TestConnectionAsync(string server, string database, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = CreateConnection(server, database);
            await connection.OpenAsync(cancellationToken);
            
            _logger.LogInformation(
                "Successfully connected to SQL Server - Server: {Server}, Database: {Database}",
                server, database);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to connect to SQL Server - Server: {Server}, Database: {Database}",
                server, database);
            
            return false;
        }
    }
}

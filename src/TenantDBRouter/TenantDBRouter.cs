using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using TenantDBRouter.Clients;
using TenantDBRouter.Exceptions;
using TenantDBRouter.Models;

namespace TenantDBRouter;

public class TenantDBRouter : IDisposable
{
    private readonly ConcurrentDictionary<string, SqlConnection> _connectionPools;
    private readonly ITenantCatalogClient _tenantCatalogClient;
    private bool _disposed;

    public TenantDBRouter(ITenantCatalogClient tenantCatalogClient)
    {
        _tenantCatalogClient = tenantCatalogClient ?? throw new ArgumentNullException(nameof(tenantCatalogClient));
        _connectionPools = new ConcurrentDictionary<string, SqlConnection>();
    }

    public async Task<SqlConnection> GetTenantConnectionAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new TenantIdMissingException("Tenant ID cannot be null or empty");
        }

        // Check if connection already exists and is open
        if (_connectionPools.TryGetValue(tenantId, out var existingConnection))
        {
            if (existingConnection.State == System.Data.ConnectionState.Open)
            {
                return existingConnection;
            }
            else
            {
                // Remove closed connection
                _connectionPools.TryRemove(tenantId, out _);
                existingConnection.Dispose();
            }
        }

        // Get database configuration from Tenant Catalog
        var dbConfig = await _tenantCatalogClient.GetDbConfigAsync(tenantId, cancellationToken);

        // Build connection string
        var connectionString = BuildConnectionString(dbConfig);

        // Create new connection
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Set schema for Schema-per-Tenant mode
        if (dbConfig.Mode.Equals("perSchema", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dbConfig.Schema))
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SET SCHEMA '{dbConfig.Schema}'";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // Store in pool
        _connectionPools.TryAdd(tenantId, connection);

        return connection;
    }

    public async Task<T?> ExecuteQueryAsync<T>(string tenantId, Func<SqlConnection, Task<T>> queryFunc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new TenantIdMissingException("Tenant ID cannot be null or empty");
        }

        var connection = await GetTenantConnectionAsync(tenantId, cancellationToken);
        return await queryFunc(connection);
    }

    private string BuildConnectionString(DatabaseConfig dbConfig)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dbConfig.Server,
            InitialCatalog = dbConfig.Database,
            UserID = "sa",
            Password = "md1599",
            TrustServerCertificate = true,
            MultipleActiveResultSets = true,
            ConnectTimeout = 5 // 5 seconds timeout
        };

        return builder.ConnectionString;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var connection in _connectionPools.Values)
        {
            try
            {
                connection.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _connectionPools.Clear();
        _disposed = true;
    }
}

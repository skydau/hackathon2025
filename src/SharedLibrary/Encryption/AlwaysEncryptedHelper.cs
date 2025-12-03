using Microsoft.Data.SqlClient;
using System.Data;

namespace SharedLibrary.Encryption;

/// <summary>
/// Helper class for working with Always Encrypted columns
/// Provides utilities for client-side encryption/decryption
/// </summary>
public class AlwaysEncryptedHelper
{
    /// <summary>
    /// Creates a SQL connection with Always Encrypted enabled
    /// </summary>
    public static SqlConnection CreateEncryptedConnection(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Enabled
        };

        return new SqlConnection(builder.ConnectionString);
    }

    /// <summary>
    /// Verifies that a connection has Always Encrypted enabled
    /// </summary>
    public static bool IsAlwaysEncryptedEnabled(SqlConnection connection)
    {
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
        return builder.ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled;
    }

    /// <summary>
    /// Inserts data into a table with encrypted columns
    /// The encryption happens automatically on the client side
    /// </summary>
    public static async Task<int> InsertEncryptedDataAsync(
        SqlConnection connection,
        string tableName,
        Dictionary<string, object> columnValues,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = string.Join(", ", columnValues.Keys);
        var parameters = string.Join(", ", columnValues.Keys.Select(k => $"@{k}"));

        var command = connection.CreateCommand();
        command.CommandText = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters}); SELECT SCOPE_IDENTITY();";

        foreach (var kvp in columnValues)
        {
            command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Queries data from a table with encrypted columns
    /// The decryption happens automatically on the client side if the client has access to the keys
    /// </summary>
    public static async Task<List<Dictionary<string, object>>> QueryEncryptedDataAsync(
        SqlConnection connection,
        string query,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var command = connection.CreateCommand();
        command.CommandText = query;

        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                command.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
            }
        }

        var results = new List<Dictionary<string, object>>();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
            }
            results.Add(row);
        }

        return results;
    }

    /// <summary>
    /// Attempts to query encrypted data without proper keys
    /// This should fail or return encrypted data, demonstrating access control
    /// </summary>
    public static async Task<bool> CanAccessEncryptedDataAsync(
        string connectionString,
        string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create connection WITHOUT Always Encrypted enabled
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ColumnEncryptionSetting = SqlConnectionColumnEncryptionSetting.Disabled
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            var command = connection.CreateCommand();
            command.CommandText = query;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            
            // If we can read the data, it means encryption is not properly enforced
            // or the data is not encrypted
            return await reader.ReadAsync(cancellationToken);
        }
        catch (SqlException)
        {
            // Expected: Cannot access encrypted data without proper configuration
            return false;
        }
    }
}

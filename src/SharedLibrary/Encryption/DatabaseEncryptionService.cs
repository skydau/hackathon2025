using Microsoft.Data.SqlClient;

namespace SharedLibrary.Encryption;

/// <summary>
/// Service for managing database encryption features including TDE and Always Encrypted
/// </summary>
public class DatabaseEncryptionService
{
    /// <summary>
    /// Enables Transparent Data Encryption (TDE) on a tenant database
    /// </summary>
    public async Task<bool> EnableTdeAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if TDE is already enabled
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = @"
            SELECT encryption_state 
            FROM sys.dm_database_encryption_keys 
            WHERE database_id = DB_ID(@databaseName)";
        checkCommand.Parameters.AddWithValue("@databaseName", databaseName);

        var encryptionState = await checkCommand.ExecuteScalarAsync(cancellationToken);
        
        if (encryptionState != null && Convert.ToInt32(encryptionState) == 3)
        {
            // TDE is already enabled (state 3 = encrypted)
            return true;
        }

        // Create database encryption key if it doesn't exist
        var createKeyCommand = connection.CreateCommand();
        createKeyCommand.CommandText = $@"
            USE [{databaseName}];
            IF NOT EXISTS (SELECT * FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID())
            BEGIN
                CREATE DATABASE ENCRYPTION KEY
                WITH ALGORITHM = AES_256
                ENCRYPTION BY SERVER CERTIFICATE TenantMasterCert;
            END";
        
        await createKeyCommand.ExecuteNonQueryAsync(cancellationToken);

        // Enable TDE
        var enableTdeCommand = connection.CreateCommand();
        enableTdeCommand.CommandText = $@"
            USE [{databaseName}];
            ALTER DATABASE [{databaseName}]
            SET ENCRYPTION ON;";
        
        await enableTdeCommand.ExecuteNonQueryAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Checks if TDE is enabled on a database
    /// </summary>
    public async Task<bool> IsTdeEnabledAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT encryption_state 
            FROM sys.dm_database_encryption_keys 
            WHERE database_id = DB_ID(@databaseName)";
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        // State 3 = encrypted
        return result != null && Convert.ToInt32(result) == 3;
    }

    /// <summary>
    /// Creates a Column Master Key for Always Encrypted
    /// </summary>
    public async Task CreateColumnMasterKeyAsync(string connectionString, string keyName, string keyPath, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = $@"
            IF NOT EXISTS (SELECT * FROM sys.column_master_keys WHERE name = @keyName)
            BEGIN
                CREATE COLUMN MASTER KEY [{keyName}]
                WITH (
                    KEY_STORE_PROVIDER_NAME = 'AZURE_KEY_VAULT',
                    KEY_PATH = @keyPath
                );
            END";
        
        command.Parameters.AddWithValue("@keyName", keyName);
        command.Parameters.AddWithValue("@keyPath", keyPath);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a Column Encryption Key for Always Encrypted
    /// </summary>
    public async Task CreateColumnEncryptionKeyAsync(string connectionString, string keyName, string masterKeyName, byte[] encryptedValue, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = $@"
            IF NOT EXISTS (SELECT * FROM sys.column_encryption_keys WHERE name = @keyName)
            BEGIN
                CREATE COLUMN ENCRYPTION KEY [{keyName}]
                WITH VALUES (
                    COLUMN_MASTER_KEY = [{masterKeyName}],
                    ALGORITHM = 'RSA_OAEP',
                    ENCRYPTED_VALUE = @encryptedValue
                );
            END";
        
        command.Parameters.AddWithValue("@keyName", keyName);
        command.Parameters.AddWithValue("@encryptedValue", encryptedValue);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if a column is encrypted with Always Encrypted
    /// </summary>
    public async Task<bool> IsColumnEncryptedAsync(string connectionString, string tableName, string columnName, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT c.encryption_type
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            WHERE t.name = @tableName AND c.name = @columnName";
        
        command.Parameters.AddWithValue("@tableName", tableName);
        command.Parameters.AddWithValue("@columnName", columnName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        // encryption_type > 0 means the column is encrypted
        return result != null && Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// Checks if database backups are encrypted
    /// </summary>
    public async Task<bool> IsBackupEncryptedAsync(string connectionString, string databaseName, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TOP 1 encryptor_type
            FROM msdb.dbo.backupset
            WHERE database_name = @databaseName
            ORDER BY backup_finish_date DESC";
        
        command.Parameters.AddWithValue("@databaseName", databaseName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        
        // encryptor_type NULL or empty means not encrypted
        return result != null && !string.IsNullOrEmpty(result.ToString());
    }
}

using Microsoft.Data.SqlClient;

namespace SharedLibrary.Encryption;

/// <summary>
/// Service for managing encrypted database backups
/// </summary>
public class BackupEncryptionService
{
    /// <summary>
    /// Creates an encrypted backup of a tenant database
    /// </summary>
    public async Task<bool> CreateEncryptedBackupAsync(
        string connectionString,
        string databaseName,
        string backupPath,
        string certificateName = "TenantMasterCert",
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandTimeout = 300; // 5 minutes for backup operations

        command.CommandText = $@"
            BACKUP DATABASE [{databaseName}]
            TO DISK = @backupPath
            WITH 
                ENCRYPTION (
                    ALGORITHM = AES_256,
                    SERVER CERTIFICATE = [{certificateName}]
                ),
                COMPRESSION,
                FORMAT,
                INIT,
                NAME = N'{databaseName}-Full Database Backup',
                SKIP,
                NOREWIND,
                NOUNLOAD,
                STATS = 10;";

        command.Parameters.AddWithValue("@backupPath", backupPath);

        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException ex)
        {
            // Log the error
            Console.WriteLine($"Backup failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Verifies that the most recent backup of a database is encrypted
    /// </summary>
    public async Task<bool> VerifyBackupEncryptionAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TOP 1 
                bs.encryptor_type,
                bs.encryptor_thumbprint,
                bs.key_algorithm
            FROM msdb.dbo.backupset bs
            WHERE bs.database_name = @databaseName
            ORDER BY bs.backup_finish_date DESC";

        command.Parameters.AddWithValue("@databaseName", databaseName);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            var encryptorType = reader.IsDBNull(0) ? null : reader.GetString(0);
            
            // If encryptor_type is not null, the backup is encrypted
            // Common values: "SERVER CERTIFICATE", "SERVER ASYMMETRIC KEY"
            return !string.IsNullOrEmpty(encryptorType);
        }

        return false;
    }

    /// <summary>
    /// Gets backup encryption details for a database
    /// </summary>
    public async Task<BackupEncryptionInfo?> GetBackupEncryptionInfoAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TOP 1 
                bs.backup_finish_date,
                bs.encryptor_type,
                bs.encryptor_thumbprint,
                bs.key_algorithm,
                bs.backup_size,
                bs.compressed_backup_size
            FROM msdb.dbo.backupset bs
            WHERE bs.database_name = @databaseName
            ORDER BY bs.backup_finish_date DESC";

        command.Parameters.AddWithValue("@databaseName", databaseName);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        
        if (await reader.ReadAsync(cancellationToken))
        {
            return new BackupEncryptionInfo
            {
                BackupFinishDate = reader.GetDateTime(0),
                EncryptorType = reader.IsDBNull(1) ? null : reader.GetString(1),
                EncryptorThumbprint = reader.IsDBNull(2) ? null : reader.GetValue(2) as byte[],
                KeyAlgorithm = reader.IsDBNull(3) ? null : reader.GetString(3),
                BackupSize = reader.GetInt64(4),
                CompressedBackupSize = reader.GetInt64(5),
                IsEncrypted = !reader.IsDBNull(1)
            };
        }

        return null;
    }
}

/// <summary>
/// Information about a database backup's encryption status
/// </summary>
public class BackupEncryptionInfo
{
    public DateTime BackupFinishDate { get; set; }
    public string? EncryptorType { get; set; }
    public byte[]? EncryptorThumbprint { get; set; }
    public string? KeyAlgorithm { get; set; }
    public long BackupSize { get; set; }
    public long CompressedBackupSize { get; set; }
    public bool IsEncrypted { get; set; }
}

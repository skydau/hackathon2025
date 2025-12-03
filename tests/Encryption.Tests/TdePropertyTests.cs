using FsCheck;
using FsCheck.Xunit;
using Microsoft.Data.SqlClient;
using SharedLibrary.Encryption;
using Xunit;

namespace Encryption.Tests;

/// <summary>
/// Property-based tests for Transparent Data Encryption (TDE)
/// </summary>
public class TdePropertyTests : IDisposable
{
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly string _masterConnectionString;
    private readonly List<string> _createdDatabases;

    public TdePropertyTests()
    {
        _encryptionService = new DatabaseEncryptionService();
        
        // Connection string to master database for creating test databases
        _masterConnectionString = "Server=XIA-JADU-LT\\SQLEXPRESS;Database=master;User Id=sa;Password=md1599;TrustServerCertificate=true;";
        
        _createdDatabases = new List<string>();
    }

    // **Feature: multi-tenant-medical-platform, Property 46: 租户数据库启用TDE**
    // **Validates: Requirements 10.1**
    [Property(MaxTest = 10, Skip = "Requires SQL Server with TDE support")]
    public Property TenantDatabaseEnablesTde()
    {
        return Prop.ForAll(
            GenerateValidDatabaseName(),
            (databaseName) =>
            {
                try
                {
                    // Create test database
                    CreateTestDatabaseAsync(databaseName).Wait();
                    _createdDatabases.Add(databaseName);

                    // Ensure master key and certificate exist
                    EnsureMasterKeyAndCertificateAsync().Wait();

                    // Enable TDE on the database
                    var result = _encryptionService.EnableTdeAsync(_masterConnectionString, databaseName).Result;

                    // Wait a moment for encryption to complete
                    Task.Delay(2000).Wait();

                    // Verify TDE is enabled
                    var isTdeEnabled = _encryptionService.IsTdeEnabledAsync(_masterConnectionString, databaseName).Result;

                    return result && isTdeEnabled;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"TDE test failed: {ex.Message}");
                    return false;
                }
            });
    }

    [Fact(Skip = "Requires SQL Server with TDE support")]
    public async Task TdeEnablement_SingleDatabase_ShouldSucceed()
    {
        // This is a concrete example test for TDE enablement
        var databaseName = "TestTenantDB_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        
        try
        {
            // Create test database
            await CreateTestDatabaseAsync(databaseName);
            _createdDatabases.Add(databaseName);

            // Ensure master key and certificate exist
            await EnsureMasterKeyAndCertificateAsync();

            // Enable TDE
            var result = await _encryptionService.EnableTdeAsync(_masterConnectionString, databaseName);
            Assert.True(result, "TDE enablement should succeed");

            // Wait for encryption to complete
            await Task.Delay(3000);

            // Verify TDE is enabled
            var isTdeEnabled = await _encryptionService.IsTdeEnabledAsync(_masterConnectionString, databaseName);
            Assert.True(isTdeEnabled, "TDE should be enabled on the database");
        }
        finally
        {
            // Cleanup is handled in Dispose
        }
    }

    [Fact(Skip = "Requires SQL Server with TDE support")]
    public async Task TdeStatus_MultipleChecks_ShouldBeConsistent()
    {
        // Test that TDE status checks are consistent
        var databaseName = "TestTenantDB_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        
        try
        {
            await CreateTestDatabaseAsync(databaseName);
            _createdDatabases.Add(databaseName);

            await EnsureMasterKeyAndCertificateAsync();
            await _encryptionService.EnableTdeAsync(_masterConnectionString, databaseName);
            await Task.Delay(3000);

            // Check TDE status multiple times
            var check1 = await _encryptionService.IsTdeEnabledAsync(_masterConnectionString, databaseName);
            var check2 = await _encryptionService.IsTdeEnabledAsync(_masterConnectionString, databaseName);
            var check3 = await _encryptionService.IsTdeEnabledAsync(_masterConnectionString, databaseName);

            Assert.True(check1 && check2 && check3, "TDE status should be consistent across multiple checks");
            Assert.Equal(check1, check2);
            Assert.Equal(check2, check3);
        }
        finally
        {
            // Cleanup is handled in Dispose
        }
    }

    private async Task CreateTestDatabaseAsync(string databaseName)
    {
        using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = $@"
            IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{databaseName}')
            BEGIN
                CREATE DATABASE [{databaseName}];
            END";
        
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureMasterKeyAndCertificateAsync()
    {
        using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();

        // Create master key if it doesn't exist
        var masterKeyCommand = connection.CreateCommand();
        masterKeyCommand.CommandText = @"
            USE master;
            IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
            BEGIN
                CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'StrongP@ssw0rd!2024';
            END";
        
        try
        {
            await masterKeyCommand.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Master key might already exist
        }

        // Create certificate if it doesn't exist
        var certCommand = connection.CreateCommand();
        certCommand.CommandText = @"
            USE master;
            IF NOT EXISTS (SELECT * FROM sys.certificates WHERE name = 'TenantMasterCert')
            BEGIN
                CREATE CERTIFICATE TenantMasterCert
                WITH SUBJECT = 'Tenant Database TDE Certificate',
                EXPIRY_DATE = '2034-12-31';
            END";
        
        try
        {
            await certCommand.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Certificate might already exist
        }
    }

    private static Arbitrary<string> GenerateValidDatabaseName()
    {
        var gen = from suffix in Gen.Choose(1000, 9999)
                  select $"TestTenantDB_{suffix}";
        
        return Arb.From(gen);
    }

    public void Dispose()
    {
        // Clean up test databases
        foreach (var dbName in _createdDatabases)
        {
            try
            {
                using var connection = new SqlConnection(_masterConnectionString);
                connection.Open();

                // Disable TDE first
                var disableTdeCommand = connection.CreateCommand();
                disableTdeCommand.CommandText = $@"
                    USE master;
                    IF EXISTS (SELECT * FROM sys.databases WHERE name = '{dbName}')
                    BEGIN
                        ALTER DATABASE [{dbName}] SET ENCRYPTION OFF;
                    END";
                disableTdeCommand.ExecuteNonQuery();

                // Wait a moment
                Thread.Sleep(1000);

                // Drop the database
                var dropCommand = connection.CreateCommand();
                dropCommand.CommandText = $@"
                    USE master;
                    IF EXISTS (SELECT * FROM sys.databases WHERE name = '{dbName}')
                    BEGIN
                        ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                        DROP DATABASE [{dbName}];
                    END";
                dropCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup database {dbName}: {ex.Message}");
            }
        }
    }
}

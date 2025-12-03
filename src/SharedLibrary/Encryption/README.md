# Data Encryption Implementation

This directory contains the implementation of data encryption features for the multi-tenant medical platform, including Transparent Data Encryption (TDE), Always Encrypted, and encrypted backups.

## Overview

The encryption implementation provides three layers of data protection:

1. **Transparent Data Encryption (TDE)** - Encrypts database files at rest
2. **Always Encrypted** - Client-side encryption for highly sensitive columns
3. **Encrypted Backups** - Ensures backup files are encrypted

## Components

### DatabaseEncryptionService

Main service for managing database encryption features.

**Key Methods:**
- `EnableTdeAsync()` - Enables TDE on a tenant database
- `IsTdeEnabledAsync()` - Checks if TDE is enabled
- `CreateColumnMasterKeyAsync()` - Creates CMK for Always Encrypted
- `CreateColumnEncryptionKeyAsync()` - Creates CEK for Always Encrypted
- `IsColumnEncryptedAsync()` - Checks if a column uses Always Encrypted
- `IsBackupEncryptedAsync()` - Verifies backup encryption

### AlwaysEncryptedHelper

Helper class for working with Always Encrypted columns.

**Key Methods:**
- `CreateEncryptedConnection()` - Creates a connection with Always Encrypted enabled
- `InsertEncryptedDataAsync()` - Inserts data with automatic client-side encryption
- `QueryEncryptedDataAsync()` - Queries encrypted data with automatic decryption
- `CanAccessEncryptedDataAsync()` - Tests access control (should fail without keys)

### BackupEncryptionService

Service for creating and verifying encrypted database backups.

**Key Methods:**
- `CreateEncryptedBackupAsync()` - Creates an encrypted backup
- `VerifyBackupEncryptionAsync()` - Verifies backup is encrypted
- `GetBackupEncryptionInfoAsync()` - Gets detailed encryption information

## SQL Scripts

### EnableTDE.sql

Script to enable Transparent Data Encryption on a tenant database.

**Steps:**
1. Create master key in master database
2. Create server certificate
3. Create database encryption key in tenant database
4. Enable TDE
5. Verify encryption status

**Important:** The certificate must be backed up for disaster recovery!

### ConfigureAlwaysEncrypted.sql

Script to configure Always Encrypted for sensitive columns.

**Steps:**
1. Create Column Master Key (CMK) pointing to Azure Key Vault
2. Create Column Encryption Key (CEK) encrypted by CMK
3. Create tables with encrypted columns
4. Verify encryption configuration

## Usage Examples

### Enabling TDE on a Tenant Database

```csharp
var encryptionService = new DatabaseEncryptionService();
var connectionString = "Server=XIA-JADU-LT\\SQLEXPRESS;Database=master;User Id=sa;Password=md1599;TrustServerCertificate=true;";

// Enable TDE
await encryptionService.EnableTdeAsync(connectionString, "TenantDB_Hospital_A");

// Verify TDE is enabled
var isTdeEnabled = await encryptionService.IsTdeEnabledAsync(connectionString, "TenantDB_Hospital_A");
Console.WriteLine($"TDE Enabled: {isTdeEnabled}");
```

### Working with Always Encrypted

```csharp
var connectionString = "Server=XIA-JADU-LT\\SQLEXPRESS;Database=TenantDB;User Id=sa;Password=md1599;TrustServerCertificate=true;";

// Create connection with Always Encrypted enabled
var connection = AlwaysEncryptedHelper.CreateEncryptedConnection(connectionString);
await connection.OpenAsync();

// Insert encrypted data (encryption happens automatically on client side)
var patientData = new Dictionary<string, object>
{
    { "PatientName", "John Doe" },
    { "SSN", "123-45-6789" },
    { "MedicalHistory", "Sensitive medical information" }
};

await AlwaysEncryptedHelper.InsertEncryptedDataAsync(connection, "PatientRecords", patientData);

// Query encrypted data (decryption happens automatically if client has keys)
var results = await AlwaysEncryptedHelper.QueryEncryptedDataAsync(
    connection,
    "SELECT PatientName, SSN FROM PatientRecords WHERE Id = @Id",
    new Dictionary<string, object> { { "Id", 1 } }
);
```

### Creating Encrypted Backups

```csharp
var backupService = new BackupEncryptionService();
var connectionString = "Server=XIA-JADU-LT\\SQLEXPRESS;Database=master;User Id=sa;Password=md1599;TrustServerCertificate=true;";

// Create encrypted backup
var backupPath = "C:\\Backups\\TenantDB_Hospital_A_backup.bak";
await backupService.CreateEncryptedBackupAsync(connectionString, "TenantDB_Hospital_A", backupPath);

// Verify backup is encrypted
var isEncrypted = await backupService.VerifyBackupEncryptionAsync(connectionString, "TenantDB_Hospital_A");
Console.WriteLine($"Backup Encrypted: {isEncrypted}");

// Get detailed encryption info
var backupInfo = await backupService.GetBackupEncryptionInfoAsync(connectionString, "TenantDB_Hospital_A");
Console.WriteLine($"Encryption Algorithm: {backupInfo.KeyAlgorithm}");
Console.WriteLine($"Encryptor Type: {backupInfo.EncryptorType}");
```

## Property-Based Tests

The implementation includes comprehensive property-based tests that verify:

### Property 46: Tenant Database Enables TDE
*For any* new tenant database, TDE should be successfully enabled and verifiable.

### Property 47: Sensitive Fields Use Always Encrypted
*For any* column marked as sensitive, the system should use Always Encrypted for client-side encryption.

### Property 48: Encrypted Data Access Control
*For any* Always Encrypted data, only clients with proper keys should be able to decrypt the data.

### Property 49: Backup Files Are Encrypted
*For any* database backup, the backup file should be encrypted when TDE is enabled.

## Requirements Validation

This implementation validates the following requirements:

- **Requirement 10.1**: TDE is enabled on tenant databases
- **Requirement 10.3**: Sensitive fields use Always Encrypted
- **Requirement 10.4**: Only clients with correct keys can decrypt Always Encrypted data
- **Requirement 10.5**: Database backups are encrypted

## Prerequisites

### For TDE:
- SQL Server Enterprise, Developer, or Evaluation Edition
- Master key and certificate in master database
- Appropriate permissions to enable encryption

### For Always Encrypted:
- SQL Server 2016 or later
- Azure Key Vault for storing Column Master Keys
- Client driver with Always Encrypted support
- Appropriate Azure permissions

### For Encrypted Backups:
- TDE enabled on the database
- Backup permissions
- Sufficient disk space for backups

## Security Considerations

1. **Certificate Backup**: Always backup the TDE certificate and private key. Without it, you cannot restore encrypted databases.

2. **Key Management**: Store Column Master Keys in Azure Key Vault, not in the database.

3. **Access Control**: Limit access to encryption keys using Azure RBAC and SQL Server permissions.

4. **Key Rotation**: Implement regular key rotation policies (recommended every 90 days).

5. **Audit Logging**: Enable audit logging for all encryption-related operations.

## Compliance

This implementation helps meet compliance requirements for:

- **HIPAA**: Encryption of PHI at rest and in transit
- **GDPR**: Protection of personal data
- **SOC 2**: Data encryption controls

## Testing

Run the encryption tests:

```bash
dotnet test tests/Encryption.Tests/Encryption.Tests.csproj
```

Note: Tests are skipped by default as they require SQL Server with TDE and Always Encrypted configured. Remove the `Skip` attribute to run tests in a properly configured environment.

## Troubleshooting

### TDE Enablement Fails

**Issue**: "Cannot find the certificate 'TenantMasterCert'"
**Solution**: Run the EnableTDE.sql script to create the master key and certificate first.

### Always Encrypted Connection Fails

**Issue**: "Column encryption key cannot be decrypted"
**Solution**: Ensure the client has access to the Column Master Key in Azure Key Vault.

### Backup Encryption Fails

**Issue**: "Cannot create encrypted backup without TDE"
**Solution**: Enable TDE on the database before creating encrypted backups.

## Future Enhancements

1. Automatic key rotation
2. Integration with Azure Key Vault for TDE certificate storage
3. Support for multiple encryption algorithms
4. Encryption performance monitoring
5. Automated compliance reporting

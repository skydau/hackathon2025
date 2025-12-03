-- Script to enable Transparent Data Encryption (TDE) on a tenant database
-- This script should be run on the master database first to create the certificate,
-- then on the tenant database to enable encryption

-- Step 1: Create master key in master database (run once per SQL Server instance)
USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'StrongP@ssw0rd!2024';
END
GO

-- Step 2: Create server certificate (run once per SQL Server instance)
IF NOT EXISTS (SELECT * FROM sys.certificates WHERE name = 'TenantMasterCert')
BEGIN
    CREATE CERTIFICATE TenantMasterCert
    WITH SUBJECT = 'Tenant Database TDE Certificate',
    EXPIRY_DATE = '2034-12-31';
END
GO

-- Step 3: Backup the certificate (IMPORTANT for disaster recovery)
-- BACKUP CERTIFICATE TenantMasterCert
-- TO FILE = '/var/opt/mssql/backup/TenantMasterCert.cer'
-- WITH PRIVATE KEY (
--     FILE = '/var/opt/mssql/backup/TenantMasterCert.pvk',
--     ENCRYPTION BY PASSWORD = 'StrongP@ssw0rd!2024'
-- );
-- GO

-- Step 4: Create database encryption key in tenant database
-- Replace [TenantDatabaseName] with actual tenant database name
USE [TenantDatabaseName];
GO

IF NOT EXISTS (SELECT * FROM sys.dm_database_encryption_keys WHERE database_id = DB_ID())
BEGIN
    CREATE DATABASE ENCRYPTION KEY
    WITH ALGORITHM = AES_256
    ENCRYPTION BY SERVER CERTIFICATE TenantMasterCert;
END
GO

-- Step 5: Enable TDE
ALTER DATABASE [TenantDatabaseName]
SET ENCRYPTION ON;
GO

-- Step 6: Verify TDE is enabled
-- State 3 = encrypted
SELECT 
    db.name AS DatabaseName,
    dek.encryption_state AS EncryptionState,
    CASE dek.encryption_state
        WHEN 0 THEN 'No database encryption key present, no encryption'
        WHEN 1 THEN 'Unencrypted'
        WHEN 2 THEN 'Encryption in progress'
        WHEN 3 THEN 'Encrypted'
        WHEN 4 THEN 'Key change in progress'
        WHEN 5 THEN 'Decryption in progress'
        WHEN 6 THEN 'Protection change in progress'
    END AS EncryptionStateDescription,
    dek.percent_complete AS PercentComplete,
    dek.encryptor_type AS EncryptorType
FROM sys.dm_database_encryption_keys dek
INNER JOIN sys.databases db ON dek.database_id = db.database_id
WHERE db.name = 'TenantDatabaseName';
GO

-- SQL Server配置脚本
-- 此脚本用于配置本地SQL Server以支持多租户平台

USE master;
GO

-- 1. 启用SQL Server认证
PRINT 'Configuring SQL Server authentication...';
EXEC xp_instance_regwrite 
    N'HKEY_LOCAL_MACHINE', 
    N'Software\Microsoft\MSSQLServer\MSSQLServer', 
    N'LoginMode', 
    REG_DWORD, 
    2;
GO

-- 2. 启用sa账户（如果未启用）
IF EXISTS (SELECT * FROM sys.server_principals WHERE name = 'sa' AND is_disabled = 1)
BEGIN
    ALTER LOGIN sa ENABLE;
    PRINT 'SA account enabled';
END
ELSE
BEGIN
    PRINT 'SA account already enabled';
END
GO

-- 3. 设置sa密码（请修改为强密码）
-- ALTER LOGIN sa WITH PASSWORD = 'YourStrongPassword123!';
-- GO

-- 4. 创建TDE主密钥（用于透明数据加密）
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'MasterKeyPassword123!';
    PRINT 'Master key created successfully';
END
ELSE
BEGIN
    PRINT 'Master key already exists';
END
GO

-- 5. 创建TDE证书
IF NOT EXISTS (SELECT * FROM sys.certificates WHERE name = 'TDE_Cert')
BEGIN
    CREATE CERTIFICATE TDE_Cert 
    WITH SUBJECT = 'TDE Certificate for MedLogic Platform';
    PRINT 'TDE certificate created successfully';
END
ELSE
BEGIN
    PRINT 'TDE certificate already exists';
END
GO

-- 6. 创建平台数据库（用于Tenant Catalog和Device Registry）
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'MedLogicPlatform')
BEGIN
    CREATE DATABASE MedLogicPlatform
    COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'MedLogicPlatform database created successfully';
END
ELSE
BEGIN
    PRINT 'MedLogicPlatform database already exists';
END
GO

-- 7. 在平台数据库中创建Tenants表
USE MedLogicPlatform;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE Tenants (
        Id NVARCHAR(50) PRIMARY KEY,
        DisplayName NVARCHAR(200) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        DbServer NVARCHAR(200) NOT NULL,
        DbDatabase NVARCHAR(100) NOT NULL,
        DbUsername NVARCHAR(100) NOT NULL,
        DbPasswordRef NVARCHAR(200) NOT NULL,
        ThrottlingRps INT NOT NULL DEFAULT 100,
        SloAvailability NVARCHAR(20),
        SloP95LatencyMs INT,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Tenants_Status ON Tenants(Status);
    PRINT 'Tenants table created successfully';
END
ELSE
BEGIN
    PRINT 'Tenants table already exists';
END
GO

-- 8. 在平台数据库中创建Devices表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Devices')
BEGIN
    CREATE TABLE Devices (
        SerialNumber NVARCHAR(100) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL,
        DeviceType NVARCHAR(50) NOT NULL,
        RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        LastSeenAt DATETIME2,
        FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
    );
    CREATE INDEX IX_Devices_TenantId ON Devices(TenantId);
    PRINT 'Devices table created successfully';
END
ELSE
BEGIN
    PRINT 'Devices table already exists';
END
GO

-- 9. 创建ResourceUsage表（用于成本分摊）
USE MedLogicPlatform;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ResourceUsage')
BEGIN
    CREATE TABLE ResourceUsage (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL,
        CpuUsage DECIMAL(18,4),
        MemoryUsageMB DECIMAL(18,4),
        StorageUsageGB DECIMAL(18,4),
        NetworkUsageGB DECIMAL(18,4),
        RecordedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
    );
    CREATE INDEX IX_ResourceUsage_TenantId ON ResourceUsage(TenantId);
    CREATE INDEX IX_ResourceUsage_RecordedAt ON ResourceUsage(RecordedAt);
    PRINT 'ResourceUsage table created successfully';
END
ELSE
BEGIN
    PRINT 'ResourceUsage table already exists';
END
GO

-- 10. 创建CostReports表（用于成本报告）
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CostReports')
BEGIN
    CREATE TABLE CostReports (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL,
        ReportMonth NVARCHAR(7) NOT NULL, -- 格式: YYYY-MM
        CpuCost DECIMAL(18,2),
        MemoryCost DECIMAL(18,2),
        StorageCost DECIMAL(18,2),
        NetworkCost DECIMAL(18,2),
        TotalCost DECIMAL(18,2),
        GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
    );
    CREATE INDEX IX_CostReports_TenantId ON CostReports(TenantId);
    CREATE INDEX IX_CostReports_ReportMonth ON CostReports(ReportMonth);
    PRINT 'CostReports table created successfully';
END
ELSE
BEGIN
    PRINT 'CostReports table already exists';
END
GO

-- 11. 验证配置
PRINT '';
PRINT '=== Configuration Summary ===';
PRINT 'SQL Server Authentication: Enabled';
SELECT 'SA Account Status: ' + CASE WHEN is_disabled = 0 THEN 'Enabled' ELSE 'Disabled' END
FROM sys.server_principals WHERE name = 'sa';
SELECT 'Master Key: ' + CASE WHEN COUNT(*) > 0 THEN 'Created' ELSE 'Not Created' END
FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##';
SELECT 'TDE Certificate: ' + CASE WHEN COUNT(*) > 0 THEN 'Created' ELSE 'Not Created' END
FROM sys.certificates WHERE name = 'TDE_Cert';
SELECT 'Platform Database: ' + CASE WHEN COUNT(*) > 0 THEN 'Created' ELSE 'Not Created' END
FROM sys.databases WHERE name = 'MedLogicPlatform';
GO

USE MedLogicPlatform;
GO

PRINT '';
PRINT '=== Database Tables ===';
SELECT 'Table: ' + name FROM sys.tables WHERE name IN ('Tenants', 'Devices', 'ResourceUsage', 'CostReports');
GO

PRINT '';
PRINT 'SQL Server configuration completed successfully!';
PRINT '';
PRINT 'IMPORTANT NEXT STEPS:';
PRINT '1. Restart SQL Server for authentication mode changes to take effect';
PRINT '2. Update the SA password using:';
PRINT '   ALTER LOGIN sa WITH PASSWORD = ''YourStrongPassword123!'';';
PRINT '3. Create a Kubernetes Secret with SQL Server admin credentials:';
PRINT '   kubectl create secret generic sqlserver-admin-secret \';
PRINT '     --from-literal=username=sa \';
PRINT '     --from-literal=password=YourStrongPassword123! \';
PRINT '     -n platform-system';
PRINT '';
PRINT '=== Verification Queries ===';
PRINT 'To verify the setup, run:';
PRINT '  SELECT * FROM MedLogicPlatform.sys.tables;';
PRINT '  SELECT name, is_disabled FROM sys.server_principals WHERE name = ''sa'';';
GO

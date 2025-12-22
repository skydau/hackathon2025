-- 初始化 MedLogic 平台数据库
-- 此脚本在 SQL Server 容器启动时自动执行

-- 等待 SQL Server 完全启动
WAITFOR DELAY '00:00:10';

-- 创建平台数据库
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'MedLogicPlatform')
BEGIN
    CREATE DATABASE MedLogicPlatform
    COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'MedLogicPlatform 数据库创建成功';
END
ELSE
BEGIN
    PRINT 'MedLogicPlatform 数据库已存在';
END
GO

-- 使用平台数据库
USE MedLogicPlatform;
GO

-- 创建 Tenants 表
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
        ThrottlingRps INT,
        SloAvailability NVARCHAR(20),
        SloP95LatencyMs INT,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Tenants_Status ON Tenants(Status);
    PRINT 'Tenants 表创建成功';
END
GO

-- 创建 ResourceUsage 表
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
    PRINT 'ResourceUsage 表创建成功';
END
GO

-- 创建 CostReports 表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CostReports')
BEGIN
    CREATE TABLE CostReports (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        TenantId NVARCHAR(50) NOT NULL,
        ReportMonth NVARCHAR(7) NOT NULL,
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
    PRINT 'CostReports 表创建成功';
END
GO

PRINT '数据库初始化完成！';
GO
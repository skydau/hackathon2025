-- 租户数据库初始化脚本
-- 此脚本由Tenant Operator在创建租户数据库后自动执行

-- 创建Transactions表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
BEGIN
    CREATE TABLE Transactions (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Amount DECIMAL(18,2) NOT NULL,
        Description NVARCHAR(500),
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Transactions_CreatedAt ON Transactions(CreatedAt);
    PRINT 'Transactions table created successfully';
END
ELSE
BEGIN
    PRINT 'Transactions table already exists';
END

-- 创建AuditLogs表
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLogs')
BEGIN
    CREATE TABLE AuditLogs (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Action NVARCHAR(100) NOT NULL,
        UserId NVARCHAR(100),
        Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Details NVARCHAR(MAX)
    );
    CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
    PRINT 'AuditLogs table created successfully';
END
ELSE
BEGIN
    PRINT 'AuditLogs table already exists';
END

PRINT 'Database initialization completed successfully';

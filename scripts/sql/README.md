# SQL 脚本说明

## init-tenant-db.sql

租户数据库初始化脚本，用于在创建新租户数据库时自动执行。

### 功能

该脚本创建以下数据库对象：

#### 1. Transactions 表
用于存储交易记录。

**字段**:
- `Id` (BIGINT): 主键，自增
- `Amount` (DECIMAL(18,2)): 交易金额
- `Description` (NVARCHAR(500)): 交易描述
- `CreatedAt` (DATETIME2): 创建时间，默认为当前UTC时间

**索引**:
- `IX_Transactions_CreatedAt`: 在 CreatedAt 字段上创建索引，优化按时间查询

#### 2. AuditLogs 表
用于存储审计日志。

**字段**:
- `Id` (BIGINT): 主键，自增
- `Action` (NVARCHAR(100)): 操作类型
- `UserId` (NVARCHAR(100)): 用户ID
- `Timestamp` (DATETIME2): 时间戳，默认为当前UTC时间
- `Details` (NVARCHAR(MAX)): 详细信息

**索引**:
- `IX_AuditLogs_Timestamp`: 在 Timestamp 字段上创建索引，优化按时间查询

### 使用方式

#### 方式1: 嵌入到 Tenant Operator（当前实现）

脚本已嵌入到 `tenant-operator/pkg/database/provisioner.go` 的 `executeInitScript` 方法中。
当 Tenant Operator 创建新租户数据库时，会自动执行此脚本。

#### 方式2: 手动执行

如果需要手动初始化租户数据库，可以使用以下命令：

```bash
# 使用 sqlcmd
sqlcmd -S localhost -U sa -P "YourPassword" -d HospitalA_DB -i init-tenant-db.sql

# 使用 SQL Server Management Studio (SSMS)
# 1. 连接到 SQL Server
# 2. 选择目标数据库
# 3. 打开 init-tenant-db.sql 文件
# 4. 执行脚本
```

### 幂等性

脚本使用 `IF NOT EXISTS` 检查，确保可以安全地多次执行而不会出错。
如果表已存在，脚本会跳过创建步骤并输出相应消息。

### 验证

执行脚本后，可以使用以下查询验证表和索引是否创建成功：

```sql
-- 检查表是否存在
SELECT name FROM sys.tables WHERE name IN ('Transactions', 'AuditLogs');

-- 检查索引是否存在
SELECT 
    t.name AS TableName,
    i.name AS IndexName,
    c.name AS ColumnName
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IN ('IX_Transactions_CreatedAt', 'IX_AuditLogs_Timestamp');
```

### 相关需求

- **需求 2.7**: 租户数据库被创建时，Tenant Operator 应执行数据库初始化脚本创建必需的表结构
- **设计文档**: 参见 `.kiro/specs/multi-tenant-medical-platform/design.md` 中的"数据库自动创建服务"章节

### 维护说明

如果需要修改表结构或添加新表：

1. 更新 `init-tenant-db.sql` 文件
2. 同步更新 `tenant-operator/pkg/database/provisioner.go` 中的 `executeInitScript` 方法
3. 确保使用 `IF NOT EXISTS` 保持幂等性
4. 更新本 README 文档
5. 运行相关测试验证更改

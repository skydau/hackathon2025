# 数据库自动创建功能

## 概述

Database Provisioner 负责为每个租户自动创建独立的SQL Server数据库实例。当Tenant CRD被创建时，Tenant Operator会自动：

1. 连接到本地SQL Server（通过`host.minikube.internal`）
2. 为租户创建独立的数据库
3. 创建数据库用户并配置权限
4. 执行初始化脚本创建表结构
5. 将数据库凭据存储到Kubernetes Secret
6. 将数据库配置注册到Tenant Catalog Service

## 前置条件

### 1. SQL Server配置

在宿主机上安装并配置SQL Server：

**Windows:**
```powershell
# 安装SQL Server Express/Developer Edition
# 下载地址: https://www.microsoft.com/sql-server/sql-server-downloads

# 运行配置脚本
sqlcmd -S localhost -E -i scripts/sql/setup-sqlserver.sql

# 设置sa密码
sqlcmd -S localhost -E -Q "ALTER LOGIN sa WITH PASSWORD = 'YourStrongPassword123!';"
```

**Linux/macOS (Docker):**
```bash
# 启动SQL Server容器
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrongPassword123!" \
  -p 1433:1433 --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2019-latest

# 运行配置脚本
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!" \
  -i /scripts/sql/setup-sqlserver.sql
```

### 2. 防火墙配置

确保宿主机防火墙允许端口1433的入站连接：

**Windows:**
```powershell
netsh advfirewall firewall add rule name="SQL Server" dir=in action=allow protocol=TCP localport=1433
```

**Linux (firewalld):**
```bash
firewall-cmd --permanent --add-port=1433/tcp
firewall-cmd --reload
```

**Linux (ufw):**
```bash
ufw allow 1433/tcp
```

### 3. Kubernetes Secret配置

创建包含SQL Server管理员凭据的Secret：

```bash
kubectl create namespace platform-system

kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password=YourStrongPassword123! \
  --from-literal=server=host.minikube.internal,1433 \
  -n platform-system
```

### 4. Tenant Operator环境变量

在Tenant Operator的Deployment中配置以下环境变量：

```yaml
env:
  - name: SQL_SERVER_HOST
    value: "host.minikube.internal,1433"
  - name: SQL_SERVER_ADMIN_USER
    valueFrom:
      secretKeyRef:
        name: sqlserver-admin-secret
        key: username
  - name: SQL_SERVER_ADMIN_PASSWORD
    valueFrom:
      secretKeyRef:
        name: sqlserver-admin-secret
        key: password
  - name: TENANT_CATALOG_URL
    value: "http://tenant-catalog.platform-system.svc.cluster.local:8080"
```

## 使用方法

### 创建租户

创建Tenant CRD时，指定数据库配置：

```yaml
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: hospital-a
spec:
  displayName: "Hospital A"
  db:
    mode: perDatabase
    server: "host.minikube.internal,1433"
    database: HospitalA_DB  # 可选，不指定则自动生成
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
```

应用配置：

```bash
kubectl apply -f hospital-a.yaml
```

### 监控创建进度

查看Tenant状态：

```bash
# 查看Tenant CRD状态
kubectl get tenant hospital-a -o yaml

# 查看Operator日志
kubectl logs -n platform-system -l app=tenant-operator --tail=100 -f

# 查看租户命名空间
kubectl get namespace tenant-hospital-a

# 查看数据库Secret
kubectl get secret -n tenant-hospital-a tenant-hospital-a-db-secret -o yaml
```

### 验证数据库创建

连接到SQL Server验证数据库：

```bash
# 查看所有租户数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" \
  -Q "SELECT name FROM sys.databases WHERE name LIKE '%_DB'"

# 查看数据库表结构
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" \
  -d HospitalA_DB \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"

# 测试租户用户连接
sqlcmd -S localhost -U hospital_a_user -P "<password_from_secret>" \
  -d HospitalA_DB \
  -Q "SELECT @@VERSION"
```

## 数据库结构

每个租户数据库包含以下表：

### Transactions表
```sql
CREATE TABLE Transactions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Amount DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_Transactions_CreatedAt ON Transactions(CreatedAt);
```

### AuditLogs表
```sql
CREATE TABLE AuditLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    Action NVARCHAR(100) NOT NULL,
    UserId NVARCHAR(100),
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Details NVARCHAR(MAX)
);
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
```

## 安全特性

### 1. 透明数据加密 (TDE)

如果SQL Server配置了TDE证书，Database Provisioner会自动为租户数据库启用TDE：

```sql
CREATE DATABASE ENCRYPTION KEY
WITH ALGORITHM = AES_256
ENCRYPTION BY SERVER CERTIFICATE TDE_Cert;

ALTER DATABASE [HospitalA_DB] SET ENCRYPTION ON;
```

### 2. 用户权限隔离

每个租户拥有独立的数据库用户，仅对自己的数据库拥有db_owner权限：

```sql
CREATE LOGIN [hospital_a_user] WITH PASSWORD = '<secure_password>';
CREATE USER [hospital_a_user] FOR LOGIN [hospital_a_user];
ALTER ROLE db_owner ADD MEMBER [hospital_a_user];
```

### 3. 凭据管理

数据库密码存储在Kubernetes Secret中，不会出现在配置文件或日志中：

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: tenant-hospital-a-db-secret
  namespace: tenant-hospital-a
type: Opaque
stringData:
  username: hospital_a_user
  password: <auto_generated_secure_password>
  server: host.minikube.internal,1433
  database: HospitalA_DB
```

## 错误处理

### 数据库创建失败

如果数据库创建失败，Tenant CRD的status会更新为Failed：

```yaml
status:
  phase: Failed
  databaseCreated: false
  conditions:
    - type: DatabaseProvisioned
      status: "False"
      reason: ProvisioningFailed
      message: "Failed to provision database: <error_details>"
```

查看详细错误信息：

```bash
kubectl get tenant hospital-a -o jsonpath='{.status.conditions[?(@.type=="DatabaseProvisioned")].message}'
```

### 回滚机制

如果数据库创建过程中出现错误，Database Provisioner会自动回滚：

1. 删除部分创建的数据库
2. 清理已创建的Kubernetes Secret
3. 更新Tenant CRD状态为Failed

### 手动重试

修复问题后，可以通过更新Tenant CRD触发重新协调：

```bash
kubectl annotate tenant hospital-a retry=true --overwrite
```

## 故障排查

### 问题1: 无法连接SQL Server

**症状:** Operator日志显示 "failed to ping SQL Server"

**解决方案:**
```bash
# 1. 验证host.minikube.internal解析
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup host.minikube.internal

# 2. 测试端口连接
kubectl run -it --rm debug --image=busybox --restart=Never -- telnet host.minikube.internal 1433

# 3. 检查SQL Server是否监听1433端口
netstat -an | grep 1433  # Linux/macOS
netstat -an | findstr 1433  # Windows

# 4. 检查防火墙规则
# Windows: netsh advfirewall firewall show rule name="SQL Server"
# Linux: sudo ufw status
```

### 问题2: 认证失败

**症状:** "Login failed for user 'sa'"

**解决方案:**
```bash
# 1. 验证SQL Server认证模式
sqlcmd -S localhost -E -Q "SELECT SERVERPROPERTY('IsIntegratedSecurityOnly')"
# 应该返回0（混合模式）

# 2. 验证sa账户状态
sqlcmd -S localhost -E -Q "SELECT name, is_disabled FROM sys.server_principals WHERE name = 'sa'"

# 3. 重置sa密码
sqlcmd -S localhost -E -Q "ALTER LOGIN sa WITH PASSWORD = 'YourStrongPassword123!';"

# 4. 更新Kubernetes Secret
kubectl delete secret sqlserver-admin-secret -n platform-system
kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password=YourStrongPassword123! \
  -n platform-system
```

### 问题3: TDE启用失败

**症状:** "TDE certificate 'TDE_Cert' not found"

**解决方案:**
```bash
# 创建TDE证书
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
USE master;
CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'MasterKeyPassword123!';
CREATE CERTIFICATE TDE_Cert WITH SUBJECT = 'TDE Certificate';
"

# 验证证书
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
SELECT name, subject FROM sys.certificates WHERE name = 'TDE_Cert'
"
```

### 问题4: 数据库已存在

**症状:** "Database 'HospitalA_DB' already exists"

**解决方案:**

Database Provisioner具有幂等性，会自动跳过已存在的数据库。如果需要重新创建：

```bash
# 1. 手动删除数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
ALTER DATABASE [HospitalA_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [HospitalA_DB];
"

# 2. 删除Kubernetes Secret
kubectl delete secret tenant-hospital-a-db-secret -n tenant-hospital-a

# 3. 更新Tenant CRD触发重新创建
kubectl annotate tenant hospital-a retry=true --overwrite
```

## 性能优化

### 连接池管理

Database Provisioner使用单个管理员连接执行所有数据库操作。在生产环境中，建议：

1. 使用连接池限制并发数据库创建
2. 实现队列机制避免SQL Server过载
3. 配置合理的超时时间

### 批量创建

如果需要批量创建租户，建议：

1. 分批创建，避免同时创建过多数据库
2. 监控SQL Server资源使用情况
3. 使用Kubernetes Job而非直接创建多个Tenant CRD

## 生产环境考虑

### 1. 高可用性

- 使用SQL Server Always On可用性组
- 配置自动故障转移
- 实现数据库备份和恢复策略

### 2. 监控告警

- 监控数据库创建成功率
- 监控数据库磁盘使用情况
- 配置数据库创建失败告警

### 3. 安全加固

- 定期轮换数据库密码
- 启用SQL Server审计
- 限制管理员账户访问
- 使用Azure Key Vault存储凭据（生产环境）

### 4. 容量规划

- 预估每个租户数据库的大小
- 规划SQL Server磁盘容量
- 配置数据库自动增长策略
- 实现数据归档和清理机制

## 参考资料

- [SQL Server TDE文档](https://docs.microsoft.com/sql/relational-databases/security/encryption/transparent-data-encryption)
- [Kubernetes Secret管理](https://kubernetes.io/docs/concepts/configuration/secret/)
- [Minikube网络配置](https://minikube.sigs.k8s.io/docs/handbook/host-access/)

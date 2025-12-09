# 数据库自动创建功能部署指南

## 概述

本文档介绍如何配置和使用Tenant Operator的数据库自动创建功能。该功能允许在创建Tenant CRD时自动为每个租户创建独立的SQL Server数据库实例。

## 架构说明

```
┌─────────────────────────────────────────────────────────────┐
│                    Minikube Cluster                          │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  Tenant Operator                                    │    │
│  │  - 监听Tenant CRD创建事件                            │    │
│  │  - 调用DatabaseProvisioner                          │    │
│  │  - 更新Tenant状态                                    │    │
│  └────────────────┬───────────────────────────────────┘    │
│                   │                                          │
│                   │ host.minikube.internal                   │
└───────────────────┼──────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────┐
│              宿主机 SQL Server                                │
│                                                              │
│  ┌────────────────────────────────────────────────────┐    │
│  │  DatabaseProvisioner                                │    │
│  │  1. 创建数据库 (CREATE DATABASE)                     │    │
│  │  2. 启用TDE加密 (可选)                               │    │
│  │  3. 创建用户 (CREATE LOGIN/USER)                     │    │
│  │  4. 执行初始化脚本 (CREATE TABLE)                    │    │
│  │  5. 创建K8s Secret                                   │    │
│  │  6. 注册到Tenant Catalog                             │    │
│  └────────────────────────────────────────────────────┘    │
│                                                              │
│  数据库:                                                     │
│  - MedLogicPlatform (平台数据库)                            │
│  - HospitalA_DB (租户A数据库)                               │
│  - HospitalB_DB (租户B数据库)                               │
└─────────────────────────────────────────────────────────────┘
```

## 前置条件

### 1. 安装SQL Server

#### Windows

1. 下载并安装SQL Server Express或Developer Edition
   - 下载地址: https://www.microsoft.com/sql-server/sql-server-downloads

2. 在安装过程中选择"混合模式"认证

3. 设置sa账户密码（强密码）

#### Linux/macOS (使用Docker)

```bash
# 启动SQL Server容器
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrongPassword123!" \
  -p 1433:1433 \
  --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2019-latest

# 验证SQL Server运行状态
docker ps | grep sqlserver

# 测试连接
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!" \
  -Q "SELECT @@VERSION"
```

### 2. 配置SQL Server

运行配置脚本创建必要的数据库和证书：

```bash
# Windows
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -i scripts\sql\setup-sqlserver.sql

# Linux/macOS (Docker)
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!" \
  -i /scripts/sql/setup-sqlserver.sql
```

配置脚本会执行以下操作：
- 启用SQL Server认证
- 创建TDE主密钥和证书
- 创建MedLogicPlatform平台数据库
- 创建Tenants和Devices表

### 3. 配置防火墙

确保宿主机防火墙允许端口1433的入站连接：

#### Windows
```powershell
netsh advfirewall firewall add rule name="SQL Server" dir=in action=allow protocol=TCP localport=1433
```

#### Linux (firewalld)
```bash
sudo firewall-cmd --permanent --add-port=1433/tcp
sudo firewall-cmd --reload
```

#### Linux (ufw)
```bash
sudo ufw allow 1433/tcp
```

### 4. 启动Minikube

```bash
# 启动Minikube集群
minikube start \
  --cpus=4 \
  --memory=8192 \
  --disk-size=50g \
  --driver=docker

# 验证host.minikube.internal解析
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup host.minikube.internal

# 测试SQL Server连接
kubectl run -it --rm sqltest --image=mcr.microsoft.com/mssql-tools --restart=Never -- \
  /opt/mssql-tools/bin/sqlcmd -S host.minikube.internal \
  -U sa -P "YourStrongPassword123!" -Q "SELECT @@VERSION"
```

## 部署步骤

### 步骤1: 创建platform-system命名空间

```bash
kubectl create namespace platform-system
```

### 步骤2: 创建SQL Server管理员Secret

```bash
kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password=YourStrongPassword123! \
  --from-literal=server=host.minikube.internal,1433 \
  -n platform-system

# 验证Secret创建
kubectl get secret sqlserver-admin-secret -n platform-system
```

或者使用YAML文件：

```bash
# 编辑k8s/local/sqlserver-admin-secret.yaml，修改密码
kubectl apply -f k8s/local/sqlserver-admin-secret.yaml
```

### 步骤3: 安装Tenant CRD

```bash
kubectl apply -f tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml

# 验证CRD安装
kubectl get crd tenants.medlogic.io
```

### 步骤4: 部署Tenant Operator

```bash
# 构建Operator镜像（使用Minikube的Docker守护进程）
eval $(minikube docker-env)
cd tenant-operator
docker build -t tenant-operator:latest .

# 部署Operator
kubectl apply -f config/rbac/role.yaml
kubectl apply -f config/manager/deployment.yaml

# 验证Operator运行
kubectl get pods -n tenant-operator-system
kubectl logs -n tenant-operator-system -l app=tenant-operator
```

### 步骤5: 创建测试租户

```bash
# 创建Hospital A租户
kubectl apply -f tenant-operator/config/samples/hospital-a.yaml

# 监控创建进度
kubectl get tenant hospital-a -w

# 查看详细状态
kubectl get tenant hospital-a -o yaml
```

### 步骤6: 验证数据库创建

```bash
# 1. 检查Tenant状态
kubectl get tenant hospital-a -o jsonpath='{.status}'

# 应该显示:
# {
#   "phase": "Ready",
#   "namespaceCreated": true,
#   "databaseCreated": true,
#   "secretCreated": true,
#   "resourcesProvisioned": true
# }

# 2. 检查租户命名空间
kubectl get namespace tenant-hospital-a

# 3. 检查数据库Secret
kubectl get secret -n tenant-hospital-a tenant-hospital-a-db-secret

# 4. 查看Secret内容
kubectl get secret -n tenant-hospital-a tenant-hospital-a-db-secret -o jsonpath='{.data.username}' | base64 -d
kubectl get secret -n tenant-hospital-a tenant-hospital-a-db-secret -o jsonpath='{.data.database}' | base64 -d

# 5. 连接SQL Server验证数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" \
  -Q "SELECT name FROM sys.databases WHERE name LIKE '%_DB'"

# 6. 查看数据库表结构
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" \
  -d HospitalA_DB \
  -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES"

# 应该显示: Transactions, AuditLogs

# 7. 测试租户用户连接
# 首先获取密码
TENANT_PASSWORD=$(kubectl get secret -n tenant-hospital-a tenant-hospital-a-db-secret -o jsonpath='{.data.password}' | base64 -d)

sqlcmd -S localhost -U hospital_a_user -P "$TENANT_PASSWORD" \
  -d HospitalA_DB \
  -Q "SELECT * FROM Transactions"
```

## 使用示例

### 创建多个租户

```bash
# 创建Hospital B
kubectl apply -f tenant-operator/config/samples/hospital-b.yaml

# 创建自定义租户
cat <<EOF | kubectl apply -f -
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: hospital-c
spec:
  displayName: "Hospital C"
  db:
    mode: perDatabase
    server: "host.minikube.internal,1433"
    database: HospitalC_DB
  throttling:
    rps: 150
  slo:
    availability: "99.9%"
    p95_latency_ms: 800
EOF

# 查看所有租户
kubectl get tenants

# 查看所有租户数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" \
  -Q "SELECT name, create_date FROM sys.databases WHERE name LIKE '%_DB' ORDER BY create_date DESC"
```

### 查看Operator日志

```bash
# 实时查看日志
kubectl logs -n tenant-operator-system -l app=tenant-operator -f

# 查看最近100行日志
kubectl logs -n tenant-operator-system -l app=tenant-operator --tail=100

# 搜索特定租户的日志
kubectl logs -n tenant-operator-system -l app=tenant-operator | grep "hospital-a"
```

### 删除租户

```bash
# 删除Tenant CRD（会自动清理命名空间和Secret）
kubectl delete tenant hospital-a

# 手动删除数据库（如果需要）
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
ALTER DATABASE [HospitalA_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [HospitalA_DB];
"
```

## 故障排查

### 问题1: Operator无法连接SQL Server

**症状:**
```
Failed to provision database: failed to ping SQL Server: dial tcp: lookup host.minikube.internal: no such host
```

**解决方案:**
```bash
# 1. 验证host.minikube.internal解析
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup host.minikube.internal

# 2. 如果解析失败，检查Minikube版本
minikube version

# 3. 重启Minikube
minikube stop
minikube start

# 4. 测试连接
kubectl run -it --rm sqltest --image=mcr.microsoft.com/mssql-tools --restart=Never -- \
  /opt/mssql-tools/bin/sqlcmd -S host.minikube.internal \
  -U sa -P "YourStrongPassword123!" -Q "SELECT 1"
```

### 问题2: 认证失败

**症状:**
```
Failed to provision database: Login failed for user 'sa'
```

**解决方案:**
```bash
# 1. 验证SQL Server认证模式
sqlcmd -S localhost -E -Q "SELECT SERVERPROPERTY('IsIntegratedSecurityOnly')"
# 应该返回0（混合模式）

# 2. 如果返回1，启用混合模式
sqlcmd -S localhost -E -Q "
EXEC xp_instance_regwrite 
  N'HKEY_LOCAL_MACHINE', 
  N'Software\Microsoft\MSSQLServer\MSSQLServer', 
  N'LoginMode', 
  REG_DWORD, 
  2
"

# 3. 重启SQL Server服务
# Windows: net stop MSSQLSERVER && net start MSSQLSERVER
# Docker: docker restart sqlserver

# 4. 验证sa账户
sqlcmd -S localhost -E -Q "
SELECT name, is_disabled FROM sys.server_principals WHERE name = 'sa'
"

# 5. 更新Secret
kubectl delete secret sqlserver-admin-secret -n platform-system
kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password=YourStrongPassword123! \
  --from-literal=server=host.minikube.internal,1433 \
  -n platform-system

# 6. 重启Operator
kubectl rollout restart deployment tenant-operator -n tenant-operator-system
```

### 问题3: TDE启用失败

**症状:**
```
Failed to enable TDE: TDE certificate 'TDE_Cert' not found
```

**解决方案:**
```bash
# 创建TDE证书
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
USE master;
IF NOT EXISTS (SELECT * FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
BEGIN
    CREATE MASTER KEY ENCRYPTION BY PASSWORD = 'MasterKeyPassword123!';
END

IF NOT EXISTS (SELECT * FROM sys.certificates WHERE name = 'TDE_Cert')
BEGIN
    CREATE CERTIFICATE TDE_Cert WITH SUBJECT = 'TDE Certificate';
END
"

# 验证证书
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
SELECT name, subject FROM sys.certificates WHERE name = 'TDE_Cert'
"

# 重新创建租户
kubectl delete tenant hospital-a
kubectl apply -f tenant-operator/config/samples/hospital-a.yaml
```

### 问题4: Tenant状态为Failed

**症状:**
```
kubectl get tenant hospital-a
NAME         PHASE   AGE
hospital-a   Failed  2m
```

**解决方案:**
```bash
# 1. 查看详细错误信息
kubectl get tenant hospital-a -o jsonpath='{.status.conditions[?(@.type=="DatabaseProvisioned")].message}'

# 2. 查看Operator日志
kubectl logs -n tenant-operator-system -l app=tenant-operator --tail=50

# 3. 根据错误信息修复问题后，删除并重新创建租户
kubectl delete tenant hospital-a
kubectl apply -f tenant-operator/config/samples/hospital-a.yaml

# 4. 或者使用annotation触发重新协调
kubectl annotate tenant hospital-a retry=true --overwrite
```

### 问题5: 数据库已存在

**症状:**
```
Database 'HospitalA_DB' already exists
```

**解决方案:**

Database Provisioner具有幂等性，会自动跳过已存在的数据库。如果需要重新创建：

```bash
# 1. 删除Tenant CRD
kubectl delete tenant hospital-a

# 2. 手动删除数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'HospitalA_DB')
BEGIN
    ALTER DATABASE [HospitalA_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [HospitalA_DB];
END
"

# 3. 删除Secret
kubectl delete secret tenant-hospital-a-db-secret -n tenant-hospital-a

# 4. 重新创建租户
kubectl apply -f tenant-operator/config/samples/hospital-a.yaml
```

## 监控和维护

### 监控数据库创建

```bash
# 查看所有租户状态
kubectl get tenants -o custom-columns=NAME:.metadata.name,PHASE:.status.phase,DB_CREATED:.status.databaseCreated,SECRET_CREATED:.status.secretCreated

# 查看数据库列表
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
SELECT 
    name AS DatabaseName,
    create_date AS CreatedDate,
    (SELECT COUNT(*) FROM sys.tables WHERE database_id = d.database_id) AS TableCount
FROM sys.databases d
WHERE name LIKE '%_DB'
ORDER BY create_date DESC
"

# 查看数据库大小
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
SELECT 
    DB_NAME(database_id) AS DatabaseName,
    SUM(size * 8 / 1024) AS SizeMB
FROM sys.master_files
WHERE DB_NAME(database_id) LIKE '%_DB'
GROUP BY database_id
ORDER BY SizeMB DESC
"
```

### 备份数据库

```bash
# 备份单个租户数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
BACKUP DATABASE [HospitalA_DB] 
TO DISK = 'C:\Backup\HospitalA_DB.bak'
WITH FORMAT, INIT, NAME = 'Full Backup of HospitalA_DB'
"

# 备份所有租户数据库
for db in $(sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "SELECT name FROM sys.databases WHERE name LIKE '%_DB'" -h -1); do
    sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
    BACKUP DATABASE [$db] 
    TO DISK = 'C:\Backup\${db}_$(date +%Y%m%d).bak'
    WITH FORMAT, INIT
    "
done
```

### 清理测试数据

```bash
# 删除所有测试租户
kubectl delete tenants --all

# 删除所有租户数据库
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql += 'ALTER DATABASE [' + name + '] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [' + name + ']; '
FROM sys.databases
WHERE name LIKE '%_DB';
EXEC sp_executesql @sql;
"
```

## 性能优化

### 连接池配置

在生产环境中，建议配置连接池参数：

```go
// 在DatabaseProvisioner初始化时配置
db.SetMaxOpenConns(10)
db.SetMaxIdleConns(5)
db.SetConnMaxLifetime(time.Hour)
```

### 批量创建优化

如果需要批量创建租户，建议：

1. 分批创建，每批5-10个租户
2. 使用Kubernetes Job而非直接创建多个Tenant CRD
3. 监控SQL Server资源使用情况

```bash
# 批量创建示例
for i in {1..10}; do
    cat <<EOF | kubectl apply -f -
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: hospital-$i
spec:
  displayName: "Hospital $i"
  db:
    mode: perDatabase
    server: "host.minikube.internal,1433"
    database: Hospital${i}_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
EOF
    sleep 10  # 等待10秒再创建下一个
done
```

## 安全最佳实践

1. **定期轮换密码**
   ```bash
   # 生成新密码
   NEW_PASSWORD=$(openssl rand -base64 32)
   
   # 更新SQL Server
   sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "
   ALTER LOGIN sa WITH PASSWORD = '$NEW_PASSWORD'
   "
   
   # 更新Kubernetes Secret
   kubectl delete secret sqlserver-admin-secret -n platform-system
   kubectl create secret generic sqlserver-admin-secret \
     --from-literal=username=sa \
     --from-literal=password=$NEW_PASSWORD \
     --from-literal=server=host.minikube.internal,1433 \
     -n platform-system
   
   # 重启Operator
   kubectl rollout restart deployment tenant-operator -n tenant-operator-system
   ```

2. **启用SQL Server审计**
   ```sql
   -- 创建服务器审计
   CREATE SERVER AUDIT TenantDatabaseAudit
   TO FILE (FILEPATH = 'C:\SQLAudit\')
   WITH (ON_FAILURE = CONTINUE);
   
   -- 启用审计
   ALTER SERVER AUDIT TenantDatabaseAudit WITH (STATE = ON);
   
   -- 创建审计规范
   CREATE SERVER AUDIT SPECIFICATION TenantDatabaseAuditSpec
   FOR SERVER AUDIT TenantDatabaseAudit
   ADD (DATABASE_OBJECT_CHANGE_GROUP),
   ADD (SCHEMA_OBJECT_CHANGE_GROUP);
   
   -- 启用审计规范
   ALTER SERVER AUDIT SPECIFICATION TenantDatabaseAuditSpec WITH (STATE = ON);
   ```

3. **限制网络访问**
   - 仅允许Minikube节点访问SQL Server
   - 使用防火墙规则限制源IP
   - 考虑使用VPN或专线连接

## 下一步

- [配置本地开发环境](LOCAL_ENVIRONMENT_GUIDE.zh.md)
- [端到端测试指南](E2E_TESTING_GUIDE.md)
- [生产环境部署](DEPLOYMENT_GUIDE.zh.md)

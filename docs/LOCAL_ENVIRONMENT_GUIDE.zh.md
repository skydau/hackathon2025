# MedLogic平台本地环境部署指南

本指南将帮助您在本地Minikube环境中部署和运行MedLogic多租户医疗平台。

## 目录

- [系统要求](#系统要求)
- [前置条件](#前置条件)
- [SQL Server安装配置](#sql-server安装配置)
- [快速开始](#快速开始)
- [手动部署步骤](#手动部署步骤)
- [验证部署](#验证部署)
- [常见问题](#常见问题)
- [故障排查](#故障排查)

## 系统要求

### 硬件要求

- **CPU**: 4核心或以上（推荐6核心）
- **内存**: 16GB或以上（推荐32GB）
- **磁盘**: 50GB可用空间

### 操作系统

- Windows 10/11 (64位)
- macOS 10.15 (Catalina) 或更高版本
- Linux (Ubuntu 20.04+, CentOS 8+, 或其他主流发行版)

## 前置条件

### 必需软件

1. **Docker Desktop** (Windows/macOS) 或 **Docker Engine** (Linux)
   - Windows: https://docs.docker.com/desktop/install/windows-install/
   - macOS: https://docs.docker.com/desktop/install/mac-install/
   - Linux: https://docs.docker.com/engine/install/

2. **Minikube**
   - 安装指南: https://minikube.sigs.k8s.io/docs/start/
   - 版本要求: v1.30.0 或更高

3. **kubectl**
   - 安装指南: https://kubernetes.io/docs/tasks/tools/
   - 版本要求: v1.28.0 或更高

4. **MS SQL Server 2019或更高版本**
   - 见下方[SQL Server安装配置](#sql-server安装配置)

5. **.NET SDK 9.0** (用于构建服务)
   - 下载: https://dotnet.microsoft.com/download

6. **Go 1.21+** (用于构建Tenant Operator)
   - 下载: https://golang.org/dl/

### 验证安装

```bash
# 检查Docker
docker --version

# 检查Minikube
minikube version

# 检查kubectl
kubectl version --client

# 检查.NET SDK
dotnet --version

# 检查Go
go version
```

## SQL Server安装配置

### Windows环境

#### 1. 下载并安装SQL Server

1. 访问 [SQL Server下载页面](https://www.microsoft.com/sql-server/sql-server-downloads)
2. 下载 **SQL Server 2019 Express** 或 **Developer Edition**
3. 运行安装程序，选择"基本"安装类型
4. 记录安装路径和实例名称（默认为 `MSSQLSERVER`）

#### 2. 启用SQL Server认证

打开 **SQL Server Management Studio (SSMS)**:

```sql
-- 启用SA账户
USE master;
GO

ALTER LOGIN sa ENABLE;
GO

ALTER LOGIN sa WITH PASSWORD = 'YourStrongPassword123!';
GO
```

#### 3. 启用TCP/IP协议

1. 打开 **SQL Server Configuration Manager**
2. 展开 **SQL Server网络配置** > **MSSQLSERVER的协议**
3. 右键点击 **TCP/IP** > **启用**
4. 双击 **TCP/IP**，切换到 **IP地址** 选项卡
5. 找到 **IPAll** 部分，设置 **TCP端口** 为 `1433`
6. 重启SQL Server服务

#### 4. 配置防火墙

```powershell
# 以管理员身份运行PowerShell
New-NetFirewallRule -DisplayName "SQL Server" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
```

#### 5. 创建平台数据库

```sql
-- 创建平台数据库
CREATE DATABASE MedLogicPlatform;
GO

-- 验证数据库
SELECT name FROM sys.databases WHERE name = 'MedLogicPlatform';
GO
```

### Linux环境 (Docker)

#### 1. 运行SQL Server容器

```bash
# 拉取SQL Server镜像
docker pull mcr.microsoft.com/mssql/server:2019-latest

# 运行SQL Server容器
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrongPassword123!" \
  -p 1433:1433 \
  --name sqlserver \
  --hostname sqlserver \
  -d mcr.microsoft.com/mssql/server:2019-latest

# 验证容器运行
docker ps | grep sqlserver
```

#### 2. 创建平台数据库

```bash
# 连接到SQL Server
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!"

# 在sqlcmd提示符下执行
CREATE DATABASE MedLogicPlatform;
GO
SELECT name FROM sys.databases WHERE name = 'MedLogicPlatform';
GO
EXIT
```

#### 3. 配置防火墙 (如果需要)

```bash
# Ubuntu/Debian (ufw)
sudo ufw allow 1433/tcp

# CentOS/RHEL (firewalld)
sudo firewall-cmd --permanent --add-port=1433/tcp
sudo firewall-cmd --reload
```

### macOS环境 (Docker)

macOS上的步骤与Linux相同，使用Docker运行SQL Server。

```bash
# 运行SQL Server容器
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourStrongPassword123!" \
  -p 1433:1433 \
  --name sqlserver \
  -d mcr.microsoft.com/mssql/server:2019-latest

# 创建数据库
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!" \
  -Q "CREATE DATABASE MedLogicPlatform"
```

### 验证SQL Server配置

```bash
# 测试连接
# Windows (使用sqlcmd)
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "SELECT @@VERSION"

# Linux/macOS (使用Docker)
docker exec -it sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "YourStrongPassword123!" -Q "SELECT @@VERSION"
```

## 快速开始

### 使用自动化脚本

我们提供了自动化脚本来简化部署过程。

#### Windows环境（PowerShell）

```powershell
# 克隆仓库
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform

# 步骤1: 配置SQL Server
sqlcmd -S localhost -U sa -i scripts\sql\setup-sqlserver.sql

# 步骤2: 启动Minikube
.\scripts\start-minikube.ps1

# 步骤3: 构建镜像
.\scripts\build-images.ps1

# 步骤4: 部署平台
.\scripts\deploy-local.ps1 -SqlPassword "YourStrongPassword123!"

# 步骤5: 运行端到端测试
.\scripts\test-e2e.ps1 -SqlServerPassword "YourStrongPassword123!"
```

#### Linux/macOS环境（Bash）

```bash
# 克隆仓库
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform

# 赋予脚本执行权限
chmod +x scripts/*.sh

# 步骤1: 配置SQL Server
sqlcmd -S localhost -U sa -i scripts/sql/setup-sqlserver.sql

# 步骤2: 启动Minikube
./scripts/start-minikube.sh

# 步骤3: 构建镜像
./scripts/build-images.sh

# 步骤4: 部署平台
SQL_PASSWORD="YourStrongPassword123!" ./scripts/deploy-local.sh

# 步骤5: 运行端到端测试
SQL_SERVER_PASSWORD="YourStrongPassword123!" ./scripts/test-e2e.sh
```

### 脚本说明

#### start-minikube (启动Minikube)
- 启动Minikube集群（默认4核CPU，8GB内存）
- 启用必需的插件（metrics-server, ingress）
- 验证集群状态

#### build-images (构建镜像)
- 配置Docker环境使用Minikube的Docker守护进程
- 构建所有.NET服务镜像
- 构建Smart Gateway (NGINX) 镜像
- 构建Tenant Operator (Go) 镜像

#### deploy-local (部署平台)
- 创建必需的命名空间
- 创建SQL Server管理员Secret
- 安装Tenant CRD
- 部署所有平台服务
- 显示服务访问端点

#### test-e2e (端到端测试)
- 创建测试租户
- 验证命名空间和Secret创建
- 验证数据库自动创建
- 验证数据库配置注册
- 测试租户退服流程

完成后，脚本会显示访问信息和快速测试命令。

## 手动部署步骤

如果您希望手动控制每个步骤，请按照以下说明操作。

### 步骤1: 启动Minikube

```bash
# 启动Minikube集群
minikube start \
  --cpus=4 \
  --memory=8192 \
  --disk-size=50g \
  --driver=docker \
  --kubernetes-version=v1.28.0

# 启用必需插件
minikube addons enable ingress
minikube addons enable metrics-server

# 验证集群状态
kubectl cluster-info
kubectl get nodes
```

### 步骤2: 验证网络连接

```bash
# 测试从Minikube到宿主机SQL Server的连接
kubectl run sqltest --image=mcr.microsoft.com/mssql-tools --restart=Never --rm -i --command -- \
  /opt/mssql-tools/bin/sqlcmd -S host.minikube.internal -U sa -P "YourStrongPassword123!" -Q "SELECT @@VERSION"
```

如果连接失败，请检查：
- SQL Server是否在运行
- TCP/IP协议是否已启用
- 防火墙是否允许端口1433
- SA密码是否正确

### 步骤3: 创建命名空间

```bash
kubectl apply -f k8s/local/namespace.yaml
```

### 步骤4: 创建SQL Server管理员Secret

```bash
kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password=YourStrongPassword123! \
  --from-literal=server=host.minikube.internal,1433 \
  -n platform-system
```

### 步骤5: 构建Docker镜像

```bash
# 切换到Minikube的Docker守护进程
eval $(minikube docker-env)

# 构建.NET服务镜像
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .
docker build -f src/DeviceRegistryService/Dockerfile -t medlogic/device-registry-service:latest .
docker build -f src/MedLogicService/Dockerfile -t medlogic/medlogic-service:latest .

# 构建NGINX网关镜像
docker build -f nginx/Dockerfile -t medlogic/smart-gateway:latest nginx/

# 构建Tenant Operator镜像
cd tenant-operator
docker build -t medlogic/tenant-operator:latest .
cd ..

# 验证镜像
docker images | grep medlogic
```

### 步骤6: 部署服务

```bash
# 部署平台服务
kubectl apply -f k8s/local/tenant-catalog-deployment.yaml
kubectl apply -f k8s/local/device-registry-deployment.yaml
kubectl apply -f k8s/local/medlogic-service-deployment.yaml

# 部署网关
kubectl apply -f k8s/local/smart-gateway-deployment.yaml

# 安装Tenant CRD
kubectl apply -f tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml

# 部署Tenant Operator
kubectl apply -f k8s/local/tenant-operator-deployment.yaml
```

### 步骤7: 等待服务就绪

```bash
# 查看Pod状态
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 等待所有Pod就绪
kubectl wait --for=condition=ready pod -l app=tenant-catalog-service -n platform-system --timeout=120s
kubectl wait --for=condition=ready pod -l app=device-registry-service -n platform-system --timeout=120s
kubectl wait --for=condition=ready pod -l app=smart-gateway -n gateway --timeout=120s
kubectl wait --for=condition=ready pod -l app=medlogic-service -n platform-system --timeout=120s
kubectl wait --for=condition=ready pod -l app=tenant-operator -n platform-system --timeout=120s
```

## 验证部署

### 1. 检查服务健康状态

```bash
# 获取Smart Gateway URL
GATEWAY_URL=$(minikube service smart-gateway -n gateway --url)
echo "Gateway URL: $GATEWAY_URL"

# 测试Tenant Catalog API
curl $GATEWAY_URL/api/tenants

# 测试Device Registry API
curl $GATEWAY_URL/api/devices
```

### 2. 创建测试租户

```bash
# 创建租户
curl -X POST $GATEWAY_URL/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "测试医院A",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "host.minikube.internal,1433",
      "database": "TestHospitalA_DB",
      "username": "test_hospital_a_user"
    },
    "throttling": {
      "rps": 100
    },
    "slo": {
      "availability": "99.9%",
      "p95LatencyMs": 1000
    }
  }'

# 保存返回的租户ID
TENANT_ID="<从上面的响应中获取>"
```

### 3. 验证Tenant Operator自动化

```bash
# 检查是否创建了租户命名空间
kubectl get namespace tenant-test-hospital-a

# 检查命名空间中的资源
kubectl get all,configmap,secret,resourcequota,networkpolicy -n tenant-test-hospital-a

# 检查Tenant CRD状态
kubectl get tenant test-hospital-a -o yaml
```

### 4. 注册测试设备

```bash
# 注册设备
curl -X POST $GATEWAY_URL/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-TEST-001",
    "tenantId": "'$TENANT_ID'",
    "deviceType": "medDispense"
  }'

# 查询设备
curl $GATEWAY_URL/api/devices/DEVICE-TEST-001
```

### 5. 测试端到端请求流

```bash
# 模拟设备请求（通过Smart Gateway）
curl -X POST $GATEWAY_URL/api/transactions \
  -H "Device-Id: DEVICE-TEST-001" \
  -H "Content-Type: application/json" \
  -d '{
    "amount": 100.50,
    "description": "测试交易"
  }'
```

## 常见问题

### Q1: Minikube启动失败

**问题**: `minikube start` 命令失败

**解决方案**:
```bash
# 删除现有集群
minikube delete

# 清理缓存
minikube delete --all --purge

# 重新启动
minikube start --driver=docker
```

### Q2: Pod无法连接SQL Server

**问题**: Pod日志显示无法连接到 `host.minikube.internal`

**解决方案**:
```bash
# 1. 验证host.minikube.internal解析
kubectl run -it --rm debug --image=busybox --restart=Never -- nslookup host.minikube.internal

# 2. 测试端口连接
kubectl run -it --rm debug --image=busybox --restart=Never -- telnet host.minikube.internal 1433

# 3. 检查SQL Server是否监听正确的端口
# Windows
netstat -an | findstr 1433

# Linux/macOS
netstat -an | grep 1433

# 4. 检查防火墙规则
# Windows
netsh advfirewall firewall show rule name="SQL Server"

# Linux
sudo ufw status
```

### Q3: 镜像拉取失败

**问题**: Pod状态显示 `ImagePullBackOff` 或 `ErrImagePull`

**解决方案**:
```bash
# 确保使用Minikube的Docker守护进程
eval $(minikube docker-env)

# 重新构建镜像
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .

# 验证镜像存在
docker images | grep medlogic

# 确保Deployment使用imagePullPolicy: Never
kubectl get deployment tenant-catalog-service -n platform-system -o yaml | grep imagePullPolicy
```

### Q4: Tenant Operator无法创建数据库

**问题**: Tenant CRD状态显示 `Failed`，数据库未创建

**解决方案**:
```bash
# 1. 检查Operator日志
kubectl logs -n platform-system -l app=tenant-operator --tail=100

# 2. 验证SQL Server管理员Secret
kubectl get secret sqlserver-admin-secret -n platform-system -o yaml

# 3. 手动测试数据库连接
kubectl run -it --rm sqltest --image=mcr.microsoft.com/mssql-tools --restart=Never -- \
  /opt/mssql-tools/bin/sqlcmd \
  -S host.minikube.internal \
  -U sa \
  -P "YourStrongPassword123!" \
  -Q "SELECT name FROM sys.databases"
```

### Q5: 服务响应缓慢

**问题**: API请求响应时间过长

**解决方案**:
```bash
# 1. 检查资源使用情况
kubectl top nodes
kubectl top pods -n platform-system

# 2. 增加Minikube资源
minikube stop
minikube delete
minikube start --cpus=6 --memory=16384

# 3. 检查数据库连接池
kubectl logs -n platform-system -l app=medlogic-service | grep "connection pool"
```

## 故障排查

### 收集诊断信息

```bash
# 使用诊断脚本
chmod +x scripts/collect-diagnostics.sh
./scripts/collect-diagnostics.sh

# 手动收集信息
kubectl get all -n platform-system
kubectl get all -n gateway
kubectl describe pods -n platform-system
kubectl logs -n platform-system -l app=tenant-catalog-service --tail=100
kubectl logs -n platform-system -l app=tenant-operator --tail=100
```

### 查看事件

```bash
# 查看最近的事件
kubectl get events -n platform-system --sort-by='.lastTimestamp'
kubectl get events -n gateway --sort-by='.lastTimestamp'
```

### 访问Kubernetes Dashboard

```bash
# 启动Dashboard
minikube dashboard
```

### 重置环境

如果遇到无法解决的问题，可以完全重置环境：

```bash
# 删除所有资源
kubectl delete namespace platform-system gateway

# 删除Minikube集群
minikube delete

# 重新开始
./scripts/local-setup.sh
```

## 下一步

- [创建您的第一个租户](./TENANT_ONBOARDING.zh.md)
- [配置监控和告警](./OBSERVABILITY_SETUP.zh.md)
- [开发指南](./DEVELOPMENT_GUIDE.zh.md)
- [API文档](./API_REFERENCE.zh.md)

## 获取帮助

如果您遇到问题：

1. 查看 [故障排查文档](./TROUBLESHOOTING.zh.md)
2. 搜索 [GitHub Issues](https://github.com/your-org/medlogic-platform/issues)
3. 提交新的Issue并附上诊断信息

## 清理环境

完成测试后，可以清理本地环境：

```bash
# 删除所有Kubernetes资源
kubectl delete namespace platform-system gateway observability

# 停止Minikube
minikube stop

# 完全删除Minikube集群
minikube delete

# 停止SQL Server容器 (如果使用Docker)
docker stop sqlserver
docker rm sqlserver
```

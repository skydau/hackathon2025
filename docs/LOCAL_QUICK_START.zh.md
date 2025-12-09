# MedLogic平台本地环境快速入门

5分钟内在本地Minikube环境中启动MedLogic多租户医疗平台。

## 前置条件

确保已安装以下软件：

- ✅ Docker Desktop (Windows/macOS) 或 Docker Engine (Linux)
- ✅ Minikube (v1.30.0+)
- ✅ kubectl (v1.28.0+)
- ✅ MS SQL Server 2019+ (在宿主机上运行)

## 快速开始

### 1. 配置SQL Server

确保SQL Server已启动并配置：

```bash
# 测试SQL Server连接
# Windows
sqlcmd -S localhost -U sa -P "YourStrongPassword123!" -Q "SELECT @@VERSION"

# Linux/macOS (Docker)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrongPassword123!" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2019-latest
```

### 2. 运行自动化设置脚本

```bash
# 克隆仓库
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform

# 运行设置脚本
chmod +x scripts/local-setup.sh
./scripts/local-setup.sh
```

脚本将自动完成：
- ✅ 启动Minikube集群
- ✅ 验证SQL Server连接
- ✅ 构建所有Docker镜像
- ✅ 部署所有服务
- ✅ 等待服务就绪

### 3. 验证部署

```bash
# 获取Gateway URL
GATEWAY_URL=$(minikube service smart-gateway -n gateway --url)

# 测试API
curl $GATEWAY_URL/api/tenants
```

## 创建第一个租户

```bash
# 创建租户
curl -X POST $GATEWAY_URL/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "测试医院",
    "dbConfig": {
      "mode": "perDatabase"
    },
    "throttling": {
      "rps": 100
    }
  }'

# 查看租户
curl $GATEWAY_URL/api/tenants
```

## 注册设备

```bash
# 注册设备
curl -X POST $GATEWAY_URL/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-001",
    "tenantId": "<从上面获取的租户ID>",
    "deviceType": "medDispense"
  }'
```

## 查看运行状态

```bash
# 查看所有Pod
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 查看日志
kubectl logs -n platform-system -l app=tenant-catalog-service
kubectl logs -n platform-system -l app=tenant-operator

# 访问Dashboard
minikube dashboard
```

## 常见问题

### 无法连接SQL Server

```bash
# 检查SQL Server是否运行
# Windows
sc query MSSQLSERVER

# Linux/macOS
docker ps | grep sqlserver

# 测试连接
kubectl run sqltest --image=mcr.microsoft.com/mssql-tools --restart=Never --rm -i --command -- \
  /opt/mssql-tools/bin/sqlcmd -S host.minikube.internal -U sa -P "YourStrongPassword123!" -Q "SELECT 1"
```

### Pod无法启动

```bash
# 查看Pod详情
kubectl describe pod -n platform-system <pod-name>

# 查看日志
kubectl logs -n platform-system <pod-name>

# 重启Pod
kubectl delete pod -n platform-system <pod-name>
```

### 镜像拉取失败

```bash
# 确保使用Minikube的Docker
eval $(minikube docker-env)

# 重新构建镜像
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .
```

## 清理环境

```bash
# 删除所有资源
kubectl delete namespace platform-system gateway

# 停止Minikube
minikube stop

# 完全删除
minikube delete
```

## 下一步

- 📖 [完整部署指南](./LOCAL_ENVIRONMENT_GUIDE.zh.md)
- 🔧 [故障排查](./TROUBLESHOOTING.zh.md)
- 📚 [API文档](./API_REFERENCE.zh.md)
- 🏗️ [架构概述](../README.md)

## 获取帮助

遇到问题？

1. 查看 [完整部署指南](./LOCAL_ENVIRONMENT_GUIDE.zh.md)
2. 查看 [故障排查文档](./TROUBLESHOOTING.zh.md)
3. 提交 [GitHub Issue](https://github.com/your-org/medlogic-platform/issues)

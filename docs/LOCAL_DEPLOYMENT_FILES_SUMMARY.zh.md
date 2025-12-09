# 本地环境部署文件总结

本文档总结了为MedLogic平台本地Minikube环境创建的所有部署文件和文档。

## 创建的文件清单

### Kubernetes清单文件 (`k8s/local/`)

| 文件名 | 用途 | 说明 |
|--------|------|------|
| `namespace.yaml` | 命名空间定义 | 创建platform-system、gateway和observability命名空间 |
| `sqlserver-admin-secret.yaml` | SQL Server凭据 | 存储SA账户的用户名和密码（示例） |
| `tenant-catalog-deployment.yaml` | 租户目录服务 | Deployment和Service，单副本，imagePullPolicy: Never |
| `device-registry-deployment.yaml` | 设备注册服务 | Deployment和Service，单副本，imagePullPolicy: Never |
| `smart-gateway-deployment.yaml` | 智能网关 | NGINX网关，NodePort类型，端口30080 |
| `tenant-operator-deployment.yaml` | 租户Operator | 包含Deployment、ServiceAccount、ClusterRole和ClusterRoleBinding |
| `medlogic-service-deployment.yaml` | 示例后端服务 | 演示多租户数据库路由的示例服务 |
| `README.zh.md` | 本地部署说明 | 详细的部署指南和故障排查 |

### 脚本文件 (`scripts/`)

| 文件名 | 用途 | 说明 |
|--------|------|------|
| `local-setup.sh` | 自动化设置脚本 | 一键部署整个本地环境 |
| `init-local-database.sql` | 平台数据库初始化 | 创建MedLogicPlatform数据库和表结构 |
| `init-tenant-database-template.sql` | 租户数据库模板 | Tenant Operator用于初始化租户数据库 |

### 文档文件 (`docs/`)

| 文件名 | 用途 | 说明 |
|--------|------|------|
| `LOCAL_ENVIRONMENT_GUIDE.zh.md` | 完整部署指南 | 详细的本地环境部署步骤和配置说明 |
| `LOCAL_QUICK_START.zh.md` | 快速入门指南 | 5分钟快速部署指南 |
| `LOCAL_DEPLOYMENT_FILES_SUMMARY.zh.md` | 本文件 | 所有创建文件的总结 |

## 文件特点

### 本地环境优化

所有本地环境文件都针对Minikube进行了优化：

1. **镜像拉取策略**: 使用 `imagePullPolicy: Never`，避免从远程仓库拉取
2. **资源限制**: 降低资源请求和限制，适应有限的硬件资源
3. **副本数**: 所有服务使用单副本，节省资源
4. **数据库连接**: 使用 `host.minikube.internal` 连接宿主机SQL Server
5. **服务暴露**: Smart Gateway使用NodePort便于本地访问
6. **环境变量**: 设置为Development模式，启用详细日志

### 与生产环境的对比

| 配置项 | 本地环境 | 生产环境 |
|--------|----------|----------|
| **副本数** | 1 | 3 |
| **镜像拉取** | Never | IfNotPresent |
| **CPU请求** | 100m | 250m |
| **内存请求** | 128Mi | 256Mi |
| **数据库** | host.minikube.internal | Azure SQL Database |
| **认证** | SQL Server认证 | Azure Workload Identity |
| **密钥管理** | Kubernetes Secret | Azure Key Vault |
| **服务类型** | NodePort | LoadBalancer |
| **日志级别** | Information | Warning |

## 使用流程

### 快速开始（推荐）

```bash
# 1. 确保SQL Server已运行
# Windows: 检查服务
# Linux/macOS: docker ps | grep sqlserver

# 2. 运行自动化脚本
./scripts/local-setup.sh

# 3. 验证部署
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 4. 获取Gateway URL
GATEWAY_URL=$(minikube service smart-gateway -n gateway --url)

# 5. 测试API
curl $GATEWAY_URL/api/tenants
```

### 手动部署

如果需要更细粒度的控制，请参考 `docs/LOCAL_ENVIRONMENT_GUIDE.zh.md` 中的手动部署步骤。

## 前置条件

### 必需软件

- ✅ Docker Desktop (Windows/macOS) 或 Docker Engine (Linux)
- ✅ Minikube v1.30.0+
- ✅ kubectl v1.28.0+
- ✅ MS SQL Server 2019+
- ✅ .NET SDK 9.0
- ✅ Go 1.21+

### 硬件要求

- **CPU**: 4核心或以上（推荐6核心）
- **内存**: 16GB或以上（推荐32GB）
- **磁盘**: 50GB可用空间

## 关键配置

### SQL Server连接

所有服务使用以下配置连接SQL Server：

```yaml
env:
- name: ConnectionStrings__DefaultConnection
  value: "Server=host.minikube.internal,1433;Database=MedLogicPlatform;User Id=sa;Password=YourStrongPassword123!;TrustServerCertificate=True;Encrypt=True;"
```

### Tenant Operator配置

Tenant Operator需要SQL Server管理员凭据来创建租户数据库：

```yaml
env:
- name: SQL_SERVER_HOST
  valueFrom:
    secretKeyRef:
      name: sqlserver-admin-secret
      key: server
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
```

### Smart Gateway配置

Smart Gateway需要知道后端服务的地址：

```yaml
env:
- name: DEVICE_REGISTRY_URL
  value: "http://device-registry.platform-system.svc.cluster.local:8080"
- name: TENANT_CATALOG_URL
  value: "http://tenant-catalog.platform-system.svc.cluster.local:8080"
- name: BACKEND_SERVICE_URL
  value: "http://medlogic-service.platform-system.svc.cluster.local:8080"
```

## 常见任务

### 更新服务

```bash
# 1. 重新构建镜像
eval $(minikube docker-env)
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .

# 2. 重启Pod
kubectl rollout restart deployment/tenant-catalog-service -n platform-system
```

### 查看日志

```bash
# 查看所有服务日志
kubectl logs -n platform-system -l app=tenant-catalog-service --tail=100
kubectl logs -n platform-system -l app=device-registry-service --tail=100
kubectl logs -n platform-system -l app=tenant-operator --tail=100
kubectl logs -n gateway -l app=smart-gateway --tail=100
```

### 清理环境

```bash
# 删除所有资源
kubectl delete namespace platform-system gateway observability

# 停止Minikube
minikube stop

# 完全删除Minikube集群
minikube delete
```

## 故障排查

### 常见问题

1. **Pod无法连接SQL Server**
   - 检查SQL Server是否运行
   - 检查TCP/IP协议是否启用
   - 检查防火墙规则
   - 测试连接: `kubectl run sqltest ...`

2. **镜像拉取失败**
   - 确保使用Minikube的Docker: `eval $(minikube docker-env)`
   - 重新构建镜像
   - 验证镜像存在: `docker images | grep medlogic`

3. **服务响应缓慢**
   - 增加Minikube资源: `minikube start --cpus=6 --memory=16384`
   - 调整Pod资源限制
   - 检查资源使用: `kubectl top pods -n platform-system`

### 诊断工具

```bash
# 收集诊断信息
./scripts/collect-diagnostics.sh

# 查看Pod状态
kubectl get pods -A

# 查看事件
kubectl get events -n platform-system --sort-by='.lastTimestamp'

# 访问Dashboard
minikube dashboard
```

## 下一步

完成本地环境部署后，您可以：

1. **创建第一个租户**: 参考 `docs/LOCAL_QUICK_START.zh.md`
2. **注册设备**: 使用Device Registry API
3. **测试端到端流程**: 模拟设备请求
4. **配置监控**: 部署Prometheus和Grafana
5. **开发新功能**: 使用本地环境进行开发和测试

## 相关文档

- [本地环境完整部署指南](./LOCAL_ENVIRONMENT_GUIDE.zh.md)
- [本地环境快速入门](./LOCAL_QUICK_START.zh.md)
- [故障排查指南](./TROUBLESHOOTING.zh.md)
- [架构设计文档](../.kiro/specs/multi-tenant-medical-platform/design.md)
- [任务列表](../.kiro/specs/multi-tenant-medical-platform/tasks.md)

## 反馈和贡献

如果您发现问题或有改进建议：

1. 查看现有的 [GitHub Issues](https://github.com/your-org/medlogic-platform/issues)
2. 提交新的Issue并附上详细信息
3. 提交Pull Request改进文档或代码

## 版本历史

- **v1.0** (2024-12): 初始版本，包含所有本地环境部署文件和文档

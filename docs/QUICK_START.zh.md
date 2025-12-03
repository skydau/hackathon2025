# MedLogic 平台快速入门指南

在几分钟内启动并运行 MedLogic 多租户医疗平台。

## 先决条件

- Azure 订阅
- 已安装 kubectl、helm 和 Azure CLI
- 已安装 Docker（用于构建镜像）

## 快速部署（开发环境）

### 1. 克隆和设置

```bash
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform
```

### 2. 设置环境变量

```bash
export RESOURCE_GROUP="medlogic-dev-rg"
export LOCATION="eastus"
export AKS_CLUSTER="medlogic-dev-aks"
export ACR_NAME="medlogicdevacr"
export KEY_VAULT_NAME="medlogic-dev-kv"
export SQL_SERVER_NAME="medlogic-dev-sql"
```

### 3. 创建 Azure 资源

```bash
# 创建资源组
az group create --name $RESOURCE_GROUP --location $LOCATION

# 创建 AKS 集群
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 3 \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --generate-ssh-keys

# 获取凭据
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER

# 创建 ACR
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Standard

# 将 ACR 附加到 AKS
az aks update --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER --attach-acr $ACR_NAME

# 创建 SQL Server
az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER_NAME \
  --admin-user sqladmin \
  --admin-password 'YourStrongPassword123!'

# 创建数据库
az sql db create --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --name TenantCatalog
az sql db create --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --name DeviceRegistry

# 创建 Key Vault
az keyvault create \
  --resource-group $RESOURCE_GROUP \
  --name $KEY_VAULT_NAME \
  --enable-rbac-authorization true
```

### 4. 构建和推送镜像

```bash
# 登录到 ACR
az acr login --name $ACR_NAME

# 构建和推送
chmod +x scripts/build-and-push.sh
./scripts/build-and-push.sh $ACR_NAME.azurecr.io
```

### 5. 配置 Workload Identity

```bash
cd k8s
chmod +x workload-identity-setup.sh
./workload-identity-setup.sh
cd ..
```

### 6. 使用 Helm 部署

创建 `values-dev.yaml`:

```yaml
global:
  imageRegistry: <your-acr>.azurecr.io

tenantCatalog:
  database:
    server: <your-sql-server>.database.windows.net
  azure:
    workloadIdentity:
      clientId: "<client-id>"
      tenantId: "<tenant-id>"

deviceRegistry:
  database:
    server: <your-sql-server>.database.windows.net
  azure:
    workloadIdentity:
      clientId: "<client-id>"
      tenantId: "<tenant-id>"
```

部署:

```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh --environment development --config values-dev.yaml
```

### 7. 验证部署

```bash
# 检查 pods
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 获取网关 IP
kubectl get svc smart-gateway -n gateway

# 运行端到端测试
chmod +x scripts/e2e-test.sh
./scripts/e2e-test.sh
```

## 创建您的第一个租户

```bash
# 获取网关 IP
GATEWAY_IP=$(kubectl get svc smart-gateway -n gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# 创建租户
curl -X POST http://$GATEWAY_IP/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "我的医院",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "medlogic-dev-sql.database.windows.net",
      "database": "MyHospital_DB"
    },
    "throttling": {
      "rps": 100
    }
  }'
```

## 注册设备

```bash
# 注册设备
curl -X POST http://$GATEWAY_IP/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-001",
    "tenantId": "<tenant-id-from-above>",
    "deviceType": "medDispense"
  }'
```

## 访问管理界面

```bash
# 端口转发以本地访问
kubectl port-forward -n platform-system svc/admin-ui 8080:8080

# 打开浏览器
open http://localhost:8080
```

## 故障排除

如果出现问题:

```bash
# 收集诊断信息
chmod +x scripts/collect-diagnostics.sh
./scripts/collect-diagnostics.sh

# 检查日志
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# 检查事件
kubectl get events -n platform-system --sort-by='.lastTimestamp'
```

详细指导请参阅 [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)。

## 下一步

- [完整部署指南](./DEPLOYMENT_GUIDE.md)
- [架构概述](../README.md)
- [租户入职](./TENANT_ONBOARDING.md)
- [监控设置](./OBSERVABILITY_SETUP.md)

## 清理

要删除所有资源:

```bash
# 删除 Helm 发布
helm uninstall medlogic-platform -n platform-system

# 删除命名空间
kubectl delete namespace platform-system gateway

# 删除 Azure 资源
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

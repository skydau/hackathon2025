# MedLogic 平台部署指南

本指南提供了将 MedLogic 多租户医疗平台部署到 Azure Kubernetes Service (AKS) 的全面说明。

## 目录

1. [先决条件](#先决条件)
2. [架构概述](#架构概述)
3. [部署前设置](#部署前设置)
4. [部署方法](#部署方法)
5. [部署后配置](#部署后配置)
6. [验证](#验证)
7. [故障排除](#故障排除)

## 先决条件

### 必需工具

- **kubectl** (v1.28+): Kubernetes 命令行工具
- **Helm** (v3.12+): Kubernetes 包管理器
- **Azure CLI** (v2.50+): Azure 命令行界面
- **Docker** (v24.0+): 容器运行时（用于构建镜像）
- **.NET SDK** (v9.0+): 用于构建 .NET 服务
- **Go** (v1.21+): 用于构建租户操作器

### Azure 资源

部署前，确保您拥有：

1. **Azure 订阅**，具有适当的权限
2. **Azure Kubernetes Service (AKS)** 集群 (v1.28+)
   - 启用 Workload Identity
   - Azure CNI 网络
   - 最少 3 个节点（Standard_D4s_v3 或更大）
3. **Azure SQL Server** 实例
4. **Azure Key Vault** 用于密钥管理
5. **Azure Container Registry (ACR)** 用于容器镜像
6. **Azure AD 应用注册** 用于 Workload Identity

### 必需权限

- **AKS**: 贡献者或更高权限
- **Azure SQL**: SQL Server 贡献者
- **Key Vault**: Key Vault 管理员
- **ACR**: AcrPush 和 AcrPull
- **Azure AD**: 应用程序管理员（用于 Workload Identity 设置）

## 架构概述

平台由以下组件组成：

```
┌─────────────────────────────────────────────────────────────┐
│                    外部设备                                   │
│                  (medDispense Stations)                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   智能网关 (NGINX)                           │
│              命名空间: gateway                               │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                平台服务                                       │
│              命名空间: platform-system                       │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ 租户         │  │ 设备         │  │ MedLogic     │      │
│  │ 目录         │  │ 注册表       │  │ 服务         │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  控制平面                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ 租户         │  │ OPA          │  │ Azure Key    │      │
│  │ 操作器       │  │ Gatekeeper   │  │ Vault        │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## 部署前设置

### 1. 克隆仓库

```bash
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform
```

### 2. 配置 Azure CLI

```bash
# 登录到 Azure
az login

# 设置您的订阅
az account set --subscription "<your-subscription-id>"

# 设置环境变量
export RESOURCE_GROUP="medlogic-platform-rg"
export LOCATION="eastus"
export AKS_CLUSTER="medlogic-aks"
export ACR_NAME="medlogicacr"
export KEY_VAULT_NAME="medlogic-kv"
export SQL_SERVER_NAME="medlogic-sql"
```

### 3. 创建 Azure 资源

#### 创建资源组

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

#### 创建启用 Workload Identity 的 AKS 集群

```bash
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --network-plugin azure \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --generate-ssh-keys

# 获取凭据
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER
```

#### 创建 Azure 容器注册表

```bash
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard

# 将 ACR 附加到 AKS
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --attach-acr $ACR_NAME
```

#### 创建 Azure SQL Server

```bash
az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER_NAME \
  --location $LOCATION \
  --admin-user sqladmin \
  --admin-password '<strong-password>'

# 创建数据库
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog \
  --service-objective S1

az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name DeviceRegistry \
  --service-objective S1

# 配置防火墙以允许 Azure 服务
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

#### 创建 Azure Key Vault

```bash
az keyvault create \
  --resource-group $RESOURCE_GROUP \
  --name $KEY_VAULT_NAME \
  --location $LOCATION \
  --enable-rbac-authorization true
```

### 4. 构建和推送容器镜像

```bash
# 登录到 ACR
az acr login --name $ACR_NAME

# 构建和推送镜像
./scripts/build-and-push.sh $ACR_NAME.azurecr.io
```

### 5. 配置 Workload Identity

运行 workload identity 设置脚本：

```bash
cd k8s
./workload-identity-setup.sh
```

此脚本将：
- 创建 Azure AD 应用注册
- 配置联合凭据
- 创建 Kubernetes 服务账户
- 授予必要的权限

## 部署方法

### 方法 1: Helm 图表部署（推荐）

#### 1. 创建 values 文件

创建 `values-production.yaml` 文件：

```yaml
# values-production.yaml
global:
  imageRegistry: medlogicacr.azurecr.io

tenantCatalog:
  image:
    repository: medlogicacr.azurecr.io/tenant-catalog-service
    tag: "1.0.0"
  database:
    server: medlogic-sql.database.windows.net
    name: TenantCatalog
  azure:
    workloadIdentity:
      clientId: "<tenant-catalog-client-id>"
      tenantId: "<azure-tenant-id>"

deviceRegistry:
  image:
    repository: medlogicacr.azurecr.io/device-registry-service
    tag: "1.0.0"
  database:
    server: medlogic-sql.database.windows.net
    name: DeviceRegistry
  azure:
    workloadIdentity:
      clientId: "<device-registry-client-id>"
      tenantId: "<azure-tenant-id>"

smartGateway:
  image:
    repository: medlogicacr.azurecr.io/smart-gateway
    tag: "1.0.0"
  service:
    type: LoadBalancer

tenantOperator:
  image:
    repository: medlogicacr.azurecr.io/tenant-operator
    tag: "1.0.0"
  azure:
    keyVault:
      name: medlogic-kv
      tenantId: "<azure-tenant-id>"
    workloadIdentity:
      clientId: "<operator-client-id>"

azure:
  subscriptionId: "<subscription-id>"
  resourceGroup: medlogic-platform-rg
  location: eastus
  keyVault:
    name: medlogic-kv
  sqlServer:
    name: medlogic-sql
```

#### 2. 安装 Helm 图表

```bash
# 安装或升级
helm upgrade --install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --create-namespace \
  --wait \
  --timeout 10m
```

#### 3. 验证部署

```bash
# 检查所有 pods 是否运行
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 检查服务
kubectl get svc -n platform-system
kubectl get svc -n gateway
```

### 方法 2: 手动 Kubectl 部署

如果您更喜欢手动部署：

```bash
# 部署命名空间
kubectl apply -f k8s/namespace.yaml

# 部署平台服务
kubectl apply -f k8s/tenant-catalog-deployment.yaml
kubectl apply -f k8s/device-registry-deployment.yaml
kubectl apply -f k8s/medlogic-service-deployment.yaml

# 部署网关
kubectl apply -f k8s/smart-gateway-deployment.yaml

# 部署租户操作器
kubectl apply -f tenant-operator/config/crd/
kubectl apply -f tenant-operator/config/manager/
kubectl apply -f tenant-operator/config/rbac/

# 部署 OPA Gatekeeper
kubectl apply -f k8s/gatekeeper-install.yaml
kubectl apply -f k8s/gatekeeper-constraint-template-tenant-label.yaml
kubectl apply -f k8s/gatekeeper-constraint-tenant-label.yaml
kubectl apply -f k8s/gatekeeper-constraint-template-no-cross-tenant-netpol.yaml
kubectl apply -f k8s/gatekeeper-constraint-no-cross-tenant-netpol.yaml
```

### 方法 3: 自动化部署脚本

使用自动化部署脚本：

```bash
./scripts/deploy.sh --environment production --config values-production.yaml
```

## 部署后配置

### 1. 配置数据库架构

运行数据库迁移：

```bash
# 租户目录
kubectl exec -n platform-system deployment/tenant-catalog-service -- \
  dotnet ef database update

# 设备注册表
kubectl exec -n platform-system deployment/device-registry-service -- \
  dotnet ef database update
```

### 2. 创建初始租户

```bash
# 获取网关外部 IP
GATEWAY_IP=$(kubectl get svc -n gateway smart-gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# 通过 API 创建租户
curl -X POST http://$GATEWAY_IP/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "医院 A",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "medlogic-sql.database.windows.net",
      "database": "HospitalA_DB"
    },
    "throttling": {
      "rps": 100
    },
    "slo": {
      "availability": "99.9%",
      "p95LatencyMs": 1000
    }
  }'
```

### 3. 注册设备

```bash
# 注册设备
curl -X POST http://$GATEWAY_IP/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-001",
    "tenantId": "<tenant-id-from-previous-step>",
    "deviceType": "medDispense"
  }'
```

### 4. 配置监控

```bash
# 部署 Prometheus 和 Grafana（如果使用可观测性堆栈）
kubectl apply -f k8s/prometheus-deployment.yaml
kubectl apply -f k8s/grafana-deployment.yaml

# 应用 SLO 监控规则
kubectl apply -f k8s/prometheus-slo-rules.yaml
```

## 验证

### 健康检查

```bash
# 检查所有 pods 是否健康
kubectl get pods --all-namespaces | grep -v Running

# 检查服务端点
kubectl get endpoints -n platform-system

# 测试健康端点
kubectl run curl --image=curlimages/curl -i --rm --restart=Never -- \
  curl http://tenant-catalog.platform-system.svc.cluster.local:8080/health

kubectl run curl --image=curlimages/curl -i --rm --restart=Never -- \
  curl http://device-registry.platform-system.svc.cluster.local:8080/health
```

### 端到端测试

```bash
# 运行端到端测试脚本
./scripts/e2e-test.sh
```

### 验证租户隔离

```bash
# 创建测试租户
kubectl apply -f tenant-operator/config/samples/tenant_v1_tenant.yaml

# 验证命名空间创建
kubectl get namespaces | grep tenant-

# 验证网络策略
kubectl get networkpolicies -n tenant-hospital-a

# 验证资源配额
kubectl get resourcequotas -n tenant-hospital-a
```

## 故障排除

详细的故障排除指导请参阅 [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)。

### 快速诊断

```bash
# 检查 pod 日志
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# 检查事件
kubectl get events -n platform-system --sort-by='.lastTimestamp'

# 检查 workload identity
kubectl describe pod -n platform-system <pod-name> | grep -A 5 "azure.workload.identity"

# 检查数据库连接
kubectl exec -n platform-system deployment/tenant-catalog-service -- \
  /bin/sh -c 'echo "SELECT 1" | sqlcmd -S $DATABASE_SERVER -d $DATABASE_NAME'
```

## 回滚

### Helm 回滚

```bash
# 列出发布
helm list -n platform-system

# 回滚到上一个版本
helm rollback medlogic-platform -n platform-system

# 回滚到特定版本
helm rollback medlogic-platform 2 -n platform-system
```

### 手动回滚

```bash
# 回滚部署
kubectl rollout undo deployment/tenant-catalog-service -n platform-system
kubectl rollout undo deployment/device-registry-service -n platform-system
kubectl rollout undo deployment/smart-gateway -n gateway
```

## 维护

### 更新服务

```bash
# 更新镜像版本
kubectl set image deployment/tenant-catalog-service \
  tenant-catalog-service=medlogicacr.azurecr.io/tenant-catalog-service:1.1.0 \
  -n platform-system

# 检查滚动更新状态
kubectl rollout status deployment/tenant-catalog-service -n platform-system
```

### 扩展

```bash
# 扩展部署
kubectl scale deployment/tenant-catalog-service --replicas=5 -n platform-system

# 启用自动扩展
kubectl autoscale deployment/tenant-catalog-service \
  --min=3 --max=10 --cpu-percent=70 \
  -n platform-system
```

## 安全考虑

1. **密钥管理**: 所有密钥都存储在 Azure Key Vault 中
2. **网络策略**: 在租户之间强制执行严格的网络隔离
3. **RBAC**: 实施最小权限访问控制
4. **镜像扫描**: 扫描所有容器镜像的漏洞
5. **审计日志**: 为所有 API 调用启用审计日志

## 支持

如有问题或疑问：
- 在 GitHub 仓库中创建问题
- 联系平台团队：platform@medlogic.io
- 参考[架构文档](../README.md)

## 下一步

- [配置可观测性](./OBSERVABILITY_SETUP.md)
- [设置 CI/CD 管道](./CICD_SETUP.md)
- [租户入职指南](./TENANT_ONBOARDING.md)
- [灾难恢复计划](./DISASTER_RECOVERY.md)

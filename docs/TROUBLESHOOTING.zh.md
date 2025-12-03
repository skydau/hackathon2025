# MedLogic 平台故障排除指南

本指南提供了在部署和运行 MedLogic 多租户医疗平台时遇到的常见问题的解决方案。

## 目录

1. [部署问题](#部署问题)
2. [服务健康问题](#服务健康问题)
3. [数据库连接](#数据库连接)
4. [Workload Identity 问题](#workload-identity-问题)
5. [网络和路由问题](#网络和路由问题)
6. [租户操作器问题](#租户操作器问题)
7. [性能问题](#性能问题)
8. [安全和策略问题](#安全和策略问题)

## 部署问题

### 问题：Pods 卡在 Pending 状态

**症状：**
```bash
kubectl get pods -n platform-system
NAME                                     READY   STATUS    RESTARTS   AGE
tenant-catalog-service-xxx               0/1     Pending   0          5m
```

**可能原因：**
1. 集群资源不足
2. 镜像拉取错误
3. PersistentVolumeClaim 问题

**诊断：**
```bash
# 检查 pod 事件
kubectl describe pod <pod-name> -n platform-system

# 检查节点资源
kubectl top nodes

# 检查资源配额
kubectl get resourcequota -n platform-system
```

**解决方案：**

**资源不足：**
```bash
# 扩展 AKS 集群
az aks scale \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 5
```

**镜像拉取错误：**
```bash
# 验证 ACR 附加
az aks check-acr \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --acr $ACR_NAME

# 如需要，重新附加 ACR
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --attach-acr $ACR_NAME
```

### 问题：Helm 安装失败

**症状：**
```
Error: INSTALLATION FAILED: timed out waiting for the condition
```

**诊断：**
```bash
# 检查 Helm 发布状态
helm list -n platform-system

# 获取详细状态
helm status medlogic-platform -n platform-system

# 检查失败的 pods
kubectl get pods -n platform-system | grep -v Running
```

**解决方案：**

**增加超时时间：**
```bash
helm upgrade --install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --timeout 15m \
  --wait
```

**调试模式：**
```bash
helm install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --debug \
  --dry-run
```

## 服务健康问题

### 问题：服务健康检查失败

**症状：**
```bash
kubectl get pods -n platform-system
NAME                                     READY   STATUS    RESTARTS   AGE
tenant-catalog-service-xxx               0/1     Running   5          10m
```

**诊断：**
```bash
# 检查 pod 日志
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# 检查存活探针
kubectl describe pod <pod-name> -n platform-system | grep -A 10 "Liveness"

# 手动测试健康端点
kubectl exec -n platform-system <pod-name> -- \
  curl -f http://localhost:8080/health
```

**常见原因和解决方案：**

**数据库连接问题：**
```bash
# 检查数据库连接
kubectl exec -n platform-system <pod-name> -- \
  nc -zv medlogic-sql.database.windows.net 1433

# 验证 Key Vault 中的连接字符串
az keyvault secret show \
  --vault-name $KEY_VAULT_NAME \
  --name TenantCatalog-ConnectionString
```

**缺少环境变量：**
```bash
# 检查 pod 环境
kubectl exec -n platform-system <pod-name> -- env | grep -i azure

# 验证 ConfigMap
kubectl get configmap -n platform-system
kubectl describe configmap <configmap-name> -n platform-system
```

**应用程序错误：**
```bash
# 获取详细日志
kubectl logs -n platform-system <pod-name> --previous

# 检查异常
kubectl logs -n platform-system <pod-name> | grep -i "exception\|error\|fatal"
```

### 问题：服务返回 503 错误

**症状：**
```bash
curl http://<gateway-ip>/api/tenants
503 Service Unavailable
```

**诊断：**
```bash
# 检查服务端点
kubectl get endpoints -n platform-system tenant-catalog

# 检查 pods 是否就绪
kubectl get pods -n platform-system -l app=tenant-catalog-service

# 检查服务定义
kubectl describe svc tenant-catalog -n platform-system
```

**解决方案：**

**没有就绪的 Pods：**
```bash
# 检查就绪探针配置
kubectl get deployment tenant-catalog-service -n platform-system -o yaml | grep -A 10 readinessProbe

# 如需要，调整探针时间
kubectl patch deployment tenant-catalog-service -n platform-system --type='json' \
  -p='[{"op": "replace", "path": "/spec/template/spec/containers/0/readinessProbe/initialDelaySeconds", "value": 30}]'
```

## 数据库连接

### 问题：无法连接到 Azure SQL

**症状：**
```
Error: A network-related or instance-specific error occurred while establishing a connection to SQL Server
```

**诊断：**
```bash
# 从 pod 测试连接
kubectl run sqltest --image=mcr.microsoft.com/mssql-tools -i --rm --restart=Never -- \
  /opt/mssql-tools/bin/sqlcmd -S medlogic-sql.database.windows.net -U sqladmin -P '<password>' -Q "SELECT 1"

# 检查防火墙规则
az sql server firewall-rule list \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME
```

**解决方案：**

**添加防火墙规则：**
```bash
# 允许 AKS 出站 IP
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAKS \
  --start-ip-address <aks-outbound-ip> \
  --end-ip-address <aks-outbound-ip>
```

**启用服务端点：**
```bash
# 获取 AKS 子网
AKS_SUBNET=$(az aks show \
  --resource-group $RESOURCE_GROUP \
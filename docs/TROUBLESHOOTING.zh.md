# MedLogic 平台故障排除指南

本指南提供了在部署和运行 MedLogic 多租户医疗平台时遇到的常见问题的解决方案。

## 目录

1. [本地环境问题](#本地环境问题)
2. [部署问题](#部署问题)
3. [服务健康问题](#服务健康问题)
4. [数据库连接](#数据库连接)
5. [Workload Identity 问题](#workload-identity-问题)
6. [网络和路由问题](#网络和路由问题)
7. [租户操作器问题](#租户操作器问题)
8. [性能问题](#性能问题)
9. [安全和策略问题](#安全和策略问题)

## 本地环境问题

### 问题：Minikube启动失败

**症状：**
```
😄  minikube v1.32.0 on Windows 10
❌  Exiting due to PROVIDER_DOCKER_NOT_RUNNING: "docker version --format -" exit status 1
```

**诊断：**
```bash
# 检查Docker状态
docker version

# 检查Docker守护进程
docker ps
```

**解决方案：**

**Docker未运行：**
```bash
# Windows: 启动Docker Desktop
# Linux: 启动Docker服务
sudo systemctl start docker

# 验证Docker运行
docker run hello-world
```

**驱动问题：**
```bash
# 删除现有集群
minikube delete

# 使用不同驱动重新启动
minikube start --driver=virtualbox  # 或 hyperv, kvm2
```

**资源不足：**
```bash
# 减少资源分配
minikube start --cpus=2 --memory=4096
```

### 问题：Pod无法连接到宿主机SQL Server

**症状：**
```
Error: A network-related or instance-specific error occurred while establishing a connection to SQL Server
```

**诊断：**
```bash
# 测试从Minikube到宿主机的连接
kubectl run -it --rm debug --image=busybox --restart=Never -- \
  nslookup host.minikube.internal

# 测试SQL Server端口
kubectl run -it --rm debug --image=busybox --restart=Never -- \
  telnet host.minikube.internal 1433
```

**解决方案：**

**SQL Server未监听正确端口：**
```powershell
# Windows: 检查SQL Server配置
# 打开SQL Server Configuration Manager
# 启用TCP/IP协议并设置端口为1433

# 重启SQL Server服务
Restart-Service MSSQLSERVER
```

**防火墙阻止连接：**
```powershell
# Windows: 添加防火墙规则
New-NetFirewallRule -DisplayName "SQL Server" `
  -Direction Inbound `
  -Protocol TCP `
  -LocalPort 1433 `
  -Action Allow

# Linux: 配置防火墙
sudo ufw allow 1433/tcp
```

**host.minikube.internal无法解析：**
```bash
# 检查Minikube版本（需要v1.10+）
minikube version

# 如果版本过旧，升级Minikube
# 或使用宿主机IP地址替代host.minikube.internal
```

### 问题：镜像拉取失败 (ImagePullBackOff)

**症状：**
```bash
kubectl get pods -n platform-system
NAME                              READY   STATUS             RESTARTS   AGE
tenant-catalog-xxx                0/1     ImagePullBackOff   0          2m
```

**诊断：**
```bash
# 检查Pod详情
kubectl describe pod <pod-name> -n platform-system

# 检查镜像是否存在
eval $(minikube docker-env)
docker images | grep tenant-catalog
```

**解决方案：**

**未使用Minikube Docker环境：**
```bash
# PowerShell
& minikube -p minikube docker-env --shell powershell | Invoke-Expression

# Bash
eval $(minikube docker-env)

# 重新构建镜像
docker build -t tenant-catalog:latest -f src/TenantCatalogService/Dockerfile .
```

**imagePullPolicy配置错误：**
```bash
# 确保Deployment使用imagePullPolicy: Never
kubectl patch deployment tenant-catalog -n platform-system \
  -p '{"spec":{"template":{"spec":{"containers":[{"name":"tenant-catalog","imagePullPolicy":"Never"}]}}}}'
```

### 问题：Tenant Operator无法创建数据库

**症状：**
```bash
kubectl get tenant hospital-a
NAME         PHASE    AGE
hospital-a   Failed   5m
```

**诊断：**
```bash
# 检查Tenant状态
kubectl get tenant hospital-a -o yaml

# 检查Operator日志
kubectl logs -n platform-system -l app=tenant-operator --tail=100

# 检查SQL Server Secret
kubectl get secret sqlserver-admin-secret -n platform-system -o yaml
```

**解决方案：**

**SQL Server凭据错误：**
```bash
# 更新Secret
kubectl delete secret sqlserver-admin-secret -n platform-system
kubectl create secret generic sqlserver-admin-secret \
  --from-literal=username=sa \
  --from-literal=password="YourCorrectPassword" \
  -n platform-system

# 重启Operator
kubectl rollout restart deployment tenant-operator -n platform-system
```

**数据库权限不足：**
```sql
-- 在SQL Server上执行
USE master;
GO

-- 确保sa账户有足够权限
ALTER SERVER ROLE sysadmin ADD MEMBER sa;
GO
```

**TDE证书未配置：**
```bash
# 检查Operator日志中的TDE错误
kubectl logs -n platform-system -l app=tenant-operator | grep -i "TDE\|certificate"

# 如果TDE失败，可以在setup-sqlserver.sql中禁用TDE
# 或按照文档配置TDE证书
```

### 问题：端到端测试失败

**症状：**
```
✗ 验证数据库在SQL Server上创建
✗ 数据库未创建
```

**诊断：**
```bash
# 运行测试脚本的详细模式
.\scripts\test-e2e.ps1 -SqlServerPassword "YourPassword" -Verbose

# 手动检查数据库
sqlcmd -S localhost -U sa -P "YourPassword" -Q "SELECT name FROM sys.databases"
```

**解决方案：**

**等待时间不足：**
```bash
# 增加等待时间
# 编辑test-e2e.ps1，增加$maxWait值
$maxWait = 300  # 从120增加到300秒
```

**Tenant Operator未运行：**
```bash
# 检查Operator状态
kubectl get pods -n platform-system -l app=tenant-operator

# 如果未运行，重新部署
kubectl apply -f k8s/local/tenant-operator-deployment.yaml
```

### 问题：服务响应缓慢

**症状：**
API请求响应时间超过10秒

**诊断：**
```bash
# 检查资源使用
kubectl top nodes
kubectl top pods -n platform-system

# 检查Minikube资源
minikube status
```

**解决方案：**

**Minikube资源不足：**
```bash
# 停止并删除现有集群
minikube stop
minikube delete

# 使用更多资源重新启动
minikube start --cpus=6 --memory=16384

# 重新部署
.\scripts\deploy-local.ps1 -SqlPassword "YourPassword"
```

**数据库连接池耗尽：**
```bash
# 检查服务日志
kubectl logs -n platform-system -l app=tenant-catalog | grep -i "pool\|connection"

# 增加连接池大小（在appsettings.json中配置）
```

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
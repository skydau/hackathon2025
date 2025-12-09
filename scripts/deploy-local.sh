#!/bin/bash

# 一键部署脚本（Bash版本）
# 将所有平台组件部署到本地Minikube集群

set -e

SQL_PASSWORD="${SQL_PASSWORD:-}"

echo -e "\033[0;36m╔════════════════════════════════════════════════════════════╗\033[0m"
echo -e "\033[0;36m║         多租户医疗平台 - 本地环境部署                        ║\033[0m"
echo -e "\033[0;36m╚════════════════════════════════════════════════════════════╝\033[0m\n"

# 检查SQL Server密码
if [ -z "$SQL_PASSWORD" ]; then
    echo -e "\033[0;31m✗ 请提供SQL Server密码\033[0m"
    echo -e "\033[0;90m  用法: SQL_PASSWORD='YourPassword' ./scripts/deploy-local.sh\033[0m"
    exit 1
fi

# 检查Minikube状态
echo -e "\033[0;33m检查Minikube状态...\033[0m"
if ! minikube status | grep -q "host: Running"; then
    echo -e "\033[0;31m✗ Minikube未运行\033[0m"
    echo -e "\033[0;90m  请先启动Minikube: ./scripts/start-minikube.sh\033[0m"
    exit 1
fi

echo -e "\033[0;32m✓ Minikube正在运行\033[0m"

# 步骤1: 创建命名空间
echo -e "\n\033[0;33m[1/7] 创建命名空间...\033[0m"
kubectl apply -f k8s/local/namespace.yaml

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ 命名空间创建成功\033[0m"
else
    echo -e "\033[0;31m✗ 命名空间创建失败\033[0m"
    exit 1
fi

# 步骤2: 创建SQL Server Secret
echo -e "\n\033[0;33m[2/7] 创建SQL Server Secret...\033[0m"
kubectl create secret generic sqlserver-admin-secret \
    --from-literal=username=sa \
    --from-literal=password="$SQL_PASSWORD" \
    --from-literal=server="host.minikube.internal,1433" \
    -n platform-system \
    --dry-run=client -o yaml | kubectl apply -f -

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ SQL Server Secret创建成功\033[0m"
else
    echo -e "\033[0;31m✗ SQL Server Secret创建失败\033[0m"
    exit 1
fi

# 步骤3: 安装Tenant CRD
echo -e "\n\033[0;33m[3/7] 安装Tenant CRD...\033[0m"

if [ -f "tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml" ]; then
    kubectl apply -f tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml
    
    if [ $? -eq 0 ]; then
        echo -e "\033[0;32m✓ Tenant CRD安装成功\033[0m"
    else
        echo -e "\033[0;31m✗ Tenant CRD安装失败\033[0m"
        exit 1
    fi
else
    echo -e "\033[0;33m⚠ Tenant CRD文件不存在，跳过\033[0m"
fi

# 步骤4: 部署Tenant Operator
echo -e "\n\033[0;33m[4/7] 部署Tenant Operator...\033[0m"
kubectl apply -f k8s/local/tenant-operator-deployment.yaml

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ Tenant Operator部署成功\033[0m"
else
    echo -e "\033[0;31m✗ Tenant Operator部署失败\033[0m"
    exit 1
fi

# 步骤5: 部署Tenant Catalog Service
echo -e "\n\033[0;33m[5/7] 部署Tenant Catalog Service...\033[0m"
kubectl apply -f k8s/local/tenant-catalog-deployment.yaml

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ Tenant Catalog Service部署成功\033[0m"
else
    echo -e "\033[0;31m✗ Tenant Catalog Service部署失败\033[0m"
    exit 1
fi

# 步骤6: 部署Device Registry Service
echo -e "\n\033[0;33m[6/7] 部署Device Registry Service...\033[0m"
kubectl apply -f k8s/local/device-registry-deployment.yaml

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ Device Registry Service部署成功\033[0m"
else
    echo -e "\033[0;31m✗ Device Registry Service部署失败\033[0m"
    exit 1
fi

# 步骤7: 部署Smart Gateway
echo -e "\n\033[0;33m[7/7] 部署Smart Gateway...\033[0m"
kubectl apply -f k8s/local/smart-gateway-deployment.yaml

if [ $? -eq 0 ]; then
    echo -e "\033[0;32m✓ Smart Gateway部署成功\033[0m"
else
    echo -e "\033[0;31m✗ Smart Gateway部署失败\033[0m"
    exit 1
fi

# 等待Pod就绪
echo -e "\n\033[0;33m等待Pod就绪...\033[0m"
echo -e "\033[0;90m  这可能需要几分钟时间...\033[0m"

sleep 10

# 检查Pod状态
echo -e "\n\033[0;36mPod状态:\033[0m"
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 显示服务端点
MINIKUBE_IP=$(minikube ip)
echo -e "\n\033[0;36m服务端点:\033[0m"
echo -e "\033[0;90m  Tenant Catalog: http://$MINIKUBE_IP:30080\033[0m"
echo -e "\033[0;90m  Device Registry: http://$MINIKUBE_IP:30081\033[0m"
echo -e "\033[0;90m  Smart Gateway: http://$MINIKUBE_IP:30000\033[0m"

echo -e "\n\033[0;32m✓ 部署完成\033[0m"
echo -e "\n\033[0;33m下一步:\033[0m"
echo -e "\033[0;90m  1. 创建测试租户: kubectl apply -f tenant-operator/config/samples/hospital-a.yaml\033[0m"
echo -e "\033[0;90m  2. 运行端到端测试: SQL_SERVER_PASSWORD='$SQL_PASSWORD' ./scripts/test-e2e.sh\033[0m"
echo -e "\033[0;90m  3. 查看日志: kubectl logs -n platform-system -l app=tenant-operator -f\033[0m"

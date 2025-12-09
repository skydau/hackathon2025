#!/bin/bash

# Minikube启动脚本（Bash版本）
# 用于在Linux/macOS环境启动和配置Minikube

set -e

# 默认配置
CPUS=${CPUS:-4}
MEMORY=${MEMORY:-8192mb}
DRIVER=${DRIVER:-docker}

echo -e "\033[0;36m启动Minikube集群...\033[0m"
echo -e "\033[0;90m配置: CPU=$CPUS, Memory=$MEMORY, Driver=$DRIVER\033[0m"

# 启动Minikube
minikube start \
    --cpus=$CPUS \
    --memory=$MEMORY \
    --driver=$DRIVER \
    --kubernetes-version=v1.28.0

echo -e "\033[0;32m✓ Minikube启动成功\033[0m"

# 启用必需的插件
echo -e "\n\033[0;36m启用Minikube插件...\033[0m"

addons=("metrics-server" "ingress")

for addon in "${addons[@]}"; do
    echo -e "\033[0;90m  启用 $addon...\033[0m"
    if minikube addons enable "$addon"; then
        echo -e "\033[0;32m  ✓ $addon 已启用\033[0m"
    else
        echo -e "\033[0;33m  ✗ $addon 启用失败\033[0m"
    fi
done

# 显示集群信息
echo -e "\n\033[0;36m集群信息:\033[0m"
minikube status

echo -e "\n\033[0;36mKubernetes版本:\033[0m"
kubectl version --short

echo -e "\n\033[0;36m节点信息:\033[0m"
kubectl get nodes

echo -e "\n\033[0;32m✓ Minikube配置完成\033[0m"
echo -e "\n\033[0;33m下一步:\033[0m"
echo -e "\033[0;90m  1. 配置SQL Server: sqlcmd -S localhost -U sa -i scripts/sql/setup-sqlserver.sql\033[0m"
echo -e "\033[0;90m  2. 构建镜像: ./scripts/build-images.sh\033[0m"
echo -e "\033[0;90m  3. 部署平台: ./scripts/deploy-local.sh\033[0m"

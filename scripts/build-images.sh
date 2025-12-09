#!/bin/bash

# 本地镜像构建脚本（Bash版本）
# 构建所有平台服务的Docker镜像

set -e

SKIP_TESTS=${SKIP_TESTS:-false}

echo -e "\033[0;36m╔════════════════════════════════════════════════════════════╗\033[0m"
echo -e "\033[0;36m║         多租户医疗平台 - 本地镜像构建                        ║\033[0m"
echo -e "\033[0;36m╚════════════════════════════════════════════════════════════╝\033[0m\n"

# 设置Minikube Docker环境
echo -e "\033[0;33m配置Docker环境...\033[0m"
eval $(minikube -p minikube docker-env)

if [ $? -ne 0 ]; then
    echo -e "\033[0;31m✗ 无法配置Minikube Docker环境\033[0m"
    echo -e "\033[0;90m  请确保Minikube正在运行: minikube start\033[0m"
    exit 1
fi

echo -e "\033[0;32m✓ Docker环境已配置为使用Minikube\033[0m"

# 构建.NET服务镜像
declare -a dotnet_services=(
    "Tenant Catalog Service:src/TenantCatalogService:tenant-catalog:latest"
    "Device Registry Service:src/DeviceRegistryService:device-registry:latest"
    "MedLogic Service:src/MedLogicService:medlogic-service:latest"
    "Admin UI:src/AdminUI:admin-ui:latest"
)

for service_info in "${dotnet_services[@]}"; do
    IFS=':' read -r name path tag <<< "$service_info"
    
    echo -e "\n\033[0;33m构建 $name...\033[0m"
    
    if [ -f "$path/Dockerfile" ]; then
        docker build -t "$tag" -f "$path/Dockerfile" .
        
        if [ $? -eq 0 ]; then
            echo -e "\033[0;32m✓ $name 构建成功\033[0m"
        else
            echo -e "\033[0;31m✗ $name 构建失败\033[0m"
            exit 1
        fi
    else
        echo -e "\033[0;33m⚠ Dockerfile不存在: $path/Dockerfile\033[0m"
    fi
done

# 构建Smart Gateway镜像
echo -e "\n\033[0;33m构建 Smart Gateway...\033[0m"

if [ -f "nginx/Dockerfile" ]; then
    docker build -t smart-gateway:latest -f nginx/Dockerfile nginx/
    
    if [ $? -eq 0 ]; then
        echo -e "\033[0;32m✓ Smart Gateway 构建成功\033[0m"
    else
        echo -e "\033[0;31m✗ Smart Gateway 构建失败\033[0m"
        exit 1
    fi
else
    echo -e "\033[0;33m⚠ Dockerfile不存在: nginx/Dockerfile\033[0m"
fi

# 构建Tenant Operator镜像
echo -e "\n\033[0;33m构建 Tenant Operator...\033[0m"

if [ -f "tenant-operator/Dockerfile" ]; then
    pushd tenant-operator > /dev/null
    
    # 构建Go二进制文件
    echo -e "\033[0;90m  编译Go代码...\033[0m"
    CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -o manager main.go
    
    if [ $? -eq 0 ]; then
        echo -e "\033[0;32m  ✓ Go编译成功\033[0m"
    else
        echo -e "\033[0;31m  ✗ Go编译失败\033[0m"
        popd > /dev/null
        exit 1
    fi
    
    # 构建Docker镜像
    docker build -t tenant-operator:latest .
    
    if [ $? -eq 0 ]; then
        echo -e "\033[0;32m✓ Tenant Operator 构建成功\033[0m"
    else
        echo -e "\033[0;31m✗ Tenant Operator 构建失败\033[0m"
        popd > /dev/null
        exit 1
    fi
    
    popd > /dev/null
else
    echo -e "\033[0;33m⚠ Dockerfile不存在: tenant-operator/Dockerfile\033[0m"
fi

# 显示构建的镜像
echo -e "\n\033[0;36m构建的镜像列表:\033[0m"
docker images | grep -E "tenant-catalog|device-registry|medlogic-service|admin-ui|smart-gateway|tenant-operator"

echo -e "\n\033[0;32m✓ 所有镜像构建完成\033[0m"
echo -e "\n\033[0;33m下一步: 部署到Minikube\033[0m"
echo -e "\033[0;90m  ./scripts/deploy-local.sh\033[0m"

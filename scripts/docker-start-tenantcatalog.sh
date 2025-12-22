#!/bin/bash
# 启动 TenantCatalogService 和 SQL Server 的 Docker 服务
# Bash 脚本

echo "启动 TenantCatalogService Docker 服务..."

# 检查 Docker 是否运行
if ! docker version > /dev/null 2>&1; then
    echo "✗ Docker 未运行，请先启动 Docker"
    exit 1
fi
echo "✓ Docker 正在运行"

# 构建并启动服务
echo "构建并启动服务..."
docker-compose -f docker-compose.tenantcatalog.yml up --build -d

# 等待服务启动
echo "等待服务启动..."
sleep 30

# 检查服务状态
echo ""
echo "检查服务状态:"
docker-compose -f docker-compose.tenantcatalog.yml ps

# 检查健康状态
echo ""
echo "等待健康检查..."
max_attempts=12
attempt=0

while [ $attempt -lt $max_attempts ]; do
    attempt=$((attempt + 1))
    echo "健康检查尝试 $attempt/$max_attempts..."
    
    if curl -f http://localhost:8080/health > /dev/null 2>&1; then
        echo "✓ TenantCatalogService 健康检查通过!"
        break
    else
        echo "健康检查失败，等待重试..."
    fi
    
    sleep 10
done

if [ $attempt -eq $max_attempts ]; then
    echo "✗ 服务启动超时，请检查日志"
    echo "查看日志命令: docker-compose -f docker-compose.tenantcatalog.yml logs"
else
    echo ""
    echo "🎉 服务启动成功!"
    echo "TenantCatalogService: http://localhost:8080"
    echo "Swagger UI: http://localhost:8080/swagger"
    echo "健康检查: http://localhost:8080/health"
    echo ""
    echo "管理命令:"
    echo "  查看日志: docker-compose -f docker-compose.tenantcatalog.yml logs -f"
    echo "  停止服务: docker-compose -f docker-compose.tenantcatalog.yml down"
    echo "  重启服务: docker-compose -f docker-compose.tenantcatalog.yml restart"
fi
# 停止 TenantCatalogService Docker 服务
# PowerShell 脚本

Write-Host "停止 TenantCatalogService Docker 服务..." -ForegroundColor Yellow

# 停止并移除容器
docker-compose -f docker-compose.tenantcatalog.yml down

Write-Host "✓ 服务已停止" -ForegroundColor Green

# 询问是否清理数据
$cleanData = Read-Host "是否清理数据库数据? (y/N)"
if ($cleanData -eq 'y' -or $cleanData -eq 'Y') {
    Write-Host "清理数据库数据..." -ForegroundColor Yellow
    docker-compose -f docker-compose.tenantcatalog.yml down -v
    Write-Host "✓ 数据已清理" -ForegroundColor Green
}
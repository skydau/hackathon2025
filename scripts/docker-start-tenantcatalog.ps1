# 启动 TenantCatalogService 和 SQL Server 的 Docker 服务
# PowerShell 脚本

Write-Host "启动 TenantCatalogService Docker 服务..." -ForegroundColor Green

# 检查 Docker 是否运行
try {
    docker version | Out-Null
    Write-Host "✓ Docker 正在运行" -ForegroundColor Green
} catch {
    Write-Host "✗ Docker 未运行，请先启动 Docker Desktop" -ForegroundColor Red
    exit 1
}

# 构建并启动服务
Write-Host "构建并启动服务..." -ForegroundColor Yellow
docker-compose -f docker-compose.tenantcatalog.yml up --build -d

# 等待服务启动
Write-Host "等待服务启动..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# 检查服务状态
Write-Host "`n检查服务状态:" -ForegroundColor Cyan
docker-compose -f docker-compose.tenantcatalog.yml ps

# 检查健康状态
Write-Host "`n等待健康检查..." -ForegroundColor Yellow
$maxAttempts = 12
$attempt = 0

do {
    $attempt++
    Write-Host "健康检查尝试 $attempt/$maxAttempts..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8080/health" -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            Write-Host "✓ TenantCatalogService 健康检查通过!" -ForegroundColor Green
            break
        }
    } catch {
        Write-Host "健康检查失败，等待重试..." -ForegroundColor Yellow
    }
    
    Start-Sleep -Seconds 10
} while ($attempt -lt $maxAttempts)

if ($attempt -eq $maxAttempts) {
    Write-Host "✗ 服务启动超时，请检查日志" -ForegroundColor Red
    Write-Host "查看日志命令: docker-compose -f docker-compose.tenantcatalog.yml logs" -ForegroundColor Yellow
} else {
    Write-Host "`n🎉 服务启动成功!" -ForegroundColor Green
    Write-Host "TenantCatalogService: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "Swagger UI: http://localhost:8080/swagger" -ForegroundColor Cyan
    Write-Host "健康检查: http://localhost:8080/health" -ForegroundColor Cyan
    Write-Host "`n管理命令:" -ForegroundColor Yellow
    Write-Host "  查看日志: docker-compose -f docker-compose.tenantcatalog.yml logs -f" -ForegroundColor White
    Write-Host "  停止服务: docker-compose -f docker-compose.tenantcatalog.yml down" -ForegroundColor White
    Write-Host "  重启服务: docker-compose -f docker-compose.tenantcatalog.yml restart" -ForegroundColor White
}
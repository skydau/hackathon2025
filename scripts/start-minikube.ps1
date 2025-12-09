# Minikube启动脚本（PowerShell版本）
# 用于在Windows环境启动和配置Minikube

param(
    [int]$Cpus = 4,
    [string]$Memory = "8192mb",
    [string]$Driver = "docker"
)

Write-Host "启动Minikube集群..." -ForegroundColor Cyan
Write-Host "配置: CPU=$Cpus, Memory=$Memory, Driver=$Driver" -ForegroundColor Gray

# 启动Minikube
minikube start `
    --cpus=$Cpus `
    --memory=$Memory `
    --driver=$Driver `
    --kubernetes-version=v1.28.0

if ($LASTEXITCODE -ne 0) {
    Write-Host "Minikube启动失败" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Minikube启动成功" -ForegroundColor Green

# 启用必需的插件
Write-Host "`n启用Minikube插件..." -ForegroundColor Cyan

$addons = @("metrics-server", "ingress")

foreach ($addon in $addons) {
    Write-Host "  启用 $addon..." -ForegroundColor Gray
    minikube addons enable $addon
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ $addon 已启用" -ForegroundColor Green
    } else {
        Write-Host "  ✗ $addon 启用失败" -ForegroundColor Yellow
    }
}

# 显示集群信息
Write-Host "`n集群信息:" -ForegroundColor Cyan
minikube status

Write-Host "`nKubernetes版本:" -ForegroundColor Cyan
kubectl version --short

Write-Host "`n节点信息:" -ForegroundColor Cyan
kubectl get nodes

Write-Host "`n✓ Minikube配置完成" -ForegroundColor Green
Write-Host "`n下一步:" -ForegroundColor Yellow
Write-Host "  1. 配置SQL Server: sqlcmd -S localhost -U sa -i scripts/sql/setup-sqlserver.sql" -ForegroundColor Gray
Write-Host "  2. 构建镜像: .\scripts\build-images.ps1" -ForegroundColor Gray
Write-Host "  3. 部署平台: .\scripts\deploy-local.ps1" -ForegroundColor Gray

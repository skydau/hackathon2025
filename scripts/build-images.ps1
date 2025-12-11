# 本地镜像构建脚本（PowerShell版本）
# 构建所有平台服务的Docker镜像

param(
    [switch]$SkipTests = $false
)

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         多租户医疗平台 - 本地镜像构建                        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# 设置Minikube Docker环境
Write-Host "配置Docker环境..." -ForegroundColor Yellow
& minikube -p minikube docker-env --shell powershell | Invoke-Expression

if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ 无法配置Minikube Docker环境" -ForegroundColor Red
    Write-Host "  请确保Minikube正在运行: minikube start" -ForegroundColor Gray
    exit 1
}

Write-Host "✓ Docker环境已配置为使用Minikube" -ForegroundColor Green

# 构建.NET服务镜像
$dotnetServices = @(
    @{Name="Tenant Catalog Service"; Path="src/TenantCatalogService"; Tag="tenant-catalog:latest"},
    @{Name="Device Registry Service"; Path="src/DeviceRegistryService"; Tag="device-registry:latest"},
    @{Name="MedLogic Service"; Path="src/MedLogicService"; Tag="medlogic-service:latest"},
    @{Name="Admin UI"; Path="src/AdminUI"; Tag="admin-ui:latest"}
)

foreach ($service in $dotnetServices) {
    Write-Host "`n构建 $($service.Name)..." -ForegroundColor Yellow
    
    if (Test-Path "$($service.Path)/Dockerfile") {
        docker build -t $service.Tag -f "$($service.Path)/Dockerfile" .
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ $($service.Name) 构建成功" -ForegroundColor Green
        } else {
            Write-Host "✗ $($service.Name) 构建失败" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "⚠ Dockerfile不存在: $($service.Path)/Dockerfile" -ForegroundColor Yellow
    }
}

# 构建Smart Gateway镜像
Write-Host "`n构建 Smart Gateway..." -ForegroundColor Yellow

if (Test-Path "nginx/Dockerfile") {
    docker build -t smart-gateway:latest -f nginx/Dockerfile nginx/
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Smart Gateway 构建成功" -ForegroundColor Green
    } else {
        Write-Host "✗ Smart Gateway 构建失败" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "⚠ Dockerfile不存在: nginx/Dockerfile" -ForegroundColor Yellow
}

# 构建Tenant Operator镜像
Write-Host "`n构建 Tenant Operator..." -ForegroundColor Yellow

if (Test-Path "tenant-operator/Dockerfile") {
    Push-Location tenant-operator
    
    # 构建Go二进制文件
    Write-Host "  编译Go代码..." -ForegroundColor Gray
    $env:CGO_ENABLED = "0"
    $env:GOOS = "linux"
    $env:GOARCH = "amd64"
    go build -o manager main.go
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Go编译成功" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Go编译失败" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    # 构建Docker镜像
    docker build -t tenant-operator:latest .
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Tenant Operator 构建成功" -ForegroundColor Green
    } else {
        Write-Host "✗ Tenant Operator 构建失败" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Pop-Location
} else {
    Write-Host "⚠ Dockerfile不存在: tenant-operator/Dockerfile" -ForegroundColor Yellow
}

# 显示构建的镜像
Write-Host "`n构建的镜像列表:" -ForegroundColor Cyan
docker images | Select-String -Pattern "tenant-catalog|device-registry|medlogic-service|admin-ui|smart-gateway|tenant-operator"

Write-Host "`n✓ 所有镜像构建完成" -ForegroundColor Green
Write-Host "`n下一步: 部署到Minikube" -ForegroundColor Yellow
Write-Host "  .\scripts\deploy-local.ps1" -ForegroundColor Gray

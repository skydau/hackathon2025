# 一键部署脚本（PowerShell版本）
# 将所有平台组件部署到本地Minikube集群

param(
    [string]$SqlPassword = ""
)

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         多租户医疗平台 - 本地环境部署                        ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# 检查SQL Server密码
if ([string]::IsNullOrEmpty($SqlPassword)) {
    Write-Host "✗ 请提供SQL Server密码" -ForegroundColor Red
    Write-Host "  用法: .\scripts\deploy-local.ps1 -SqlPassword 'YourPassword'" -ForegroundColor Gray
    exit 1
}

# 检查Minikube状态
Write-Host "检查Minikube状态..." -ForegroundColor Yellow
$minikubeStatus = minikube status --format='{{.Host}}' 2>$null

if ($minikubeStatus -ne "Running") {
    Write-Host "✗ Minikube未运行" -ForegroundColor Red
    Write-Host "  请先启动Minikube: .\scripts\start-minikube.ps1" -ForegroundColor Gray
    exit 1
}

Write-Host "✓ Minikube正在运行" -ForegroundColor Green

# 步骤1: 创建命名空间
Write-Host "`n[1/7] 创建命名空间..." -ForegroundColor Yellow
kubectl apply -f k8s/local/namespace.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ 命名空间创建成功" -ForegroundColor Green
} else {
    Write-Host "✗ 命名空间创建失败" -ForegroundColor Red
    exit 1
}

# 步骤2: 创建SQL Server Secret
Write-Host "`n[2/7] 创建SQL Server Secret..." -ForegroundColor Yellow
kubectl create secret generic sqlserver-admin-secret `
    --from-literal=username=sa `
    --from-literal=password=$SqlPassword `
    --from-literal=server="host.minikube.internal,1433" `
    -n platform-system `
    --dry-run=client -o yaml | kubectl apply -f -

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ SQL Server Secret创建成功" -ForegroundColor Green
} else {
    Write-Host "✗ SQL Server Secret创建失败" -ForegroundColor Red
    exit 1
}

# 步骤3: 安装Tenant CRD
Write-Host "`n[3/7] 安装Tenant CRD..." -ForegroundColor Yellow

if (Test-Path "tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml") {
    kubectl apply -f tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Tenant CRD安装成功" -ForegroundColor Green
    } else {
        Write-Host "✗ Tenant CRD安装失败" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "⚠ Tenant CRD文件不存在，跳过" -ForegroundColor Yellow
}

# 步骤4: 部署Tenant Operator
Write-Host "`n[4/7] 部署Tenant Operator..." -ForegroundColor Yellow
kubectl apply -f k8s/local/tenant-operator-deployment.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Tenant Operator部署成功" -ForegroundColor Green
} else {
    Write-Host "✗ Tenant Operator部署失败" -ForegroundColor Red
    exit 1
}

# 步骤5: 部署Tenant Catalog Service
Write-Host "`n[5/7] 部署Tenant Catalog Service..." -ForegroundColor Yellow
kubectl apply -f k8s/local/tenant-catalog-deployment.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Tenant Catalog Service部署成功" -ForegroundColor Green
} else {
    Write-Host "✗ Tenant Catalog Service部署失败" -ForegroundColor Red
    exit 1
}

# 步骤6: 部署Device Registry Service
Write-Host "`n[6/7] 部署Device Registry Service..." -ForegroundColor Yellow
kubectl apply -f k8s/local/device-registry-deployment.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Device Registry Service部署成功" -ForegroundColor Green
} else {
    Write-Host "✗ Device Registry Service部署失败" -ForegroundColor Red
    exit 1
}

# 步骤7: 部署Smart Gateway
Write-Host "`n[7/7] 部署Smart Gateway..." -ForegroundColor Yellow
kubectl apply -f k8s/local/smart-gateway-deployment.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ Smart Gateway部署成功" -ForegroundColor Green
} else {
    Write-Host "✗ Smart Gateway部署失败" -ForegroundColor Red
    exit 1
}

# 等待Pod就绪
Write-Host "`n等待Pod就绪..." -ForegroundColor Yellow
Write-Host "  这可能需要几分钟时间..." -ForegroundColor Gray

Start-Sleep -Seconds 10

# 检查Pod状态
Write-Host "`nPod状态:" -ForegroundColor Cyan
kubectl get pods -n platform-system
kubectl get pods -n gateway

# 显示服务端点
Write-Host "`n服务端点:" -ForegroundColor Cyan
Write-Host "  Tenant Catalog: http://$(minikube ip):30080" -ForegroundColor Gray
Write-Host "  Device Registry: http://$(minikube ip):30081" -ForegroundColor Gray
Write-Host "  Smart Gateway: http://$(minikube ip):30000" -ForegroundColor Gray

Write-Host "`n✓ 部署完成" -ForegroundColor Green
Write-Host "`n下一步:" -ForegroundColor Yellow
Write-Host "  1. 创建测试租户: kubectl apply -f tenant-operator/config/samples/hospital-a.yaml" -ForegroundColor Gray
Write-Host "  2. 运行端到端测试: .\scripts\test-e2e.ps1 -SqlServerPassword '$SqlPassword'" -ForegroundColor Gray
Write-Host "  3. 查看日志: kubectl logs -n platform-system -l app=tenant-operator -f" -ForegroundColor Gray

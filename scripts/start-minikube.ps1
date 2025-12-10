
# Minikube start script (PowerShell)
# Windows-friendly, ASCII-only, PS 5.1 compatible

param(
    [int]$Cpus = 4,
    [string]$Memory = "8192mb",
    [ValidateSet("docker","hyperv","virtualbox","none")]
    [string]$Driver = "hyperv"
)

$ErrorActionPreference = "Continue"

function Fail($msg) {
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

function Check-Cmd($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Fail "Command not found: $name. Please install and add to PATH."
    }
}

Check-Cmd "minikube"
Check-Cmd "kubectl"

Write-Host "Starting Minikube..." -ForegroundColor Cyan
Write-Host "Config: CPU=$Cpus, Memory=$Memory, Driver=$Driver" -ForegroundColor Gray

# IMPORTANT: pass args explicitly; do NOT use splatting for dashed keys
minikube start `
    --cpus $Cpus `
    --memory $Memory `
    --driver $Driver `
    --kubernetes-version v1.28.0

if ($LASTEXITCODE -ne 0) { Fail "Minikube start failed (exit code $LASTEXITCODE)" }

Write-Host "Minikube started successfully." -ForegroundColor Green

# ensure kubectl context
kubectl config use-context minikube | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "kubectl context switch to 'minikube' failed (exit code $LASTEXITCODE)" }

Write-Host "`nEnabling addons..." -ForegroundColor Cyan
$addons = @("metrics-server", "ingress")

foreach ($addon in $addons) {
    Write-Host "  Enabling $addon..." -ForegroundColor Gray
    minikube addons enable $addon
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK: $addon enabled" -ForegroundColor Green
    } else {
        Write-Host "  WARN: $addon enable failed (exit code $LASTEXITCODE)" -ForegroundColor Yellow
    }
}

# wait for ingress controller if exists
$ingNsExists = (kubectl get ns ingress-nginx -o name 2>$null)
if ($ingNsExists) {
    Write-Host "`nWaiting for Ingress Controller rollout..." -ForegroundColor Cyan
    kubectl -n ingress-nginx rollout status deployment/ingress-nginx-controller --timeout=180s
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  WARN: Ingress Controller not ready within timeout (exit code $LASTEXITCODE)" -ForegroundColor Yellow
    }
}

Write-Host "`nCluster status:" -ForegroundColor Cyan
minikube status

Write-Host "`nKubernetes version:" -ForegroundColor Cyan
kubectl version --short

Write-Host "`nNodes:" -ForegroundColor Cyan
kubectl get nodes

Write-Host "`nDone." -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1) Configure SQL Server: sqlcmd -S localhost -U sa -i scripts/sql/setup-sqlserver.sql" -ForegroundColor Gray
Write-Host "  2) Build images: .\scripts\build-images.ps1" -ForegroundColor Gray
Write-Host "  3) Deploy platform: .\scripts\deploy-local.ps1" -ForegroundColor Gray

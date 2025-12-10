
# One-click Deployment Script (PowerShell)
# Deploy all platform components to local Minikube cluster

param(
    [Parameter(Mandatory = $true)]
    [string]$SqlPassword
)

$ErrorActionPreference = 'Stop'

Write-Host "=== Multi-tenant Medical Platform - Local Deployment ===`n" -ForegroundColor Cyan

# Check SQL Server password
if ([string]::IsNullOrWhiteSpace($SqlPassword)) {
    Write-Host "ERROR: Please provide SQL Server password." -ForegroundColor Red
    Write-Host "Usage: .\scripts\deploy-local.ps1 -SqlPassword 'YourPassword'" -ForegroundColor Gray
    exit 1
}

# Check Minikube status
Write-Host "Checking Minikube status..." -ForegroundColor Yellow
$minikubeStatus = & minikube status --format='{{.Host}}' 2>$null

if ($minikubeStatus -ne 'Running') {
    Write-Host "ERROR: Minikube is not running." -ForegroundColor Red
    Write-Host "Start Minikube first: .\scripts\start-minikube.ps1" -ForegroundColor Gray
    exit 1
} else {
    Write-Host "Minikube is running." -ForegroundColor Green
}

# Step 1: Create namespaces
Write-Host "`n[1/7] Creating namespaces..." -ForegroundColor Yellow
& kubectl apply -f k8s/local/namespace.yaml
if (-not $?) {
    Write-Host "ERROR: Failed to create namespaces." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Namespaces created successfully." -ForegroundColor Green
}

# Step 2: Create SQL Server Secret (build YAML first, then apply)
Write-Host "`n[2/7] Creating SQL Server Secret..." -ForegroundColor Yellow
$secretYaml = & kubectl create secret generic sqlserver-admin-secret `
    --from-literal=username=sa `
    --from-literal=password="$SqlPassword" `
    --from-literal=server="host.minikube.internal,1433" `
    -n platform-system `
    --dry-run=client -o yaml

if (-not $?) {
    Write-Host "ERROR: Failed to prepare SQL Server Secret YAML." -ForegroundColor Red
    exit 1
}

$null = $secretYaml | kubectl apply -f -
if (-not $?) {
    Write-Host "ERROR: Failed to create SQL Server Secret." -ForegroundColor Red
    exit 1
} else {
    Write-Host "SQL Server Secret created successfully." -ForegroundColor Green
}

# Step 3: Install Tenant CRD
Write-Host "`n[3/7] Installing Tenant CRD..." -ForegroundColor Yellow
$crdPath = "tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml"
if (Test-Path $crdPath) {
    & kubectl apply -f $crdPath
    if (-not $?) {
        Write-Host "ERROR: Failed to install Tenant CRD." -ForegroundColor Red
        exit 1
    } else {
        Write-Host "Tenant CRD installed successfully." -ForegroundColor Green
    }
} else {
    Write-Host "WARNING: Tenant CRD file not found, skipping." -ForegroundColor Yellow
}

# Step 4: Deploy Tenant Operator
Write-Host "`n[4/7] Deploying Tenant Operator..." -ForegroundColor Yellow
& kubectl apply -f k8s/local/tenant-operator-deployment.yaml
if (-not $?) {
    Write-Host "ERROR: Failed to deploy Tenant Operator." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Tenant Operator deployed successfully." -ForegroundColor Green
}

# Step 5: Deploy Tenant Catalog Service
Write-Host "`n[5/7] Deploying Tenant Catalog Service..." -ForegroundColor Yellow
& kubectl apply -f k8s/local/tenant-catalog-deployment.yaml
if (-not $?) {
    Write-Host "ERROR: Failed to deploy Tenant Catalog Service." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Tenant Catalog Service deployed successfully." -ForegroundColor Green
}

# Step 6: Deploy Device Registry Service
Write-Host "`n[6/7] Deploying Device Registry Service..." -ForegroundColor Yellow
& kubectl apply -f k8s/local/device-registry-deployment.yaml
if (-not $?) {
    Write-Host "ERROR: Failed to deploy Device Registry Service." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Device Registry Service deployed successfully." -ForegroundColor Green
}

# Step 7: Deploy Smart Gateway
Write-Host "`n[7/7] Deploying Smart Gateway..." -ForegroundColor Yellow
& kubectl apply -f k8s/local/smart-gateway-deployment.yaml
if (-not $?) {
    Write-Host "ERROR: Failed to deploy Smart Gateway." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Smart Gateway deployed successfully." -ForegroundColor Green
}

# Wait for Pods to be ready
Write-Host "`nWaiting for Pods to become Ready..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Gray
Start-Sleep -Seconds 10

# Show Pod status
Write-Host "`nPod status:" -ForegroundColor Cyan
& kubectl get pods -n platform-system
& kubectl get pods -n gateway

# Show service endpoints
$miniIp = (& minikube ip).Trim()
Write-Host "`nService endpoints:" -ForegroundColor Cyan
Write-Host ("Tenant Catalog:  http://{0}:30080" -f $miniIp) -ForegroundColor Gray
Write-Host ("Device Registry: http://{0}:30081" -f $miniIp) -ForegroundColor Gray
Write-Host ("Smart Gateway:   http://{0}:30000" -f $miniIp) -ForegroundColor Gray

Write-Host "`nDeployment completed." -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1) Create test tenant: kubectl apply -f tenant-operator/config/samples/hospital-a.yaml" -ForegroundColor Gray
Write-Host ("2) Run end-to-end tests: .\scripts\test-e2e.ps1 -SqlServerPassword '{0}'" -f $SqlPassword) -ForegroundColor Gray
Write-Host "3) View logs: kubectl logs -n platform-system -l app=tenant-operator -f" -ForegroundColor Gray

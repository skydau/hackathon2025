param(
    [string]$TenantName = "test-hospital",
    [string]$SqlServerPassword = ""
)

if ([string]::IsNullOrWhiteSpace($SqlServerPassword)) {
    Write-Host "ERROR: Please provide SQL Server password." -ForegroundColor Red
    Write-Host "Usage: .\scripts\test-e2e-working.ps1 -SqlServerPassword 'YourPassword'" -ForegroundColor Gray
    exit 1
}

Write-Host "=== Multi-tenant Medical Platform - E2E Test ===" -ForegroundColor Cyan

# Check Minikube
Write-Host "Checking Minikube status..." -ForegroundColor Yellow
$minikubeStatus = minikube status --format='{{.Host}}' 2>$null
if ($minikubeStatus -ne "Running") {
    Write-Host "ERROR: Minikube not running" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: Minikube is running" -ForegroundColor Green

# Cleanup
Write-Host "Cleaning up previous resources..." -ForegroundColor Yellow
kubectl delete tenant $TenantName --ignore-not-found=true 2>$null
kubectl delete namespace "tenant-$TenantName" --ignore-not-found=true 2>$null
Start-Sleep -Seconds 5
Write-Host "SUCCESS: Cleanup completed" -ForegroundColor Green

# Create Tenant
Write-Host "Creating Tenant CRD..." -ForegroundColor Yellow
$tenantYaml = @"
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: $TenantName
spec:
  displayName: "Test Hospital"
  db:
    mode: perDatabase
    server: "host.minikube.internal,1433"
    database: ${TenantName}_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
"@

$tenantYaml | kubectl apply -f - 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Tenant CRD created" -ForegroundColor Green
} else {
    Write-Host "ERROR: Failed to create Tenant CRD" -ForegroundColor Red
    exit 1
}

# Wait for processing
Write-Host "Waiting for Tenant Operator..." -ForegroundColor Yellow
$maxWait = 60
$waited = 0
while ($waited -lt $maxWait) {
    $phase = kubectl get tenant $TenantName -o jsonpath='{.status.phase}' 2>$null
    if ($phase -eq "Ready") {
        Write-Host "SUCCESS: Tenant is Ready" -ForegroundColor Green
        break
    }
    Write-Host "INFO: Tenant status: $phase (waiting $waited/$maxWait seconds)" -ForegroundColor Cyan
    Start-Sleep -Seconds 5
    $waited += 5
}

# Check database
Write-Host "Checking database creation..." -ForegroundColor Yellow
$dbName = "${TenantName}_DB"
$query = "SELECT name FROM sys.databases WHERE name = '$dbName'"

try {
    $result = sqlcmd -S "localhost,1433" -U "sa" -P $SqlServerPassword -Q $query -h -1 2>$null
    if ($result -match $dbName) {
        Write-Host "SUCCESS: Database created: $dbName" -ForegroundColor Green
    } else {
        Write-Host "WARNING: Database not found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "WARNING: Cannot connect to SQL Server" -ForegroundColor Yellow
}

Write-Host "=== Test Completed ===" -ForegroundColor Cyan
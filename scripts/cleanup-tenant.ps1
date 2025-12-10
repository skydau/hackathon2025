# Cleanup Tenant Script
# This script cleans up a specific tenant and its resources

param(
    [string]$TenantName = "test-hospital"
)

Write-Host "=== Cleaning up tenant: $TenantName ===" -ForegroundColor Yellow

# Delete Tenant CRD
Write-Host "Deleting Tenant CRD..." -ForegroundColor Cyan
kubectl delete tenant $TenantName --ignore-not-found=true
Write-Host "ℹ Tenant CRD deletion command executed" -ForegroundColor Cyan

# Delete tenant namespace
Write-Host "Deleting tenant namespace..." -ForegroundColor Cyan
kubectl delete namespace "tenant-$TenantName" --ignore-not-found=true
Write-Host "ℹ Namespace deletion command executed" -ForegroundColor Cyan

# Wait for resource cleanup
Write-Host "Waiting for resource cleanup..." -ForegroundColor Cyan
Start-Sleep -Seconds 5

# Check final status
Write-Host "Checking cleanup results..." -ForegroundColor Cyan
$tenant = kubectl get tenant $TenantName --ignore-not-found=true 2>$null
$namespace = kubectl get namespace "tenant-$TenantName" --ignore-not-found=true 2>$null

if (-not $tenant) {
    Write-Host "✓ Tenant CRD removed: $TenantName" -ForegroundColor Green
} else {
    Write-Host "ℹ Tenant CRD still exists (may be in deletion process)" -ForegroundColor Yellow
}

if (-not $namespace) {
    Write-Host "✓ Namespace removed: tenant-$TenantName" -ForegroundColor Green
} else {
    Write-Host "ℹ Namespace still exists (may be in deletion process)" -ForegroundColor Yellow
}

Write-Host "✓ Cleanup completed for tenant: $TenantName" -ForegroundColor Green
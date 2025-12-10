
# Local image build script (PowerShell)
# Builds all platform Docker images and uses minikube's Docker daemon.

param(
    [switch]$SkipTests = $false,
    [string]$Profile = "minikube"  # change to "mk-docker" if you created a docker profile
)

$ErrorActionPreference = "Stop"

function Fail($msg) {
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit 1
}

function Check-Cmd($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        Fail "Command not found: $name. Please install and ensure it is in PATH."
    }
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Multi-Tenant Medical Platform - Local Image Build" -ForegroundColor Cyan
Write-Host "============================================================`n" -ForegroundColor Cyan

# Check required commands
Check-Cmd "minikube"
Check-Cmd "kubectl"
Check-Cmd "docker"

# Ensure minikube is running for the given profile
Write-Host "Checking minikube status for profile '$Profile'..." -ForegroundColor Yellow
minikube -p $Profile status | Out-Null
if ($LASTEXITCODE -ne 0) {
    Fail "Minikube (profile=$Profile) is not running. Start it first: minikube start -p $Profile"
}

# Use minikube's Docker daemon
Write-Host "Configuring Docker to use minikube's Docker daemon..." -ForegroundColor Yellow
minikube -p $Profile docker-env --shell powershell | Invoke-Expression
if ($LASTEXITCODE -ne 0) {
    Fail "Failed to configure Docker environment from minikube. Please run: minikube -p $Profile docker-env"
}
Write-Host "OK: Docker is now targeting minikube ($Profile)" -ForegroundColor Green

# .NET service images (build CONTEXT = repo root '.')
$dotnetServices = @(
    @{Name="Tenant Catalog Service"; Path="src/TenantCatalogService"; Tag="tenant-catalog:latest"},
    @{Name="Device Registry Service"; Path="src/DeviceRegistryService"; Tag="device-registry:latest"},
    @{Name="MedLogic Service"; Path="src/MedLogicService"; Tag="medlogic-service:latest"},
    @{Name="Admin UI"; Path="src/AdminUI"; Tag="admin-ui:latest"}
)

foreach ($svc in $dotnetServices) {
    $name = $svc.Name
    $path = $svc.Path
    $tag  = $svc.Tag

    Write-Host "`nBuilding $name..." -ForegroundColor Yellow

    $dockerfile = Join-Path $path "Dockerfile"
    if (Test-Path $dockerfile) {
        # IMPORTANT: use repo root '.' as build context, because Dockerfile copies src/...
        docker build -t $tag -f $dockerfile .
        if ($LASTEXITCODE -eq 0) {
            Write-Host "OK: $name built successfully -> $tag" -ForegroundColor Green
        } else {
            Fail "$name build failed (exit code $LASTEXITCODE)"
        }
    } else {
        Write-Host "WARN: Dockerfile not found: $dockerfile" -ForegroundColor Yellow
    }
}

# Smart Gateway image (nginx) - context can be 'nginx' safely
Write-Host "`nBuilding Smart Gateway (nginx)..." -ForegroundColor Yellow
$nginxDockerfile = "nginx/Dockerfile"
if (Test-Path $nginxDockerfile) {
    docker build -t smart-gateway:latest -f $nginxDockerfile "nginx"
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: Smart Gateway built successfully" -ForegroundColor Green
    } else {
        Fail "Smart Gateway build failed (exit code $LASTEXITCODE)"
    }
} else {
    Write-Host "WARN: Dockerfile not found: $nginxDockerfile" -ForegroundColor Yellow
}

# Tenant Operator image (Go) - context = tenant-operator
Write-Host "`nBuilding Tenant Operator (Go)..." -ForegroundColor Yellow
$operatorDir = "tenant-operator"
$operatorDockerfile = Join-Path $operatorDir "Dockerfile"
if (Test-Path $operatorDockerfile) {
    Push-Location $operatorDir

    # Check Go toolchain
    if (-not (Get-Command "go" -ErrorAction SilentlyContinue)) {
        Pop-Location
        Fail "Go toolchain not found. Please install Go and ensure 'go' is in PATH."
    }

    # Build static linux/amd64 binary
    Write-Host "  Compiling Go code..." -ForegroundColor Gray
    $env:CGO_ENABLED = "0"
    $env:GOOS = "linux"
    $env:GOARCH = "amd64"

    # Optional: remove previous binary to avoid stale artifacts
    if (Test-Path ".\manager") { Remove-Item ".\manager" -Force -ErrorAction SilentlyContinue }

    go build -o manager main.go
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Fail "Go build failed (exit code $LASTEXITCODE)"
    }

    # Build Docker image
    docker build -t tenant-operator:latest .
    if ($LASTEXITCODE -eq 0) {
        Write-Host "OK: Tenant Operator image built successfully" -ForegroundColor Green
    } else {
        Pop-Location
        Fail "Tenant Operator image build failed (exit code $LASTEXITCODE)"
    }

    Pop-Location
} else {
    Write-Host "WARN: Dockerfile not found: $operatorDockerfile" -ForegroundColor Yellow
}

# Show built images
Write-Host "`nBuilt images (filtered):" -ForegroundColor Cyan
docker images | Select-String -Pattern "tenant-catalog|device-registry|medlogic-service|admin-ui|smart-gateway|tenant-operator"

Write-Host "`nDone: All images built." -ForegroundColor Green
Write-Host "`nNext step: Deploy to minikube profile '$Profile'" -ForegroundColor Yellow
Write-Host "  .\scripts\deploy-local.ps1 -Profile $Profile" -ForegroundColor Gray

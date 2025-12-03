# Technology Stack

## Backend Services (.NET)

- **.NET 9.0**: Primary application framework
- **ASP.NET Core**: Web API services
- **Entity Framework Core 9.0**: ORM for database access
- **SQL Server**: Primary database (supports in-memory for testing)
- **Serilog**: Structured logging framework
- **xUnit**: Testing framework

## Infrastructure & Orchestration

- **Kubernetes (AKS)**: Container orchestration
- **Docker**: Containerization
- **NGINX with OpenResty/Lua**: Smart gateway with custom routing logic
- **Kubernetes Operator (Go)**: Tenant lifecycle automation
- **OPA Gatekeeper**: Policy-as-code enforcement

## Security & Identity

- **Azure Workload Identity**: Passwordless authentication for pods
- **Azure Key Vault**: Secret management with CSI driver
- **SQL Server TDE**: Transparent data encryption
- **Always Encrypted**: Client-side encryption for sensitive data

## Common Commands

### Build & Test

```bash
# Build entire solution
dotnet build

# Run all tests
dotnet test

# Run specific test project
dotnet test tests/DeviceRegistryService.Tests

# Run tests with filter
dotnet test --filter "FullyQualifiedName~DbContextTests"
```

### Run Services Locally

```bash
# Tenant Catalog Service (default port: 5000)
dotnet run --project src/TenantCatalogService

# Device Registry Service (default port: 5001)
dotnet run --project src/DeviceRegistryService
```

### Docker

```bash
# Build service images
docker build -f src/TenantCatalogService/Dockerfile -t medlogic/tenant-catalog-service:latest .
docker build -f src/DeviceRegistryService/Dockerfile -t medlogic/device-registry-service:latest .
```

### Kubernetes

```bash
# Apply manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/tenant-catalog-deployment.yaml
kubectl apply -f k8s/device-registry-deployment.yaml

# Check health
kubectl get pods -n medlogic
kubectl logs -n medlogic <pod-name>
```

### Go (Tenant Operator)

```bash
# Build operator
cd tenant-operator
go build -o bin/manager main.go

# Run tests
go test ./...

# Install CRDs
kubectl apply -f config/crd/
```

# Project Structure

## Root Layout

```
MedLogicPlatform/
├── src/                    # .NET source code
├── tests/                  # .NET test projects
├── k8s/                    # Kubernetes manifests
├── nginx/                  # NGINX gateway configuration
├── tenant-operator/        # Go-based Kubernetes operator
└── .kiro/                  # Kiro configuration and specs
```

## Source Code (`src/`)

### Microservices

- **TenantCatalogService/**: REST API for tenant management
  - `Controllers/`: API endpoints
  - `Models/`: Domain entities (Tenant, DatabaseConfig, SLOConfig, ThrottlingConfig)
  - `DTOs/`: Data transfer objects
  - `Data/`: EF Core DbContext
  - `Repositories/`: Data access layer

- **DeviceRegistryService/**: REST API for device-to-tenant mapping
  - `Controllers/`: API endpoints (DevicesController)
  - `Models/`: Domain entities (Device)
  - `DTOs/`: Request/response objects
  - `Data/`: EF Core DbContext
  - `Repositories/`: Data access layer

- **SharedLibrary/**: Common utilities
  - `WorkloadIdentity/`: Azure credential providers and service extensions
  - `Encryption/`: Database encryption helpers (TDE, Always Encrypted)

### Service Patterns

All services follow consistent patterns:
- Repository pattern for data access
- Dependency injection via `Program.cs`
- Health check endpoints at `/health` and `/health/ready`
- Serilog for structured logging
- In-memory database fallback for development

## Tests (`tests/`)

Each service has a corresponding test project:
- **DeviceRegistryService.Tests/**: Unit and integration tests
- **TenantCatalogService.Tests/**: Unit and integration tests
- **Encryption.Tests/**: Property-based tests for encryption features
- **Gatekeeper.Tests/**: Policy validation tests
- **WorkloadIdentity.Tests/**: Integration tests for Azure identity

Test projects use:
- xUnit as test framework
- WebApplicationFactory for integration testing
- Property-based testing for security features

## Infrastructure

### Kubernetes (`k8s/`)

- `namespace.yaml`: Namespace definitions
- `*-deployment.yaml`: Service deployments with health checks
- `gatekeeper-*.yaml`: OPA policy definitions
- `workload-identity-*.yaml`: Azure identity configuration
- `GATEKEEPER_SETUP.md`, `WORKLOAD_IDENTITY_GUIDE.md`: Setup documentation

### NGINX Gateway (`nginx/`)

- `nginx.conf`: Main configuration with upstream definitions
- `lua/tenant_router.lua`: Device-to-tenant routing logic
- `lua/rate_limiter.lua`: Per-tenant rate limiting
- `Dockerfile`: Gateway container image

### Tenant Operator (`tenant-operator/`)

Go-based Kubernetes operator following kubebuilder patterns:
- `api/v1/`: CRD definitions (Tenant resource)
- `controllers/`: Reconciliation logic
- `config/`: CRD manifests, RBAC, samples
- `pkg/keyvault/`: Azure Key Vault integration

## Naming Conventions

- **C# Projects**: PascalCase (e.g., `TenantCatalogService`)
- **Files**: PascalCase for C# (e.g., `TenantsController.cs`)
- **Kubernetes Resources**: kebab-case (e.g., `tenant-catalog-deployment.yaml`)
- **Go Packages**: lowercase (e.g., `keyvault`)
- **API Endpoints**: lowercase with hyphens (e.g., `/api/tenants`)

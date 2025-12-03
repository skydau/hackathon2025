# Tenant Operator

Kubernetes Operator for managing multi-tenant medical platform resources.

## Overview

The Tenant Operator automates the provisioning and management of tenant resources in a Kubernetes cluster. When a Tenant CRD is created, the operator automatically:

1. Creates a dedicated namespace for the tenant
2. Provisions a ConfigMap with tenant configuration
3. Applies ResourceQuota to limit resource usage
4. Configures NetworkPolicy for network isolation
5. Sets up RBAC rules for access control

## Architecture

The operator follows the Kubernetes Operator pattern using controller-runtime. It watches for Tenant CRD resources and reconciles the desired state with the actual cluster state.

### Components

- **Tenant CRD**: Custom Resource Definition defining tenant specifications
- **TenantReconciler**: Controller that reconciles Tenant resources
- **API Types**: Go types representing the Tenant CRD schema

## Installation

### Prerequisites

- Kubernetes cluster (v1.28+)
- kubectl configured to access the cluster
- Go 1.21+ (for development)

### Deploy the Operator

1. Install the CRD:
```bash
kubectl apply -f config/crd/tenants.medlogic.io_tenants.yaml
```

2. Create the operator namespace:
```bash
kubectl create namespace tenant-operator-system
```

3. Apply RBAC:
```bash
kubectl apply -f config/rbac/role.yaml
```

4. Deploy the operator:
```bash
kubectl apply -f config/manager/deployment.yaml
```

## Usage

### Creating a Tenant

Create a Tenant resource:

```yaml
apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: hospital-a
spec:
  displayName: "Hospital A"
  db:
    mode: perDatabase
    server: sqlserver.default.svc.cluster.local
    database: HospitalA_DB
  throttling:
    rps: 100
  slo:
    availability: "99.9%"
    p95_latency_ms: 1000
```

Apply it:
```bash
kubectl apply -f tenant.yaml
```

### Checking Tenant Status

```bash
kubectl get tenant hospital-a -o yaml
```

The status will show:
- `phase`: Current provisioning phase (Provisioning, Ready, Failed)
- `namespaceCreated`: Whether the namespace was created
- `resourcesProvisioned`: Whether all resources were provisioned

### Listing All Tenants

```bash
kubectl get tenants
```

## Development

### Building

```bash
go build -o bin/manager main.go
```

### Running Tests

Run all tests including property-based tests:
```bash
go test ./... -v
```

Run only property tests:
```bash
go test ./controllers -v -run TestProperty
```

### Running Locally

```bash
go run main.go
```

### Building Docker Image

```bash
docker build -t tenant-operator:latest .
```

## Testing

The operator includes comprehensive property-based tests using gopter:

- **Property 6**: CRD creation triggers namespace creation
- **Property 7**: Namespace contains configuration ConfigMap
- **Property 8**: Namespace has ResourceQuota applied
- **Property 9**: Namespace has NetworkPolicy applied
- **Property 10**: Namespace has RBAC configured

Each property test runs 100 iterations with randomly generated tenant configurations to ensure correctness across all valid inputs.

## Resources Created

For each Tenant, the operator creates:

### Namespace
- Name: `tenant-{tenant-name}`
- Labels: `tenant`, `tenantId`, `managedBy`

### ConfigMap
- Name: `tenant-config`
- Contains: tenant configuration (ID, display name, DB config, throttling)

### ResourceQuota
- Name: `tenant-quota`
- Limits: CPU (4 requests, 8 limits), Memory (8Gi requests, 16Gi limits), Pods (50)

### NetworkPolicy
- Name: `tenant-isolation`
- Rules: 
  - Allow intra-namespace traffic
  - Allow egress to platform-system and kube-system namespaces
  - Deny all other cross-namespace traffic

### RoleBinding
- Name: `tenant-admin`
- Binds: `admin` ClusterRole to namespace default ServiceAccount

## Troubleshooting

### Operator not reconciling

Check operator logs:
```bash
kubectl logs -n tenant-operator-system deployment/tenant-operator
```

### Tenant stuck in Provisioning

Check tenant status:
```bash
kubectl describe tenant <tenant-name>
```

Check operator logs for errors.

### Resources not created

Verify operator has correct RBAC permissions:
```bash
kubectl auth can-i create namespaces --as=system:serviceaccount:tenant-operator-system:tenant-operator
```

## License

Copyright 2024 TouchPoint Medical

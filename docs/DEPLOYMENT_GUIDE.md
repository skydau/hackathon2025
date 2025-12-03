# MedLogic Platform Deployment Guide

This guide provides comprehensive instructions for deploying the MedLogic Multi-Tenant Medical Platform to Azure Kubernetes Service (AKS).

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Architecture Overview](#architecture-overview)
3. [Pre-Deployment Setup](#pre-deployment-setup)
4. [Deployment Methods](#deployment-methods)
5. [Post-Deployment Configuration](#post-deployment-configuration)
6. [Verification](#verification)
7. [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Tools

- **kubectl** (v1.28+): Kubernetes command-line tool
- **Helm** (v3.12+): Kubernetes package manager
- **Azure CLI** (v2.50+): Azure command-line interface
- **Docker** (v24.0+): Container runtime (for building images)
- **.NET SDK** (v9.0+): For building .NET services
- **Go** (v1.21+): For building the Tenant Operator

### Azure Resources

Before deployment, ensure you have:

1. **Azure Subscription** with appropriate permissions
2. **Azure Kubernetes Service (AKS)** cluster (v1.28+)
   - Workload Identity enabled
   - Azure CNI networking
   - Minimum 3 nodes (Standard_D4s_v3 or larger)
3. **Azure SQL Server** instance
4. **Azure Key Vault** for secrets management
5. **Azure Container Registry (ACR)** for container images
6. **Azure AD App Registration** for Workload Identity

### Required Permissions

- **AKS**: Contributor or higher
- **Azure SQL**: SQL Server Contributor
- **Key Vault**: Key Vault Administrator
- **ACR**: AcrPush and AcrPull
- **Azure AD**: Application Administrator (for Workload Identity setup)

## Architecture Overview

The platform consists of the following components:

```
┌─────────────────────────────────────────────────────────────┐
│                    External Devices                          │
│                  (medDispense Stations)                      │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                   Smart Gateway (NGINX)                      │
│              Namespace: gateway                              │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                Platform Services                             │
│              Namespace: platform-system                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Tenant       │  │ Device       │  │ MedLogic     │      │
│  │ Catalog      │  │ Registry     │  │ Service      │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                  Control Plane                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │ Tenant       │  │ OPA          │  │ Azure Key    │      │
│  │ Operator     │  │ Gatekeeper   │  │ Vault        │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Pre-Deployment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform
```

### 2. Configure Azure CLI

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "<your-subscription-id>"

# Set environment variables
export RESOURCE_GROUP="medlogic-platform-rg"
export LOCATION="eastus"
export AKS_CLUSTER="medlogic-aks"
export ACR_NAME="medlogicacr"
export KEY_VAULT_NAME="medlogic-kv"
export SQL_SERVER_NAME="medlogic-sql"
```

### 3. Create Azure Resources

#### Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

#### Create AKS Cluster with Workload Identity

```bash
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 3 \
  --node-vm-size Standard_D4s_v3 \
  --network-plugin azure \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --generate-ssh-keys

# Get credentials
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER
```

#### Create Azure Container Registry

```bash
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard

# Attach ACR to AKS
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --attach-acr $ACR_NAME
```

#### Create Azure SQL Server

```bash
az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER_NAME \
  --location $LOCATION \
  --admin-user sqladmin \
  --admin-password '<strong-password>'

# Create databases
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog \
  --service-objective S1

az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name DeviceRegistry \
  --service-objective S1

# Configure firewall to allow Azure services
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

#### Create Azure Key Vault

```bash
az keyvault create \
  --resource-group $RESOURCE_GROUP \
  --name $KEY_VAULT_NAME \
  --location $LOCATION \
  --enable-rbac-authorization true
```

### 4. Build and Push Container Images

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Build and push images
./scripts/build-and-push.sh $ACR_NAME.azurecr.io
```

### 5. Configure Workload Identity

Run the workload identity setup script:

```bash
cd k8s
./workload-identity-setup.sh
```

This script will:
- Create Azure AD app registrations
- Configure federated credentials
- Create Kubernetes service accounts
- Grant necessary permissions

## Deployment Methods

### Method 1: Helm Chart Deployment (Recommended)

#### 1. Create values file

Create a `values-production.yaml` file:

```yaml
# values-production.yaml
global:
  imageRegistry: medlogicacr.azurecr.io

tenantCatalog:
  image:
    repository: medlogicacr.azurecr.io/tenant-catalog-service
    tag: "1.0.0"
  database:
    server: medlogic-sql.database.windows.net
    name: TenantCatalog
  azure:
    workloadIdentity:
      clientId: "<tenant-catalog-client-id>"
      tenantId: "<azure-tenant-id>"

deviceRegistry:
  image:
    repository: medlogicacr.azurecr.io/device-registry-service
    tag: "1.0.0"
  database:
    server: medlogic-sql.database.windows.net
    name: DeviceRegistry
  azure:
    workloadIdentity:
      clientId: "<device-registry-client-id>"
      tenantId: "<azure-tenant-id>"

smartGateway:
  image:
    repository: medlogicacr.azurecr.io/smart-gateway
    tag: "1.0.0"
  service:
    type: LoadBalancer

tenantOperator:
  image:
    repository: medlogicacr.azurecr.io/tenant-operator
    tag: "1.0.0"
  azure:
    keyVault:
      name: medlogic-kv
      tenantId: "<azure-tenant-id>"
    workloadIdentity:
      clientId: "<operator-client-id>"

azure:
  subscriptionId: "<subscription-id>"
  resourceGroup: medlogic-platform-rg
  location: eastus
  keyVault:
    name: medlogic-kv
  sqlServer:
    name: medlogic-sql
```

#### 2. Install the Helm chart

```bash
# Install or upgrade
helm upgrade --install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --create-namespace \
  --wait \
  --timeout 10m
```

#### 3. Verify deployment

```bash
# Check all pods are running
kubectl get pods -n platform-system
kubectl get pods -n gateway

# Check services
kubectl get svc -n platform-system
kubectl get svc -n gateway
```

### Method 2: Manual Kubectl Deployment

If you prefer manual deployment:

```bash
# Deploy namespaces
kubectl apply -f k8s/namespace.yaml

# Deploy platform services
kubectl apply -f k8s/tenant-catalog-deployment.yaml
kubectl apply -f k8s/device-registry-deployment.yaml
kubectl apply -f k8s/medlogic-service-deployment.yaml

# Deploy gateway
kubectl apply -f k8s/smart-gateway-deployment.yaml

# Deploy tenant operator
kubectl apply -f tenant-operator/config/crd/
kubectl apply -f tenant-operator/config/manager/
kubectl apply -f tenant-operator/config/rbac/

# Deploy OPA Gatekeeper
kubectl apply -f k8s/gatekeeper-install.yaml
kubectl apply -f k8s/gatekeeper-constraint-template-tenant-label.yaml
kubectl apply -f k8s/gatekeeper-constraint-tenant-label.yaml
kubectl apply -f k8s/gatekeeper-constraint-template-no-cross-tenant-netpol.yaml
kubectl apply -f k8s/gatekeeper-constraint-no-cross-tenant-netpol.yaml
```

### Method 3: Automated Deployment Script

Use the automated deployment script:

```bash
./scripts/deploy.sh --environment production --config values-production.yaml
```

## Post-Deployment Configuration

### 1. Configure Database Schemas

Run database migrations:

```bash
# Tenant Catalog
kubectl exec -n platform-system deployment/tenant-catalog-service -- \
  dotnet ef database update

# Device Registry
kubectl exec -n platform-system deployment/device-registry-service -- \
  dotnet ef database update
```

### 2. Create Initial Tenant

```bash
# Get the gateway external IP
GATEWAY_IP=$(kubectl get svc -n gateway smart-gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Create a tenant via API
curl -X POST http://$GATEWAY_IP/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Hospital A",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "medlogic-sql.database.windows.net",
      "database": "HospitalA_DB"
    },
    "throttling": {
      "rps": 100
    },
    "slo": {
      "availability": "99.9%",
      "p95LatencyMs": 1000
    }
  }'
```

### 3. Register Devices

```bash
# Register a device
curl -X POST http://$GATEWAY_IP/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-001",
    "tenantId": "<tenant-id-from-previous-step>",
    "deviceType": "medDispense"
  }'
```

### 4. Configure Monitoring

```bash
# Deploy Prometheus and Grafana (if using observability stack)
kubectl apply -f k8s/prometheus-deployment.yaml
kubectl apply -f k8s/grafana-deployment.yaml

# Apply SLO monitoring rules
kubectl apply -f k8s/prometheus-slo-rules.yaml
```

## Verification

### Health Checks

```bash
# Check all pods are healthy
kubectl get pods --all-namespaces | grep -v Running

# Check service endpoints
kubectl get endpoints -n platform-system

# Test health endpoints
kubectl run curl --image=curlimages/curl -i --rm --restart=Never -- \
  curl http://tenant-catalog.platform-system.svc.cluster.local:8080/health

kubectl run curl --image=curlimages/curl -i --rm --restart=Never -- \
  curl http://device-registry.platform-system.svc.cluster.local:8080/health
```

### End-to-End Test

```bash
# Run the end-to-end test script
./scripts/e2e-test.sh
```

### Verify Tenant Isolation

```bash
# Create test tenants
kubectl apply -f tenant-operator/config/samples/tenant_v1_tenant.yaml

# Verify namespace creation
kubectl get namespaces | grep tenant-

# Verify network policies
kubectl get networkpolicies -n tenant-hospital-a

# Verify resource quotas
kubectl get resourcequotas -n tenant-hospital-a
```

## Troubleshooting

See [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) for detailed troubleshooting guidance.

### Quick Diagnostics

```bash
# Check pod logs
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# Check events
kubectl get events -n platform-system --sort-by='.lastTimestamp'

# Check workload identity
kubectl describe pod -n platform-system <pod-name> | grep -A 5 "azure.workload.identity"

# Check database connectivity
kubectl exec -n platform-system deployment/tenant-catalog-service -- \
  /bin/sh -c 'echo "SELECT 1" | sqlcmd -S $DATABASE_SERVER -d $DATABASE_NAME'
```

## Rollback

### Helm Rollback

```bash
# List releases
helm list -n platform-system

# Rollback to previous version
helm rollback medlogic-platform -n platform-system

# Rollback to specific revision
helm rollback medlogic-platform 2 -n platform-system
```

### Manual Rollback

```bash
# Rollback deployments
kubectl rollout undo deployment/tenant-catalog-service -n platform-system
kubectl rollout undo deployment/device-registry-service -n platform-system
kubectl rollout undo deployment/smart-gateway -n gateway
```

## Maintenance

### Updating Services

```bash
# Update image version
kubectl set image deployment/tenant-catalog-service \
  tenant-catalog-service=medlogicacr.azurecr.io/tenant-catalog-service:1.1.0 \
  -n platform-system

# Check rollout status
kubectl rollout status deployment/tenant-catalog-service -n platform-system
```

### Scaling

```bash
# Scale deployment
kubectl scale deployment/tenant-catalog-service --replicas=5 -n platform-system

# Enable autoscaling
kubectl autoscale deployment/tenant-catalog-service \
  --min=3 --max=10 --cpu-percent=70 \
  -n platform-system
```

## Security Considerations

1. **Secrets Management**: All secrets are stored in Azure Key Vault
2. **Network Policies**: Enforce strict network isolation between tenants
3. **RBAC**: Implement least-privilege access control
4. **Image Scanning**: Scan all container images for vulnerabilities
5. **Audit Logging**: Enable audit logging for all API calls

## Support

For issues or questions:
- Create an issue in the GitHub repository
- Contact the platform team at platform@medlogic.io
- Refer to the [Architecture Documentation](../README.md)

## Next Steps

- [Configure Observability](./OBSERVABILITY_SETUP.md)
- [Set up CI/CD Pipeline](./CICD_SETUP.md)
- [Tenant Onboarding Guide](./TENANT_ONBOARDING.md)
- [Disaster Recovery Plan](./DISASTER_RECOVERY.md)

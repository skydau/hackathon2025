# MedLogic Platform Quick Start Guide

Get the MedLogic Multi-Tenant Medical Platform up and running in minutes.

## Prerequisites

- Azure subscription
- kubectl, helm, and Azure CLI installed
- Docker installed (for building images)

## Quick Deployment (Development)

### 1. Clone and Setup

```bash
git clone https://github.com/your-org/medlogic-platform.git
cd medlogic-platform
```

### 2. Set Environment Variables

```bash
export RESOURCE_GROUP="medlogic-dev-rg"
export LOCATION="eastus"
export AKS_CLUSTER="medlogic-dev-aks"
export ACR_NAME="medlogicdevacr"
export KEY_VAULT_NAME="medlogic-dev-kv"
export SQL_SERVER_NAME="medlogic-dev-sql"
```

### 3. Create Azure Resources

```bash
# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create AKS cluster
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 3 \
  --enable-managed-identity \
  --enable-workload-identity \
  --enable-oidc-issuer \
  --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER

# Create ACR
az acr create --resource-group $RESOURCE_GROUP --name $ACR_NAME --sku Standard

# Attach ACR to AKS
az aks update --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER --attach-acr $ACR_NAME

# Create SQL Server
az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER_NAME \
  --admin-user sqladmin \
  --admin-password 'YourStrongPassword123!'

# Create databases
az sql db create --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --name TenantCatalog
az sql db create --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --name DeviceRegistry

# Create Key Vault
az keyvault create \
  --resource-group $RESOURCE_GROUP \
  --name $KEY_VAULT_NAME \
  --enable-rbac-authorization true
```

### 4. Build and Push Images

```bash
# Login to ACR
az acr login --name $ACR_NAME

# Build and push
chmod +x scripts/build-and-push.sh
./scripts/build-and-push.sh $ACR_NAME.azurecr.io
```

### 5. Configure Workload Identity

```bash
cd k8s
chmod +x workload-identity-setup.sh
./workload-identity-setup.sh
cd ..
```

### 6. Deploy with Helm

Create `values-dev.yaml`:

```yaml
global:
  imageRegistry: <your-acr>.azurecr.io

tenantCatalog:
  database:
    server: <your-sql-server>.database.windows.net
  azure:
    workloadIdentity:
      clientId: "<client-id>"
      tenantId: "<tenant-id>"

deviceRegistry:
  database:
    server: <your-sql-server>.database.windows.net
  azure:
    workloadIdentity:
      clientId: "<client-id>"
      tenantId: "<tenant-id>"
```

Deploy:

```bash
chmod +x scripts/deploy.sh
./scripts/deploy.sh --environment development --config values-dev.yaml
```

### 7. Verify Deployment

```bash
# Check pods
kubectl get pods -n platform-system
kubectl get pods -n gateway

# Get gateway IP
kubectl get svc smart-gateway -n gateway

# Run E2E tests
chmod +x scripts/e2e-test.sh
./scripts/e2e-test.sh
```

## Create Your First Tenant

```bash
# Get gateway IP
GATEWAY_IP=$(kubectl get svc smart-gateway -n gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Create tenant
curl -X POST http://$GATEWAY_IP/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "My Hospital",
    "dbConfig": {
      "mode": "perDatabase",
      "server": "medlogic-dev-sql.database.windows.net",
      "database": "MyHospital_DB"
    },
    "throttling": {
      "rps": 100
    }
  }'
```

## Register a Device

```bash
# Register device
curl -X POST http://$GATEWAY_IP/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE-001",
    "tenantId": "<tenant-id-from-above>",
    "deviceType": "medDispense"
  }'
```

## Access Admin UI

```bash
# Port forward to access locally
kubectl port-forward -n platform-system svc/admin-ui 8080:8080

# Open browser
open http://localhost:8080
```

## Troubleshooting

If something goes wrong:

```bash
# Collect diagnostics
chmod +x scripts/collect-diagnostics.sh
./scripts/collect-diagnostics.sh

# Check logs
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# Check events
kubectl get events -n platform-system --sort-by='.lastTimestamp'
```

See [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) for detailed guidance.

## Next Steps

- [Full Deployment Guide](./DEPLOYMENT_GUIDE.md)
- [Architecture Overview](../README.md)
- [Tenant Onboarding](./TENANT_ONBOARDING.md)
- [Monitoring Setup](./OBSERVABILITY_SETUP.md)

## Clean Up

To remove all resources:

```bash
# Delete Helm release
helm uninstall medlogic-platform -n platform-system

# Delete namespaces
kubectl delete namespace platform-system gateway

# Delete Azure resources
az group delete --name $RESOURCE_GROUP --yes --no-wait
```

# Admin UI Quick Start Guide

This guide will help you get the MedLogic Admin UI running quickly for demonstration purposes.

## Quick Start (Local Development)

### Step 1: Start Backend Services

First, ensure the backend services are running:

```bash
# Terminal 1: Start Tenant Catalog Service
dotnet run --project src/TenantCatalogService

# Terminal 2: Start Device Registry Service  
dotnet run --project src/DeviceRegistryService
```

### Step 2: Start Admin UI

```bash
# Terminal 3: Start Admin UI
dotnet run --project src/AdminUI
```

The Admin UI will be available at `http://localhost:5002` (or the port shown in console).

### Step 3: Create Your First Tenant

1. Open your browser to `http://localhost:5002`
2. Navigate to "Tenants" in the menu
3. Click "Create New Tenant"
4. Fill in the form:
   - **Display Name**: Hospital A
   - **Database Mode**: Database per Tenant
   - **Database Server**: localhost
   - **Database Name**: HospitalA_DB
   - **Rate Limit**: 100 (RPS)
   - **SLO Availability**: 99.9%
   - **SLO P95 Latency**: 1000 (ms)
5. Click "Create Tenant"

### Step 4: Register a Device

1. Navigate to "Devices" in the menu
2. Click "Register New Device"
3. Fill in the form:
   - **Serial Number**: MED-001
   - **Tenant**: Select "Hospital A" from dropdown
   - **Device Type**: medDispense Station
4. Click "Register Device"

### Step 5: View Dashboard

1. Navigate to "Dashboard" (home page)
2. See the overview of your platform:
   - Total tenants
   - Registered devices
   - Recent activity

## Docker Deployment

### Build the Image

```bash
docker build -f src/AdminUI/Dockerfile -t medlogic/admin-ui:latest .
```

### Run with Docker Compose

Create a `docker-compose.yml`:

```yaml
version: '3.8'
services:
  admin-ui:
    image: medlogic/admin-ui:latest
    ports:
      - "8080:8080"
    environment:
      - Services__TenantCatalog=http://tenant-catalog:8080
      - Services__DeviceRegistry=http://device-registry:8080
    depends_on:
      - tenant-catalog
      - device-registry
```

Then run:

```bash
docker-compose up
```

## Kubernetes Deployment

### Deploy to Kubernetes

```bash
# Apply the deployment
kubectl apply -f k8s/admin-ui-deployment.yaml

# Check the status
kubectl get pods -n medlogic -l app=admin-ui

# Get the service URL
kubectl get svc admin-ui-service -n medlogic
```

### Access the UI

If using LoadBalancer:
```bash
# Get the external IP
kubectl get svc admin-ui-service -n medlogic
```

If using Ingress:
```bash
# Add to /etc/hosts (or C:\Windows\System32\drivers\etc\hosts on Windows)
<ingress-ip> admin.medlogic.local

# Access at http://admin.medlogic.local
```

## Configuration

### Service URLs

Update `appsettings.json` or set environment variables:

```json
{
  "Services": {
    "TenantCatalog": "http://tenant-catalog-service:8080",
    "DeviceRegistry": "http://device-registry-service:8080"
  }
}
```

Or via environment variables:
```bash
export Services__TenantCatalog=http://localhost:5000
export Services__DeviceRegistry=http://localhost:5001
```

### Grafana Integration

To enable monitoring dashboards:

1. Ensure Grafana is running (default: http://localhost:3000)
2. Navigate to "Monitoring" page
3. Enter your Grafana URL
4. Click "Load Dashboard"

## Troubleshooting

### Cannot Connect to Backend Services

**Problem**: UI shows "No tenants found" or errors when creating tenants.

**Solution**: 
- Verify backend services are running
- Check service URLs in `appsettings.json`
- Check network connectivity

```bash
# Test Tenant Catalog
curl http://localhost:5000/health

# Test Device Registry
curl http://localhost:5001/health
```

### Port Already in Use

**Problem**: Cannot start Admin UI due to port conflict.

**Solution**: Change the port in `Properties/launchSettings.json` or use:

```bash
dotnet run --project src/AdminUI --urls "http://localhost:5003"
```

### Grafana Dashboard Not Loading

**Problem**: Monitoring page shows empty iframe.

**Solution**:
- Verify Grafana is running
- Check Grafana URL is correct
- Ensure Grafana allows iframe embedding (check `allow_embedding` setting)

## Next Steps

- Add authentication to secure the UI
- Create custom Grafana dashboards
- Implement tenant status updates
- Add device management features (update, delete)
- Integrate with Azure Key Vault for secrets
- Add audit logging

## Demo Scenario

For a complete demo:

1. **Create 3 tenants**: Hospital A, Hospital B, Hospital C
2. **Register devices**: 2-3 devices per tenant
3. **View dashboard**: Show tenant and device counts
4. **Show monitoring**: Embed Grafana dashboards
5. **Demonstrate isolation**: Show each tenant has separate database config

This demonstrates the core multi-tenant capabilities of the MedLogic platform.

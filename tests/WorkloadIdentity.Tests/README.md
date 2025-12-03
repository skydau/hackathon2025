# Workload Identity Integration Tests

## Overview

This test suite validates that AKS Workload Identity is properly configured and functioning for passwordless authentication to Azure services (Key Vault and SQL Server).

## Requirements Validated

- **Requirement 9.4**: WHEN 微服务需要访问数据库 THEN 微服务 SHALL 使用AKS Workload Identity进行无凭据身份验证
- **Property 44**: *对于任何*微服务访问数据库的操作，应该使用AKS Workload Identity进行无凭据身份验证

## Test Coverage

### 1. Configuration Tests
- `WorkloadIdentity_ShouldBeConfigured_WhenRunningInAKS`: Verifies environment variables are set
- `CredentialProvider_ShouldUseDefaultAzureCredential`: Validates credential provider initialization

### 2. Key Vault Access Tests
- `KeyVault_ShouldAccessSecrets_WithoutPassword`: Tests passwordless Key Vault access
- `KeyVault_SecretCache_ShouldWork`: Validates secret caching mechanism
- `KeyVault_ShouldHandleErrors_Gracefully`: Tests error handling

### 3. SQL Server Access Tests
- `SqlServer_ShouldConnect_WithoutPassword`: Tests passwordless SQL connection
- `SqlServer_ConnectionString_ShouldUseAzureADAuthentication`: Validates connection string format
- `SqlServer_ShouldExecuteQuery_WithWorkloadIdentity`: Tests query execution
- `SqlServer_ShouldHandleConnectionFailure_Gracefully`: Tests error handling

### 4. Multi-Service Tests
- `MultipleServices_ShouldAccessDifferentResources_WithSeparateIdentities`: Validates service isolation

## Running Tests

### Prerequisites

1. **AKS Cluster** with Workload Identity enabled
2. **Azure AD App Registrations** configured with federated credentials
3. **ServiceAccounts** created in Kubernetes with proper annotations
4. **RBAC roles** assigned to service principals

### Local Development

Tests will be skipped if Workload Identity environment variables are not set:

```bash
dotnet test tests/WorkloadIdentity.Tests/
```

Output:
```
Workload Identity not configured - tests will be skipped
To run these tests, ensure AZURE_CLIENT_ID, AZURE_TENANT_ID, and AZURE_FEDERATED_TOKEN_FILE are set
```

### In AKS Cluster

Deploy a test pod with the test suite:

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: workload-identity-test
  namespace: platform-system
  labels:
    azure.workload.identity/use: "true"
spec:
  serviceAccountName: tenant-catalog-sa
  containers:
  - name: test-runner
    image: mcr.microsoft.com/dotnet/sdk:9.0
    command: ["/bin/bash", "-c"]
    args:
      - |
        cd /tests
        dotnet test --logger "console;verbosity=detailed"
    volumeMounts:
    - name: test-source
      mountPath: /tests
    env:
    - name: KEY_VAULT_URL
      value: "https://medlogic-kv.vault.azure.net/"
    - name: SQL_SERVER
      value: "medlogic-sql.database.windows.net"
    - name: TEST_DATABASE
      value: "TenantCatalog"
  volumes:
  - name: test-source
    configMap:
      name: test-source
```

Run tests:

```bash
kubectl exec -it workload-identity-test -n platform-system -- dotnet test
```

### Environment Variables

Configure these environment variables for testing:

| Variable | Description | Example |
|----------|-------------|---------|
| `AZURE_CLIENT_ID` | Azure AD application client ID | Auto-injected by Workload Identity |
| `AZURE_TENANT_ID` | Azure AD tenant ID | Auto-injected by Workload Identity |
| `AZURE_FEDERATED_TOKEN_FILE` | Path to service account token | Auto-injected by Workload Identity |
| `KEY_VAULT_URL` | Azure Key Vault URL | `https://medlogic-kv.vault.azure.net/` |
| `SQL_SERVER` | SQL Server hostname | `medlogic-sql.database.windows.net` |
| `TEST_DATABASE` | Database name for testing | `TenantCatalog` |

## Test Scenarios

### Scenario 1: Tenant Catalog Service

```bash
# Deploy with tenant-catalog-sa ServiceAccount
kubectl apply -f k8s/tenant-catalog-deployment.yaml

# Run tests
kubectl exec -it <tenant-catalog-pod> -- dotnet test /app/tests/WorkloadIdentity.Tests.dll
```

Expected: All tests pass, service can access Key Vault and TenantCatalog database

### Scenario 2: Device Registry Service

```bash
# Deploy with device-registry-sa ServiceAccount
kubectl apply -f k8s/device-registry-deployment.yaml

# Run tests
kubectl exec -it <device-registry-pod> -- dotnet test /app/tests/WorkloadIdentity.Tests.dll
```

Expected: All tests pass, service can access Key Vault and DeviceRegistry database

### Scenario 3: Backend Microservice

```bash
# Deploy with medlogic-service-sa ServiceAccount
kubectl apply -f k8s/medlogic-service-deployment.yaml

# Run tests
kubectl exec -it <medlogic-pod> -- dotnet test /app/tests/WorkloadIdentity.Tests.dll
```

Expected: All tests pass, service can access Key Vault and tenant databases

## Troubleshooting

### Tests are Skipped

**Symptom**: All tests show as skipped

**Cause**: Workload Identity environment variables not set

**Solution**:
1. Verify pod has correct labels: `azure.workload.identity/use: "true"`
2. Verify pod uses correct ServiceAccount
3. Check ServiceAccount annotations:
   ```bash
   kubectl get sa -n platform-system tenant-catalog-sa -o yaml
   ```

### Key Vault Access Denied

**Symptom**: `KeyVault_ShouldAccessSecrets_WithoutPassword` fails with 403 Forbidden

**Cause**: Service principal doesn't have Key Vault permissions

**Solution**:
```bash
# Grant Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee <CLIENT_ID> \
  --scope /subscriptions/<SUB_ID>/resourceGroups/<RG>/providers/Microsoft.KeyVault/vaults/<KV_NAME>
```

### SQL Server Connection Failed

**Symptom**: `SqlServer_ShouldConnect_WithoutPassword` fails

**Cause**: Service principal doesn't have SQL Server permissions or Azure AD authentication not enabled

**Solution**:
1. Enable Azure AD authentication on SQL Server
2. Grant SQL DB Contributor role:
   ```bash
   az role assignment create \
     --role "SQL DB Contributor" \
     --assignee <CLIENT_ID> \
     --scope /subscriptions/<SUB_ID>/resourceGroups/<RG>/providers/Microsoft.Sql/servers/<SQL_SERVER>
   ```
3. Create SQL user for the managed identity:
   ```sql
   CREATE USER [tenant-catalog-workload-id] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [tenant-catalog-workload-id];
   ALTER ROLE db_datawriter ADD MEMBER [tenant-catalog-workload-id];
   ```

### Authentication Token Issues

**Symptom**: Tests fail with "Unable to get token" errors

**Cause**: Federated credential not properly configured

**Solution**:
1. Verify OIDC issuer URL:
   ```bash
   az aks show --resource-group <RG> --name <AKS> --query "oidcIssuerProfile.issuerUrl"
   ```
2. Verify federated credential:
   ```bash
   az ad app federated-credential list --id <APP_ID>
   ```
3. Ensure subject matches: `system:serviceaccount:platform-system:tenant-catalog-sa`

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Workload Identity Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - name: Get AKS Credentials
        run: |
          az aks get-credentials \
            --resource-group medlogic-rg \
            --name medlogic-aks
      
      - name: Run Workload Identity Tests
        run: |
          kubectl run workload-identity-test \
            --image=mcr.microsoft.com/dotnet/sdk:9.0 \
            --restart=Never \
            --labels="azure.workload.identity/use=true" \
            --serviceaccount=tenant-catalog-sa \
            --namespace=platform-system \
            --command -- /bin/bash -c "dotnet test /tests/WorkloadIdentity.Tests.dll"
          
          kubectl wait --for=condition=complete --timeout=300s pod/workload-identity-test -n platform-system
          kubectl logs workload-identity-test -n platform-system
```

## References

- [AKS Workload Identity Documentation](https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview)
- [Azure Identity SDK for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [SQL Server Azure AD Authentication](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-overview)

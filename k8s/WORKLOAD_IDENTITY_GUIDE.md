# AKS Workload Identity Configuration Guide

## Overview

This guide explains how to configure Azure Kubernetes Service (AKS) Workload Identity for the Multi-Tenant Medical Platform. Workload Identity enables passwordless authentication from Kubernetes pods to Azure services like Key Vault and SQL Server.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Kubernetes Pod                            │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Application Container                                  │ │
│  │  - Uses Azure SDK                                       │ │
│  │  - DefaultAzureCredential                               │ │
│  └────────────────────────────────────────────────────────┘ │
│                           │                                  │
│                           ▼                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  Service Account Token (Projected Volume)              │ │
│  │  - JWT token signed by AKS OIDC issuer                 │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              Azure AD Token Exchange                         │
│  - Validates JWT token from AKS                             │
│  - Issues Azure AD access token                             │
└─────────────────────────────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────┐
│              Azure Services                                  │
│  ┌──────────────┐  ┌──────────────┐                        │
│  │ Key Vault    │  │ SQL Server   │                        │
│  │ (Secrets)    │  │ (Database)   │                        │
│  └──────────────┘  └──────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

1. **AKS Cluster with OIDC Issuer enabled**:
   ```bash
   az aks update \
     --resource-group medlogic-rg \
     --name medlogic-aks \
     --enable-oidc-issuer \
     --enable-workload-identity
   ```

2. **Azure CLI** with appropriate permissions to:
   - Create Azure AD App Registrations
   - Create Service Principals
   - Assign RBAC roles
   - Manage Key Vault and SQL Server

3. **kubectl** configured to access the AKS cluster

## Setup Steps

### 1. Run the Setup Script

The `workload-identity-setup.sh` script automates the entire configuration:

```bash
# Set environment variables
export RESOURCE_GROUP="medlogic-rg"
export AKS_CLUSTER_NAME="medlogic-aks"
export KEY_VAULT_NAME="medlogic-kv"
export SQL_SERVER_NAME="medlogic-sql"

# Run the setup script
chmod +x k8s/workload-identity-setup.sh
./k8s/workload-identity-setup.sh
```

This script will:
1. Get the AKS OIDC Issuer URL
2. Create Azure AD App Registrations for each service
3. Create Service Principals
4. Configure Federated Identity Credentials
5. Grant Key Vault Secrets User role
6. Grant SQL DB Contributor role
7. Generate environment configuration file

### 2. Apply Kubernetes Configuration

After running the setup script, apply the Kubernetes resources:

```bash
# Load environment variables
source workload-identity-env.sh

# Apply ServiceAccounts with Workload Identity annotations
envsubst < k8s/workload-identity-setup.yaml | kubectl apply -f -

# Apply updated deployments
kubectl apply -f k8s/tenant-catalog-deployment.yaml
kubectl apply -f k8s/device-registry-deployment.yaml
```

### 3. Verify Configuration

Check that the ServiceAccounts are created with correct annotations:

```bash
kubectl get serviceaccount -n platform-system tenant-catalog-sa -o yaml
kubectl get serviceaccount -n platform-system device-registry-sa -o yaml
```

Verify pods are running with the correct service account:

```bash
kubectl get pods -n platform-system -l app=tenant-catalog-service -o yaml | grep serviceAccountName
```

## Application Code Integration

### .NET Applications

To use Workload Identity in .NET applications, use the `Azure.Identity` library:

```csharp
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Data.SqlClient;

// For Key Vault access
var credential = new DefaultAzureCredential();
var keyVaultUrl = "https://medlogic-kv.vault.azure.net/";
var secretClient = new SecretClient(new Uri(keyVaultUrl), credential);

// Retrieve secret
KeyVaultSecret secret = await secretClient.GetSecretAsync("database-password");
string password = secret.Value;

// For SQL Server access with Azure AD authentication
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = "medlogic-sql.database.windows.net",
    InitialCatalog = "TenantCatalog",
    Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
    Encrypt = true
};

using var connection = new SqlConnection(connectionString.ToString());
await connection.OpenAsync();
```

### Required NuGet Packages

Add these packages to your .NET projects:

```xml
<PackageReference Include="Azure.Identity" Version="1.10.0" />
<PackageReference Include="Azure.Security.KeyVault.Secrets" Version="4.5.0" />
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.0" />
```

### Environment Variables

The following environment variables are automatically injected by Workload Identity:

- `AZURE_CLIENT_ID`: The Azure AD application (client) ID
- `AZURE_TENANT_ID`: The Azure AD tenant ID
- `AZURE_FEDERATED_TOKEN_FILE`: Path to the projected service account token

`DefaultAzureCredential` automatically detects these variables and uses Workload Identity.

## Security Considerations

### Principle of Least Privilege

Each service has its own Azure AD identity with minimal required permissions:

- **Tenant Catalog Service**:
  - Key Vault: Read secrets for database credentials
  - SQL Server: Connect to Tenant Catalog database

- **Device Registry Service**:
  - Key Vault: Read secrets for database credentials
  - SQL Server: Connect to Device Registry database

- **Backend Microservices**:
  - Key Vault: Read tenant-specific secrets
  - SQL Server: Connect to tenant databases

### RBAC Roles

The setup script assigns these Azure RBAC roles:

1. **Key Vault Secrets User**: Read-only access to Key Vault secrets
2. **SQL DB Contributor**: Ability to connect and manage SQL databases

### Token Lifecycle

- Service account tokens are automatically rotated by Kubernetes
- Azure AD access tokens are cached and refreshed by the Azure SDK
- No manual token management required

## Troubleshooting

### Pod Cannot Authenticate

1. **Check ServiceAccount annotations**:
   ```bash
   kubectl get sa -n platform-system tenant-catalog-sa -o yaml
   ```
   Verify `azure.workload.identity/client-id` and `azure.workload.identity/tenant-id` are set.

2. **Check Pod labels**:
   ```bash
   kubectl get pod -n platform-system <pod-name> -o yaml | grep azure.workload.identity
   ```
   Verify `azure.workload.identity/use: "true"` label is present.

3. **Check Federated Credential**:
   ```bash
   az ad app federated-credential list --id <APP_ID>
   ```
   Verify the subject matches: `system:serviceaccount:platform-system:tenant-catalog-sa`

### Key Vault Access Denied

1. **Verify RBAC role assignment**:
   ```bash
   az role assignment list --assignee <CLIENT_ID> --scope <KEY_VAULT_ID>
   ```

2. **Check Key Vault network settings**: Ensure the AKS cluster can reach Key Vault

3. **Verify Key Vault access policies**: If using access policies instead of RBAC, ensure the service principal has appropriate permissions

### SQL Server Connection Failed

1. **Verify Azure AD authentication is enabled** on SQL Server:
   ```bash
   az sql server ad-admin list --resource-group medlogic-rg --server medlogic-sql
   ```

2. **Check firewall rules**: Ensure AKS cluster IPs are allowed

3. **Verify SQL user exists**:
   ```sql
   SELECT name, type_desc FROM sys.database_principals WHERE type = 'E';
   ```

## Testing

### Manual Testing

Test Key Vault access from a pod:

```bash
# Exec into a pod
kubectl exec -it -n platform-system <pod-name> -- /bin/bash

# Test with Azure CLI (if installed in container)
az login --identity
az keyvault secret show --vault-name medlogic-kv --name test-secret
```

### Integration Tests

The integration test suite verifies Workload Identity functionality:

```bash
dotnet test tests/WorkloadIdentity.Tests/WorkloadIdentityIntegrationTests.cs
```

## Monitoring

### Metrics to Monitor

1. **Authentication failures**: Monitor Azure AD sign-in logs
2. **Key Vault access**: Monitor Key Vault diagnostic logs
3. **SQL connection errors**: Monitor application logs for authentication errors

### Azure Monitor Queries

```kusto
// Failed authentication attempts
SigninLogs
| where AppId in ("<TENANT_CATALOG_CLIENT_ID>", "<DEVICE_REGISTRY_CLIENT_ID>")
| where ResultType != 0
| project TimeGenerated, UserPrincipalName, AppDisplayName, ResultType, ResultDescription

// Key Vault access
AzureDiagnostics
| where ResourceProvider == "MICROSOFT.KEYVAULT"
| where OperationName == "SecretGet"
| project TimeGenerated, CallerIPAddress, identity_claim_appid_g, ResultSignature
```

## References

- [Azure Workload Identity Documentation](https://azure.github.io/azure-workload-identity/)
- [AKS Workload Identity Overview](https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview)
- [Azure Identity SDK for .NET](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/identity-readme)
- [SQL Server Azure AD Authentication](https://learn.microsoft.com/en-us/azure/azure-sql/database/authentication-aad-overview)

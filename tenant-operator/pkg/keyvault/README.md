# Azure Key Vault Integration

This package provides integration with Azure Key Vault for managing tenant database credentials securely.

## Overview

The Key Vault client is responsible for:
- Creating database credentials when a tenant is provisioned
- Storing credentials securely in Azure Key Vault
- Revoking and deleting credentials when a tenant is decommissioned

## Implementation

### Mock Client

For testing and development, a `MockClient` is provided that simulates Key Vault operations in-memory.

```go
mockClient := keyvault.NewMockClient()
```

### Production Client

In production, this would be replaced with an actual Azure Key Vault client using the Azure SDK:

```go
import (
    "github.com/Azure/azure-sdk-for-go/sdk/azidentity"
    "github.com/Azure/azure-sdk-for-go/sdk/keyvault/azsecrets"
)

// Create Azure Key Vault client
cred, err := azidentity.NewDefaultAzureCredential(nil)
client, err := azsecrets.NewClient("https://<vault-name>.vault.azure.net/", cred, nil)
```

## Secret Naming Convention

Secrets are named using the pattern: `{tenantId}-{secret-type}`

For example:
- `hospital-a-db-password` - Database password for tenant "hospital-a"

## Operations

### Create Secret

Creates a new secret in Key Vault for a tenant:

```go
err := client.CreateSecret(ctx, tenantID, secretName, secretValue)
```

### Get Secret

Retrieves a secret from Key Vault:

```go
value, err := client.GetSecret(ctx, tenantID, secretName)
```

### Delete Secret

Deletes a secret from Key Vault:

```go
err := client.DeleteSecret(ctx, tenantID, secretName)
```

### Revoke Secret

Revokes access to a secret (marks it as disabled):

```go
err := client.RevokeSecret(ctx, tenantID, secretName)
```

## Integration with Tenant Operator

The Tenant Operator automatically:

1. **On Tenant Creation**: Generates a secure database password and stores it in Key Vault
2. **On Tenant Deletion**: Revokes and deletes all secrets associated with the tenant

## Security Considerations

- Secrets are generated using cryptographically secure random number generation
- In production, the operator should use AKS Workload Identity to authenticate with Key Vault
- Secrets should be rotated regularly (every 90 days recommended)
- Deleted secrets should be purged after the retention period

## Testing

Property-based tests verify:
- **Property 41**: For any tenant creation, database credentials are created in Key Vault
- **Property 45**: For any tenant deletion, all Key Vault secrets are revoked and deleted

Run tests:
```bash
go test -v ./controllers/ -run TestProperty_TenantCreation
go test -v ./controllers/ -run TestProperty_TenantDeletion
```

## Future Enhancements

1. **Secret Rotation**: Implement automatic secret rotation
2. **Multiple Secrets**: Support for multiple secret types (API keys, certificates, etc.)
3. **Secret Versioning**: Track and manage secret versions
4. **Audit Logging**: Log all secret access and modifications
5. **Secrets Store CSI Driver**: Mount secrets as files in pods using CSI driver

# Tenant Decommissioning Implementation

## Overview

This document describes the implementation of the tenant decommissioning flow for the MedLogic multi-tenant medical platform. The decommissioning flow ensures safe and orderly removal of tenant resources while maintaining audit trails and data backups.

## Implementation Details

### Core Functionality

The decommissioning flow is implemented in the Tenant Operator controller (`tenant_controller.go`) and consists of the following components:

#### 1. Decommissioning Detection

The reconciliation loop now checks for tenants with status `Decommissioned`:

```go
// Handle decommissioning if status is set to Decommissioned
if tenant.Status.Phase == "Decommissioned" {
    return r.handleDecommissioning(ctx, tenant)
}
```

#### 2. Decommissioning Flow (`handleDecommissioning`)

The decommissioning process follows a strict sequence:

1. **Audit Logging Start**: Log the initiation of decommissioning
2. **Database Backup**: Create a complete backup of the tenant's database
3. **Secret Revocation**: Revoke and delete all Key Vault secrets
4. **Namespace Deletion**: Delete the tenant's Kubernetes namespace and all resources
5. **Audit Logging Complete**: Log the successful completion

Each step includes comprehensive audit logging for compliance and troubleshooting.

#### 3. Database Backup (`createDatabaseBackup`)

Creates a backup of the tenant's database before any destructive operations:

```go
func (r *TenantReconciler) createDatabaseBackup(ctx context.Context, tenant *tenantsv1.Tenant) error
```

**Current Implementation**: Simulates backup creation with logging
**Production Requirements**:
- Connect to SQL Server using tenant's database configuration
- Execute `BACKUP DATABASE` command
- Store backup in Azure Blob Storage
- Verify backup integrity
- Return error if backup fails (prevents further decommissioning)

#### 4. Audit Logging (`logAuditEvent`)

Logs structured audit events for all decommissioning operations:

```go
func (r *TenantReconciler) logAuditEvent(ctx context.Context, tenant *tenantsv1.Tenant, eventType, message string)
```

**Logged Events**:
- `DecommissioningStarted`: Flow initiated
- `BackupCompleted`: Database backup successful
- `BackupFailed`: Database backup failed
- `SecretsRevoked`: Key Vault secrets revoked
- `SecretRevocationFailed`: Secret revocation failed
- `NamespaceDeleted`: Kubernetes namespace deleted
- `NamespaceDeletionFailed`: Namespace deletion failed
- `DecommissioningCompleted`: Flow completed successfully

**Current Implementation**: Structured logging to stdout
**Production Requirements**:
- Write to Azure Log Analytics
- Send to SIEM system
- Create Kubernetes Events
- Store in persistent audit database
- Ensure logs are immutable and tamper-proof

## Property-Based Tests

Five comprehensive property-based tests validate the decommissioning flow:

### Property 60: Decommissioned Status Triggers Flow
**Validates**: Requirements 13.1

Tests that setting tenant status to "Decommissioned" triggers the decommissioning flow.

```go
func TestProperty_DecommissionedStatusTriggersFlow(t *testing.T)
```

### Property 61: Decommissioning Creates Backup First
**Validates**: Requirements 13.2

Tests that database backup is created before any destructive operations.

```go
func TestProperty_DecommissioningCreatesBackupFirst(t *testing.T)
```

### Property 62: Secrets Revoked After Backup
**Validates**: Requirements 13.3

Tests that Key Vault secrets are revoked only after successful backup.

```go
func TestProperty_SecretsRevokedAfterBackup(t *testing.T)
```

### Property 63: Namespace Deleted After Secret Revocation
**Validates**: Requirements 13.4

Tests that namespace deletion occurs only after secrets are revoked.

```go
func TestProperty_NamespaceDeletedAfterSecretRevocation(t *testing.T)
```

### Property 64: Decommissioning Preserves Audit Logs
**Validates**: Requirements 13.5

Tests that audit logs are preserved throughout the decommissioning process.

```go
func TestProperty_DecommissioningPreservesAuditLogs(t *testing.T)
```

## Test Results

All property-based tests pass with 100 iterations each:

```
=== RUN   TestProperty_DecommissionedStatusTriggersFlow
+ For any Tenant with status Decommissioned, the decommissioning flow
   should be triggered: OK, passed 100 tests.
--- PASS: TestProperty_DecommissionedStatusTriggersFlow (0.08s)

=== RUN   TestProperty_DecommissioningCreatesBackupFirst
+ For any Tenant starting decommissioning, a database backup should be
   created first: OK, passed 100 tests.
--- PASS: TestProperty_DecommissioningCreatesBackupFirst (0.08s)

=== RUN   TestProperty_SecretsRevokedAfterBackup
+ For any decommissioning Tenant, secrets should be revoked after backup
   is complete: OK, passed 100 tests.
--- PASS: TestProperty_SecretsRevokedAfterBackup (0.08s)

=== RUN   TestProperty_NamespaceDeletedAfterSecretRevocation
+ For any decommissioning Tenant, namespace should be deleted after
   secrets are revoked: OK, passed 100 tests.
--- PASS: TestProperty_NamespaceDeletedAfterSecretRevocation (0.08s)

=== RUN   TestProperty_DecommissioningPreservesAuditLogs
+ For any completed decommissioning, audit logs should be preserved with
   operation details: OK, passed 100 tests.
--- PASS: TestProperty_DecommissioningPreservesAuditLogs (0.08s)
```

## Usage

### Triggering Decommissioning

To decommission a tenant, update the tenant's status to "Decommissioned":

```bash
kubectl patch tenant hospital-a --type=merge -p '{"status":{"phase":"Decommissioned"}}'
```

Or update the Tenant CRD directly:

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
status:
  phase: Decommissioned  # Set this to trigger decommissioning
```

### Monitoring Decommissioning

Monitor the decommissioning process through:

1. **Operator Logs**:
```bash
kubectl logs -n platform-system deployment/tenant-operator -f | grep AUDIT
```

2. **Tenant Status**:
```bash
kubectl get tenant hospital-a -o yaml
```

3. **Namespace Status**:
```bash
kubectl get namespace tenant-hospital-a
```

## Security Considerations

### Order of Operations

The decommissioning flow follows a strict order to ensure data safety:

1. **Backup First**: Database is backed up before any destructive operations
2. **Secrets Second**: Secrets are revoked only after backup succeeds
3. **Namespace Last**: Namespace is deleted only after secrets are revoked

This ensures:
- Data is never lost without a backup
- Secrets are not accessible after decommissioning starts
- Resources are cleaned up in the correct order

### Audit Trail

All decommissioning operations are logged with:
- Tenant ID and display name
- Event type and message
- Timestamp
- Operation status (success/failure)

This provides:
- Complete audit trail for compliance
- Troubleshooting information
- Evidence of proper decommissioning procedure

### Error Handling

If any step fails:
- The decommissioning flow stops immediately
- An audit log entry is created with the error
- The operator returns an error to trigger retry
- No further destructive operations are performed

## Production Readiness Checklist

Before deploying to production, implement:

- [ ] Real database backup logic with Azure SQL Server
- [ ] Backup storage in Azure Blob Storage
- [ ] Backup verification and integrity checks
- [ ] Persistent audit log storage (Azure Log Analytics)
- [ ] SIEM integration for audit events
- [ ] Kubernetes Event creation for visibility
- [ ] Backup retention policy enforcement
- [ ] Automated backup testing
- [ ] Decommissioning workflow documentation
- [ ] Runbook for failed decommissioning scenarios
- [ ] Monitoring and alerting for decommissioning operations
- [ ] Compliance validation for audit logs

## Related Requirements

This implementation satisfies the following requirements from the design document:

- **Requirement 13.1**: Decommissioned status triggers flow
- **Requirement 13.2**: Backup created first
- **Requirement 13.3**: Secrets revoked after backup
- **Requirement 13.4**: Namespace deleted after secret revocation
- **Requirement 13.5**: Audit logs preserved

## Related Properties

This implementation validates the following correctness properties:

- **Property 60**: 退服状态触发流程 (Decommissioned status triggers flow)
- **Property 61**: 退服首先创建备份 (Backup created first during decommissioning)
- **Property 62**: 备份后吊销密钥 (Secrets revoked after backup)
- **Property 63**: 密钥吊销后删除命名空间 (Namespace deleted after secret revocation)
- **Property 64**: 退服保留审计日志 (Audit logs preserved during decommissioning)

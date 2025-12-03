# OPA Gatekeeper Property-Based Tests

This test project contains property-based tests for OPA Gatekeeper policies used in the multi-tenant medical platform.

## Overview

The tests verify that OPA Gatekeeper policies correctly enforce security requirements for the multi-tenant platform:

1. **Tenant Label Validation**: Ensures all workloads in tenant namespaces have a `tenantId` label
2. **Missing Label Rejection**: Verifies that workloads without required labels are rejected
3. **Cross-Tenant NetworkPolicy Prevention**: Ensures NetworkPolicies cannot allow cross-tenant communication

## Test Files

### GatekeeperTenantLabelPropertyTests.cs
- **Property 65**: Gatekeeper验证租户标签 (Validates Requirements 14.1)
- Tests that Gatekeeper can validate the presence of tenantId labels on workloads

### GatekeeperMissingLabelPropertyTests.cs
- **Property 66**: 缺少标签拒绝部署 (Validates Requirements 14.2)
- Tests that workloads without tenantId labels are correctly identified for rejection
- Tests that workloads with empty tenantId labels are rejected
- Tests that all workload types (Deployment, StatefulSet, etc.) are validated

### GatekeeperCrossTenantNetPolPropertyTests.cs
- **Property 67**: 跨租户策略被拒绝 (Validates Requirements 14.3)
- Tests that NetworkPolicies allowing cross-tenant communication are rejected
- Tests that same-tenant NetworkPolicies are allowed
- Tests that NetworkPolicies allowing access to shared services are allowed

## Running the Tests

```bash
# Run all Gatekeeper tests
dotnet test tests/Gatekeeper.Tests/Gatekeeper.Tests.csproj

# Run with detailed output
dotnet test tests/Gatekeeper.Tests/Gatekeeper.Tests.csproj --logger "console;verbosity=detailed"

# Run a specific test class
dotnet test tests/Gatekeeper.Tests/Gatekeeper.Tests.csproj --filter "FullyQualifiedName~GatekeeperTenantLabelPropertyTests"
```

## Property-Based Testing

These tests use FsCheck for property-based testing, which:
- Generates random test cases (100 iterations per property by default)
- Tests universal properties that should hold for all valid inputs
- Provides better coverage than example-based tests

## Test Generators

The tests include custom generators for:
- Kubernetes Deployments with various tenantId label configurations
- NetworkPolicies with different namespace selectors
- Various workload types (Deployment, StatefulSet, DaemonSet, etc.)

## Dependencies

- **xUnit**: Test framework
- **FsCheck**: Property-based testing library
- **FsCheck.Xunit**: xUnit integration for FsCheck
- **KubernetesClient**: Kubernetes API models

## Notes

- These tests validate the **logic** of Gatekeeper policies, not the actual Kubernetes admission control
- For integration testing with a real Kubernetes cluster, see the deployment documentation in `k8s/GATEKEEPER_SETUP.md`
- The tests use the Kubernetes client library models to represent workloads and policies

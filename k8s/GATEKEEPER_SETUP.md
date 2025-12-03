# OPA Gatekeeper Setup Guide

This guide explains how to install and configure OPA Gatekeeper for the multi-tenant medical platform.

## Overview

OPA Gatekeeper is used to enforce security policies at the Kubernetes admission control level. This ensures that:
1. All workloads in tenant namespaces have a `tenantId` label
2. NetworkPolicies cannot allow cross-tenant communication

## Installation

### Option 1: Using kubectl

```bash
kubectl apply -f https://raw.githubusercontent.com/open-policy-agent/gatekeeper/release-3.14/deploy/gatekeeper.yaml
```

### Option 2: Using Helm

```bash
helm repo add gatekeeper https://open-policy-agent.github.io/gatekeeper/charts
helm install gatekeeper gatekeeper/gatekeeper --namespace gatekeeper-system --create-namespace
```

### Verify Installation

```bash
# Check that Gatekeeper pods are running
kubectl get pods -n gatekeeper-system

# Check that Gatekeeper CRDs are installed
kubectl get crd | grep gatekeeper
```

## Policy Configuration

### 1. Tenant Label Requirement Policy

This policy ensures all workloads in tenant namespaces have a `tenantId` label.

**Install the ConstraintTemplate:**
```bash
kubectl apply -f gatekeeper-constraint-template-tenant-label.yaml
```

**Install the Constraint:**
```bash
kubectl apply -f gatekeeper-constraint-tenant-label.yaml
```

**What it does:**
- Validates Deployments, StatefulSets, DaemonSets, Jobs, CronJobs, Pods, and ReplicaSets
- Only applies to namespaces starting with "tenant-"
- Rejects resources without a `tenantId` label
- Rejects resources with an empty `tenantId` label

### 2. No Cross-Tenant NetworkPolicy

This policy prevents NetworkPolicies from allowing cross-tenant communication.

**Install the ConstraintTemplate:**
```bash
kubectl apply -f gatekeeper-constraint-template-no-cross-tenant-netpol.yaml
```

**Install the Constraint:**
```bash
kubectl apply -f gatekeeper-constraint-no-cross-tenant-netpol.yaml
```

**What it does:**
- Validates NetworkPolicy resources
- Only applies to namespaces starting with "tenant-"
- Rejects NetworkPolicies that allow ingress from other tenant namespaces
- Rejects NetworkPolicies that allow egress to other tenant namespaces

## Testing the Policies

### Test 1: Deploy without tenantId label (should fail)

```bash
cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: test-deployment
  namespace: tenant-hospital-a
spec:
  replicas: 1
  selector:
    matchLabels:
      app: test
  template:
    metadata:
      labels:
        app: test
    spec:
      containers:
      - name: nginx
        image: nginx:latest
EOF
```

Expected result: **Rejected** with message about missing `tenantId` label

### Test 2: Deploy with tenantId label (should succeed)

```bash
cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: test-deployment
  namespace: tenant-hospital-a
  labels:
    tenantId: hospital-a
spec:
  replicas: 1
  selector:
    matchLabels:
      app: test
  template:
    metadata:
      labels:
        app: test
        tenantId: hospital-a
    spec:
      containers:
      - name: nginx
        image: nginx:latest
EOF
```

Expected result: **Accepted**

### Test 3: Create cross-tenant NetworkPolicy (should fail)

```bash
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: cross-tenant-policy
  namespace: tenant-hospital-a
spec:
  podSelector: {}
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: tenant-hospital-b
EOF
```

Expected result: **Rejected** with message about cross-tenant communication

### Test 4: Create same-tenant NetworkPolicy (should succeed)

```bash
cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: allow-same-tenant
  namespace: tenant-hospital-a
spec:
  podSelector: {}
  ingress:
  - from:
    - podSelector: {}
EOF
```

Expected result: **Accepted**

## Monitoring and Troubleshooting

### View Constraint Status

```bash
kubectl get constraints
kubectl describe k8srequiretenantlabel require-tenant-label
kubectl describe k8snocrosstenannetpol no-cross-tenant-networkpolicy
```

### View Violations

```bash
kubectl get k8srequiretenantlabel require-tenant-label -o yaml
kubectl get k8snocrosstenannetpol no-cross-tenant-networkpolicy -o yaml
```

### Check Gatekeeper Logs

```bash
kubectl logs -n gatekeeper-system -l control-plane=controller-manager
kubectl logs -n gatekeeper-system -l control-plane=audit-controller
```

### Disable a Constraint Temporarily

```bash
kubectl delete k8srequiretenantlabel require-tenant-label
# Or delete the constraint
kubectl delete k8snocrosstenannetpol no-cross-tenant-networkpolicy
```

## Policy Updates

When you update a ConstraintTemplate, the changes take effect immediately for new admission requests. Existing resources are not affected unless they are updated.

To update a policy:
1. Edit the ConstraintTemplate YAML file
2. Apply the changes: `kubectl apply -f <template-file>.yaml`
3. The constraint will automatically use the new template

## Uninstallation

To remove Gatekeeper:

```bash
# Remove constraints first
kubectl delete k8srequiretenantlabel require-tenant-label
kubectl delete k8snocrosstenannetpol no-cross-tenant-networkpolicy

# Remove constraint templates
kubectl delete constrainttemplate k8srequiretenantlabel
kubectl delete constrainttemplate k8snocrosstenannetpol

# Remove Gatekeeper
kubectl delete -f https://raw.githubusercontent.com/open-policy-agent/gatekeeper/release-3.14/deploy/gatekeeper.yaml
```

## References

- [OPA Gatekeeper Documentation](https://open-policy-agent.github.io/gatekeeper/)
- [Rego Language Guide](https://www.openpolicyagent.org/docs/latest/policy-language/)
- [Gatekeeper Policy Library](https://github.com/open-policy-agent/gatekeeper-library)

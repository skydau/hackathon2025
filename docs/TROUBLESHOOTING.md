# MedLogic Platform Troubleshooting Guide

This guide provides solutions to common issues encountered when deploying and operating the MedLogic Multi-Tenant Medical Platform.

## Table of Contents

1. [Deployment Issues](#deployment-issues)
2. [Service Health Issues](#service-health-issues)
3. [Database Connectivity](#database-connectivity)
4. [Workload Identity Issues](#workload-identity-issues)
5. [Network and Routing Issues](#network-and-routing-issues)
6. [Tenant Operator Issues](#tenant-operator-issues)
7. [Performance Issues](#performance-issues)
8. [Security and Policy Issues](#security-and-policy-issues)

## Deployment Issues

### Issue: Pods Stuck in Pending State

**Symptoms:**
```bash
kubectl get pods -n platform-system
NAME                                     READY   STATUS    RESTARTS   AGE
tenant-catalog-service-xxx               0/1     Pending   0          5m
```

**Possible Causes:**
1. Insufficient cluster resources
2. Image pull errors
3. PersistentVolumeClaim issues

**Diagnosis:**
```bash
# Check pod events
kubectl describe pod <pod-name> -n platform-system

# Check node resources
kubectl top nodes

# Check for resource quotas
kubectl get resourcequota -n platform-system
```

**Solutions:**

**Insufficient Resources:**
```bash
# Scale up AKS cluster
az aks scale \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --node-count 5
```

**Image Pull Errors:**
```bash
# Verify ACR attachment
az aks check-acr \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --acr $ACR_NAME

# Re-attach ACR if needed
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --attach-acr $ACR_NAME
```

### Issue: Helm Installation Fails

**Symptoms:**
```
Error: INSTALLATION FAILED: timed out waiting for the condition
```

**Diagnosis:**
```bash
# Check Helm release status
helm list -n platform-system

# Get detailed status
helm status medlogic-platform -n platform-system

# Check for failed pods
kubectl get pods -n platform-system | grep -v Running
```

**Solutions:**

**Increase Timeout:**
```bash
helm upgrade --install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --timeout 15m \
  --wait
```

**Debug Mode:**
```bash
helm install medlogic-platform \
  ./helm/medlogic-platform \
  --values values-production.yaml \
  --namespace platform-system \
  --debug \
  --dry-run
```

## Service Health Issues

### Issue: Service Failing Health Checks

**Symptoms:**
```bash
kubectl get pods -n platform-system
NAME                                     READY   STATUS    RESTARTS   AGE
tenant-catalog-service-xxx               0/1     Running   5          10m
```

**Diagnosis:**
```bash
# Check pod logs
kubectl logs -n platform-system deployment/tenant-catalog-service --tail=100

# Check liveness probe
kubectl describe pod <pod-name> -n platform-system | grep -A 10 "Liveness"

# Test health endpoint manually
kubectl exec -n platform-system <pod-name> -- \
  curl -f http://localhost:8080/health
```

**Common Causes and Solutions:**

**Database Connection Issues:**
```bash
# Check database connectivity
kubectl exec -n platform-system <pod-name> -- \
  nc -zv medlogic-sql.database.windows.net 1433

# Verify connection string in Key Vault
az keyvault secret show \
  --vault-name $KEY_VAULT_NAME \
  --name TenantCatalog-ConnectionString
```

**Missing Environment Variables:**
```bash
# Check pod environment
kubectl exec -n platform-system <pod-name> -- env | grep -i azure

# Verify ConfigMap
kubectl get configmap -n platform-system
kubectl describe configmap <configmap-name> -n platform-system
```

**Application Errors:**
```bash
# Get detailed logs
kubectl logs -n platform-system <pod-name> --previous

# Check for exceptions
kubectl logs -n platform-system <pod-name> | grep -i "exception\|error\|fatal"
```

### Issue: Service Returns 503 Errors

**Symptoms:**
```bash
curl http://<gateway-ip>/api/tenants
503 Service Unavailable
```

**Diagnosis:**
```bash
# Check service endpoints
kubectl get endpoints -n platform-system tenant-catalog

# Check if pods are ready
kubectl get pods -n platform-system -l app=tenant-catalog-service

# Check service definition
kubectl describe svc tenant-catalog -n platform-system
```

**Solutions:**

**No Ready Pods:**
```bash
# Check readiness probe configuration
kubectl get deployment tenant-catalog-service -n platform-system -o yaml | grep -A 10 readinessProbe

# Adjust probe timing if needed
kubectl patch deployment tenant-catalog-service -n platform-system --type='json' \
  -p='[{"op": "replace", "path": "/spec/template/spec/containers/0/readinessProbe/initialDelaySeconds", "value": 30}]'
```

## Database Connectivity

### Issue: Cannot Connect to Azure SQL

**Symptoms:**
```
Error: A network-related or instance-specific error occurred while establishing a connection to SQL Server
```

**Diagnosis:**
```bash
# Test connectivity from pod
kubectl run sqltest --image=mcr.microsoft.com/mssql-tools -i --rm --restart=Never -- \
  /opt/mssql-tools/bin/sqlcmd -S medlogic-sql.database.windows.net -U sqladmin -P '<password>' -Q "SELECT 1"

# Check firewall rules
az sql server firewall-rule list \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME
```

**Solutions:**

**Add Firewall Rule:**
```bash
# Allow AKS outbound IPs
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAKS \
  --start-ip-address <aks-outbound-ip> \
  --end-ip-address <aks-outbound-ip>
```

**Enable Service Endpoints:**
```bash
# Get AKS subnet
AKS_SUBNET=$(az aks show \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --query "agentPoolProfiles[0].vnetSubnetId" -o tsv)

# Add virtual network rule
az sql server vnet-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAKSSubnet \
  --subnet $AKS_SUBNET
```

### Issue: Database Migration Fails

**Symptoms:**
```
Error: The database 'TenantCatalog' does not exist
```

**Diagnosis:**
```bash
# Check if database exists
az sql db show \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog

# Check migration status
kubectl logs -n platform-system deployment/tenant-catalog-service | grep -i "migration"
```

**Solutions:**

**Create Database:**
```bash
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog \
  --service-objective S1
```

**Run Migrations Manually:**
```bash
# From local machine
cd src/TenantCatalogService
dotnet ef database update --connection "Server=medlogic-sql.database.windows.net;Database=TenantCatalog;..."

# Or from pod
kubectl exec -n platform-system deployment/tenant-catalog-service -- \
  dotnet ef database update
```

## Workload Identity Issues

### Issue: Pod Cannot Authenticate to Azure Resources

**Symptoms:**
```
Error: ManagedIdentityCredential authentication failed: No managed identity endpoint found
```

**Diagnosis:**
```bash
# Check if workload identity is enabled on cluster
az aks show \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --query "oidcIssuerProfile.enabled"

# Check pod annotations
kubectl describe pod <pod-name> -n platform-system | grep -i "azure.workload.identity"

# Check service account
kubectl get sa tenant-catalog-sa -n platform-system -o yaml
```

**Solutions:**

**Enable Workload Identity:**
```bash
az aks update \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --enable-workload-identity \
  --enable-oidc-issuer
```

**Fix Service Account Annotations:**
```bash
kubectl annotate sa tenant-catalog-sa -n platform-system \
  azure.workload.identity/client-id=<client-id> \
  azure.workload.identity/tenant-id=<tenant-id> \
  --overwrite
```

**Recreate Federated Credential:**
```bash
# Get OIDC issuer URL
OIDC_ISSUER=$(az aks show \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --query "oidcIssuerProfile.issuerUrl" -o tsv)

# Create federated credential
az ad app federated-credential create \
  --id <app-id> \
  --parameters '{
    "name": "tenant-catalog-federated",
    "issuer": "'$OIDC_ISSUER'",
    "subject": "system:serviceaccount:platform-system:tenant-catalog-sa",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

### Issue: Key Vault Access Denied

**Symptoms:**
```
Error: The user, group or application does not have secrets get permission on key vault
```

**Diagnosis:**
```bash
# Check Key Vault access policies
az keyvault show \
  --name $KEY_VAULT_NAME \
  --query "properties.enableRbacAuthorization"

# Check role assignments
az role assignment list \
  --scope /subscriptions/<sub-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME
```

**Solutions:**

**Grant RBAC Permissions:**
```bash
# Get managed identity principal ID
PRINCIPAL_ID=$(az ad sp show --id <client-id> --query id -o tsv)

# Grant Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope /subscriptions/<sub-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT_NAME
```

## Network and Routing Issues

### Issue: Gateway Cannot Reach Backend Services

**Symptoms:**
```
502 Bad Gateway
```

**Diagnosis:**
```bash
# Check gateway logs
kubectl logs -n gateway deployment/smart-gateway --tail=100

# Test connectivity from gateway pod
kubectl exec -n gateway <gateway-pod> -- \
  curl -v http://tenant-catalog.platform-system.svc.cluster.local:8080/health

# Check DNS resolution
kubectl exec -n gateway <gateway-pod> -- \
  nslookup tenant-catalog.platform-system.svc.cluster.local
```

**Solutions:**

**DNS Issues:**
```bash
# Restart CoreDNS
kubectl rollout restart deployment/coredns -n kube-system

# Check CoreDNS logs
kubectl logs -n kube-system -l k8s-app=kube-dns
```

**Network Policy Blocking:**
```bash
# Check network policies
kubectl get networkpolicies -n platform-system

# Temporarily disable to test
kubectl delete networkpolicy <policy-name> -n platform-system
```

### Issue: Cross-Tenant Communication Not Blocked

**Symptoms:**
Pods from one tenant namespace can access another tenant's services.

**Diagnosis:**
```bash
# Check network policies
kubectl get networkpolicies -n tenant-hospital-a

# Test connectivity
kubectl exec -n tenant-hospital-a <pod> -- \
  curl http://service.tenant-hospital-b.svc.cluster.local
```

**Solutions:**

**Apply Network Policies:**
```bash
# Apply default deny policy
kubectl apply -f - <<EOF
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: default-deny-cross-namespace
  namespace: tenant-hospital-a
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
  ingress: []
  egress:
  - to:
    - namespaceSelector:
        matchLabels:
          name: tenant-hospital-a
EOF
```

**Verify CNI Plugin:**
```bash
# Check if Azure CNI is enabled
az aks show \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_CLUSTER \
  --query "networkProfile.networkPlugin"
```

## Tenant Operator Issues

### Issue: Tenant CRD Not Creating Resources

**Symptoms:**
```bash
kubectl get tenant hospital-a
NAME         PHASE         AGE
hospital-a   Provisioning  10m
```

**Diagnosis:**
```bash
# Check operator logs
kubectl logs -n platform-system deployment/tenant-operator --tail=100

# Check tenant status
kubectl describe tenant hospital-a

# Check for events
kubectl get events --sort-by='.lastTimestamp' | grep tenant
```

**Solutions:**

**Operator Not Running:**
```bash
# Check operator pod
kubectl get pods -n platform-system -l app=tenant-operator

# Restart operator
kubectl rollout restart deployment/tenant-operator -n platform-system
```

**RBAC Issues:**
```bash
# Check operator service account
kubectl get sa tenant-operator -n platform-system

# Verify RBAC permissions
kubectl auth can-i create namespaces --as=system:serviceaccount:platform-system:tenant-operator
kubectl auth can-i create configmaps --as=system:serviceaccount:platform-system:tenant-operator
```

**CRD Not Installed:**
```bash
# Check if CRD exists
kubectl get crd tenants.medlogic.io

# Reinstall CRD
kubectl apply -f tenant-operator/config/crd/tenants.medlogic.io_tenants.yaml
```

## Performance Issues

### Issue: High Latency

**Symptoms:**
API responses taking > 2 seconds

**Diagnosis:**
```bash
# Check pod resource usage
kubectl top pods -n platform-system

# Check database performance
az sql db show-usage \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog

# Check for throttling
kubectl logs -n gateway deployment/smart-gateway | grep "429"
```

**Solutions:**

**Scale Services:**
```bash
# Horizontal scaling
kubectl scale deployment/tenant-catalog-service --replicas=5 -n platform-system

# Enable autoscaling
kubectl autoscale deployment/tenant-catalog-service \
  --min=3 --max=10 --cpu-percent=70 \
  -n platform-system
```

**Upgrade Database:**
```bash
az sql db update \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name TenantCatalog \
  --service-objective S3
```

**Add Connection Pooling:**
Check application configuration for connection pool settings.

### Issue: Memory Leaks

**Symptoms:**
```bash
kubectl top pods -n platform-system
NAME                                     CPU   MEMORY
tenant-catalog-service-xxx               50m   480Mi  # Increasing over time
```

**Diagnosis:**
```bash
# Monitor memory over time
watch kubectl top pods -n platform-system

# Check for OOMKilled events
kubectl get events -n platform-system | grep OOMKilled

# Get heap dump (if .NET)
kubectl exec -n platform-system <pod-name> -- \
  dotnet-dump collect -p 1
```

**Solutions:**

**Increase Memory Limits:**
```bash
kubectl patch deployment tenant-catalog-service -n platform-system --type='json' \
  -p='[{"op": "replace", "path": "/spec/template/spec/containers/0/resources/limits/memory", "value": "1Gi"}]'
```

**Restart Pods Periodically:**
```bash
# Add to deployment
kubectl patch deployment tenant-catalog-service -n platform-system --type='json' \
  -p='[{"op": "add", "path": "/spec/template/spec/containers/0/lifecycle", "value": {"preStop": {"exec": {"command": ["/bin/sh", "-c", "sleep 15"]}}}}]'
```

## Security and Policy Issues

### Issue: OPA Gatekeeper Blocking Valid Deployments

**Symptoms:**
```
Error from server: admission webhook "validation.gatekeeper.sh" denied the request
```

**Diagnosis:**
```bash
# Check Gatekeeper constraints
kubectl get constraints

# Check constraint details
kubectl describe constraint tenant-label-required

# Check audit results
kubectl get constraint tenant-label-required -o yaml | grep -A 20 violations
```

**Solutions:**

**Add Required Labels:**
```bash
kubectl patch deployment <deployment-name> -n <namespace> --type='json' \
  -p='[{"op": "add", "path": "/spec/template/metadata/labels/tenantId", "value": "hospital-a"}]'
```

**Temporarily Disable Constraint:**
```bash
kubectl delete constraint tenant-label-required
```

**Fix Constraint Template:**
```bash
kubectl edit constrainttemplate tenant-label
```

### Issue: Secrets Not Mounting

**Symptoms:**
```
Error: secret "tenant-db-credentials" not found
```

**Diagnosis:**
```bash
# Check if CSI driver is installed
kubectl get pods -n kube-system | grep secrets-store

# Check SecretProviderClass
kubectl get secretproviderclass -n platform-system

# Check pod events
kubectl describe pod <pod-name> -n platform-system | grep -A 10 "Events"
```

**Solutions:**

**Install CSI Driver:**
```bash
helm repo add csi-secrets-store-provider-azure https://azure.github.io/secrets-store-csi-driver-provider-azure/charts
helm install csi csi-secrets-store-provider-azure/csi-secrets-store-provider-azure \
  --namespace kube-system
```

**Fix SecretProviderClass:**
```bash
kubectl apply -f - <<EOF
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-keyvault
  namespace: platform-system
spec:
  provider: azure
  parameters:
    usePodIdentity: "false"
    useVMManagedIdentity: "false"
    clientID: "<client-id>"
    keyvaultName: "$KEY_VAULT_NAME"
    tenantId: "<tenant-id>"
    objects: |
      array:
        - |
          objectName: TenantCatalog-ConnectionString
          objectType: secret
EOF
```

## Getting Help

If you cannot resolve an issue:

1. **Collect Diagnostics:**
```bash
./scripts/collect-diagnostics.sh
```

2. **Check Documentation:**
- [Deployment Guide](./DEPLOYMENT_GUIDE.md)
- [Architecture Documentation](../README.md)

3. **Contact Support:**
- Create a GitHub issue with diagnostics
- Email: platform@medlogic.io
- Include: cluster info, pod logs, error messages

## Useful Commands Reference

```bash
# Get all resources in namespace
kubectl get all -n platform-system

# Describe all pods
kubectl describe pods -n platform-system

# Get logs from all pods
kubectl logs -n platform-system -l app=tenant-catalog-service --tail=100

# Port forward for local testing
kubectl port-forward -n platform-system svc/tenant-catalog 8080:8080

# Execute command in pod
kubectl exec -it -n platform-system <pod-name> -- /bin/sh

# Check resource usage
kubectl top nodes
kubectl top pods -n platform-system

# Get events
kubectl get events -n platform-system --sort-by='.lastTimestamp'
```

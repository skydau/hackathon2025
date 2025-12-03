#!/bin/bash

# Diagnostic Collection Script
# Collects logs, events, and configuration for troubleshooting

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

# Create output directory
OUTPUT_DIR="diagnostics-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$OUTPUT_DIR"

print_info "Collecting diagnostics to $OUTPUT_DIR/"

# Cluster info
print_info "Collecting cluster information..."
kubectl cluster-info > "$OUTPUT_DIR/cluster-info.txt" 2>&1
kubectl version > "$OUTPUT_DIR/kubectl-version.txt" 2>&1
kubectl get nodes -o wide > "$OUTPUT_DIR/nodes.txt" 2>&1

# Namespaces
print_info "Collecting namespace information..."
kubectl get namespaces > "$OUTPUT_DIR/namespaces.txt" 2>&1

# Platform system namespace
print_info "Collecting platform-system resources..."
kubectl get all -n platform-system -o wide > "$OUTPUT_DIR/platform-system-resources.txt" 2>&1
kubectl describe pods -n platform-system > "$OUTPUT_DIR/platform-system-pods-describe.txt" 2>&1
kubectl get events -n platform-system --sort-by='.lastTimestamp' > "$OUTPUT_DIR/platform-system-events.txt" 2>&1

# Gateway namespace
print_info "Collecting gateway resources..."
kubectl get all -n gateway -o wide > "$OUTPUT_DIR/gateway-resources.txt" 2>&1
kubectl describe pods -n gateway > "$OUTPUT_DIR/gateway-pods-describe.txt" 2>&1
kubectl get events -n gateway --sort-by='.lastTimestamp' > "$OUTPUT_DIR/gateway-events.txt" 2>&1

# Logs from platform services
print_info "Collecting service logs..."
SERVICES=(
    "tenant-catalog-service"
    "device-registry-service"
    "medlogic-service"
)

for service in "${SERVICES[@]}"; do
    print_info "Collecting logs for $service..."
    kubectl logs -n platform-system deployment/$service --tail=500 > "$OUTPUT_DIR/${service}-logs.txt" 2>&1 || true
    kubectl logs -n platform-system deployment/$service --previous --tail=500 > "$OUTPUT_DIR/${service}-logs-previous.txt" 2>&1 || true
done

# Gateway logs
print_info "Collecting gateway logs..."
kubectl logs -n gateway deployment/smart-gateway --tail=500 > "$OUTPUT_DIR/smart-gateway-logs.txt" 2>&1 || true

# Tenant Operator logs
print_info "Collecting tenant operator logs..."
kubectl logs -n platform-system deployment/tenant-operator --tail=500 > "$OUTPUT_DIR/tenant-operator-logs.txt" 2>&1 || true

# ConfigMaps and Secrets (names only, not content)
print_info "Collecting ConfigMaps and Secrets..."
kubectl get configmaps -n platform-system > "$OUTPUT_DIR/configmaps.txt" 2>&1
kubectl get secrets -n platform-system > "$OUTPUT_DIR/secrets.txt" 2>&1

# Service Accounts
print_info "Collecting Service Accounts..."
kubectl get serviceaccounts -n platform-system -o yaml > "$OUTPUT_DIR/serviceaccounts.yaml" 2>&1

# Network Policies
print_info "Collecting Network Policies..."
kubectl get networkpolicies --all-namespaces -o yaml > "$OUTPUT_DIR/networkpolicies.yaml" 2>&1

# Resource Quotas
print_info "Collecting Resource Quotas..."
kubectl get resourcequotas --all-namespaces -o yaml > "$OUTPUT_DIR/resourcequotas.yaml" 2>&1

# CRDs
print_info "Collecting Custom Resource Definitions..."
kubectl get crd > "$OUTPUT_DIR/crds.txt" 2>&1
kubectl get tenants.medlogic.io --all-namespaces -o yaml > "$OUTPUT_DIR/tenant-crds.yaml" 2>&1 || true

# OPA Gatekeeper
print_info "Collecting OPA Gatekeeper information..."
kubectl get constraints > "$OUTPUT_DIR/gatekeeper-constraints.txt" 2>&1 || true
kubectl get constrainttemplates > "$OUTPUT_DIR/gatekeeper-templates.txt" 2>&1 || true

# Helm releases
print_info "Collecting Helm releases..."
helm list --all-namespaces > "$OUTPUT_DIR/helm-releases.txt" 2>&1 || true

# Resource usage
print_info "Collecting resource usage..."
kubectl top nodes > "$OUTPUT_DIR/nodes-usage.txt" 2>&1 || true
kubectl top pods -n platform-system > "$OUTPUT_DIR/platform-system-pods-usage.txt" 2>&1 || true
kubectl top pods -n gateway > "$OUTPUT_DIR/gateway-pods-usage.txt" 2>&1 || true

# Ingress/Services
print_info "Collecting ingress and service information..."
kubectl get ingress --all-namespaces -o wide > "$OUTPUT_DIR/ingress.txt" 2>&1
kubectl get services --all-namespaces -o wide > "$OUTPUT_DIR/services.txt" 2>&1

# PersistentVolumes
print_info "Collecting storage information..."
kubectl get pv > "$OUTPUT_DIR/persistent-volumes.txt" 2>&1
kubectl get pvc --all-namespaces > "$OUTPUT_DIR/persistent-volume-claims.txt" 2>&1

# Create archive
print_info "Creating archive..."
tar -czf "${OUTPUT_DIR}.tar.gz" "$OUTPUT_DIR"

print_info "Diagnostics collected successfully!"
echo ""
echo "Archive created: ${OUTPUT_DIR}.tar.gz"
echo ""
echo "Please attach this file when reporting issues."
echo "Note: Review the contents before sharing to ensure no sensitive data is included."

exit 0

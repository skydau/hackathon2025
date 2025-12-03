#!/bin/bash

# MedLogic Platform Automated Deployment Script
# This script automates the deployment of the MedLogic Multi-Tenant Medical Platform

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="development"
CONFIG_FILE=""
SKIP_BUILD=false
SKIP_TESTS=false
DRY_RUN=false
HELM_TIMEOUT="10m"

# Function to print colored output
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to display usage
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Deploy the MedLogic Multi-Tenant Medical Platform

OPTIONS:
    -e, --environment ENV       Deployment environment (development|staging|production) [default: development]
    -c, --config FILE          Path to values file for Helm
    -s, --skip-build           Skip building container images
    -t, --skip-tests           Skip running tests before deployment
    -d, --dry-run              Perform a dry run without actual deployment
    --timeout DURATION         Helm timeout duration [default: 10m]
    -h, --help                 Display this help message

EXAMPLES:
    # Deploy to development
    $0 --environment development

    # Deploy to production with custom config
    $0 --environment production --config values-production.yaml

    # Dry run for staging
    $0 --environment staging --dry-run

EOF
    exit 1
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -c|--config)
            CONFIG_FILE="$2"
            shift 2
            ;;
        -s|--skip-build)
            SKIP_BUILD=true
            shift
            ;;
        -t|--skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        -d|--dry-run)
            DRY_RUN=true
            shift
            ;;
        --timeout)
            HELM_TIMEOUT="$2"
            shift 2
            ;;
        -h|--help)
            usage
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            ;;
    esac
done

# Validate environment
if [[ ! "$ENVIRONMENT" =~ ^(development|staging|production)$ ]]; then
    print_error "Invalid environment: $ENVIRONMENT"
    exit 1
fi

print_info "Starting deployment for environment: $ENVIRONMENT"

# Check prerequisites
print_info "Checking prerequisites..."

command -v kubectl >/dev/null 2>&1 || { print_error "kubectl is required but not installed. Aborting."; exit 1; }
command -v helm >/dev/null 2>&1 || { print_error "helm is required but not installed. Aborting."; exit 1; }
command -v az >/dev/null 2>&1 || { print_error "Azure CLI is required but not installed. Aborting."; exit 1; }

# Check kubectl connectivity
if ! kubectl cluster-info &> /dev/null; then
    print_error "Cannot connect to Kubernetes cluster. Please check your kubeconfig."
    exit 1
fi

print_info "Prerequisites check passed"

# Set config file based on environment if not provided
if [ -z "$CONFIG_FILE" ]; then
    CONFIG_FILE="values-${ENVIRONMENT}.yaml"
    if [ ! -f "$CONFIG_FILE" ]; then
        print_warn "Config file $CONFIG_FILE not found. Using default values."
        CONFIG_FILE=""
    fi
fi

# Run tests
if [ "$SKIP_TESTS" = false ]; then
    print_info "Running tests..."
    if ! dotnet test --configuration Release --no-build; then
        print_error "Tests failed. Aborting deployment."
        exit 1
    fi
    print_info "Tests passed"
fi

# Build and push images
if [ "$SKIP_BUILD" = false ]; then
    print_info "Building and pushing container images..."
    
    # Get ACR name from config or environment
    ACR_NAME=$(grep "imageRegistry:" "$CONFIG_FILE" 2>/dev/null | awk '{print $2}' | cut -d'.' -f1)
    
    if [ -z "$ACR_NAME" ]; then
        print_warn "ACR name not found in config. Skipping image build."
    else
        print_info "Logging into ACR: $ACR_NAME"
        az acr login --name "$ACR_NAME"
        
        print_info "Building images..."
        ./scripts/build-and-push.sh "$ACR_NAME.azurecr.io"
    fi
fi

# Dry run check
if [ "$DRY_RUN" = true ]; then
    print_info "Performing dry run..."
    HELM_FLAGS="--dry-run --debug"
else
    HELM_FLAGS=""
fi

# Deploy with Helm
print_info "Deploying MedLogic Platform with Helm..."

HELM_CMD="helm upgrade --install medlogic-platform ./helm/medlogic-platform"
HELM_CMD="$HELM_CMD --namespace platform-system --create-namespace"
HELM_CMD="$HELM_CMD --timeout $HELM_TIMEOUT"

if [ -n "$CONFIG_FILE" ]; then
    HELM_CMD="$HELM_CMD --values $CONFIG_FILE"
fi

if [ "$DRY_RUN" = false ]; then
    HELM_CMD="$HELM_CMD --wait"
fi

HELM_CMD="$HELM_CMD $HELM_FLAGS"

print_info "Executing: $HELM_CMD"
eval $HELM_CMD

if [ $? -ne 0 ]; then
    print_error "Helm deployment failed"
    exit 1
fi

if [ "$DRY_RUN" = true ]; then
    print_info "Dry run completed successfully"
    exit 0
fi

# Wait for deployments to be ready
print_info "Waiting for deployments to be ready..."

DEPLOYMENTS=(
    "tenant-catalog-service"
    "device-registry-service"
    "medlogic-service"
)

for deployment in "${DEPLOYMENTS[@]}"; do
    print_info "Waiting for $deployment..."
    if ! kubectl rollout status deployment/$deployment -n platform-system --timeout=5m; then
        print_error "Deployment $deployment failed to become ready"
        exit 1
    fi
done

# Wait for gateway
print_info "Waiting for smart-gateway..."
if ! kubectl rollout status deployment/smart-gateway -n gateway --timeout=5m; then
    print_error "Gateway deployment failed to become ready"
    exit 1
fi

# Verify health
print_info "Verifying service health..."

# Check tenant catalog health
if kubectl run health-check --image=curlimages/curl --rm -i --restart=Never -- \
    curl -f http://tenant-catalog.platform-system.svc.cluster.local:8080/health &> /dev/null; then
    print_info "Tenant Catalog Service is healthy"
else
    print_warn "Tenant Catalog Service health check failed"
fi

# Check device registry health
if kubectl run health-check --image=curlimages/curl --rm -i --restart=Never -- \
    curl -f http://device-registry.platform-system.svc.cluster.local:8080/health &> /dev/null; then
    print_info "Device Registry Service is healthy"
else
    print_warn "Device Registry Service health check failed"
fi

# Get gateway external IP
print_info "Getting gateway external IP..."
GATEWAY_IP=$(kubectl get svc smart-gateway -n gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

if [ -z "$GATEWAY_IP" ]; then
    print_warn "Gateway external IP not yet assigned. It may take a few minutes."
else
    print_info "Gateway is accessible at: http://$GATEWAY_IP"
fi

# Display deployment summary
print_info "Deployment Summary:"
echo "===================="
echo "Environment: $ENVIRONMENT"
echo "Namespace: platform-system"
echo "Gateway Namespace: gateway"
if [ -n "$GATEWAY_IP" ]; then
    echo "Gateway IP: $GATEWAY_IP"
fi
echo ""

# Display pod status
print_info "Pod Status:"
kubectl get pods -n platform-system
echo ""
kubectl get pods -n gateway

# Display service status
print_info "Service Status:"
kubectl get svc -n platform-system
echo ""
kubectl get svc -n gateway

print_info "Deployment completed successfully!"
print_info "Next steps:"
echo "  1. Verify all services are healthy"
echo "  2. Create initial tenant: curl -X POST http://$GATEWAY_IP/api/tenants -H 'Content-Type: application/json' -d '{...}'"
echo "  3. Register devices"
echo "  4. Configure monitoring and alerting"
echo ""
print_info "For troubleshooting, see: docs/TROUBLESHOOTING.md"

exit 0

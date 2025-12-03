#!/bin/bash

# Build and Push Container Images Script
# Builds all service images and pushes them to Azure Container Registry

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if registry is provided
if [ -z "$1" ]; then
    print_error "Usage: $0 <registry-url>"
    print_error "Example: $0 medlogicacr.azurecr.io"
    exit 1
fi

REGISTRY=$1
VERSION=${2:-latest}

print_info "Building and pushing images to $REGISTRY with tag $VERSION"

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$ROOT_DIR"

# Build .NET services
print_info "Building .NET solution..."
dotnet build --configuration Release

# Array of services to build
declare -A SERVICES=(
    ["TenantCatalogService"]="src/TenantCatalogService/Dockerfile"
    ["DeviceRegistryService"]="src/DeviceRegistryService/Dockerfile"
    ["MedLogicService"]="src/MedLogicService/Dockerfile"
    ["AdminUI"]="src/AdminUI/Dockerfile"
)

# Build and push .NET services
for service in "${!SERVICES[@]}"; do
    dockerfile="${SERVICES[$service]}"
    image_name=$(echo "$service" | tr '[:upper:]' '[:lower:]' | sed 's/service$//' | sed 's/-$//')
    full_image="$REGISTRY/$image_name-service:$VERSION"
    
    print_info "Building $service..."
    docker build -f "$dockerfile" -t "$full_image" .
    
    print_info "Pushing $full_image..."
    docker push "$full_image"
    
    print_info "Successfully pushed $full_image"
done

# Build Smart Gateway (NGINX)
print_info "Building Smart Gateway..."
docker build -f nginx/Dockerfile -t "$REGISTRY/smart-gateway:$VERSION" nginx/

print_info "Pushing Smart Gateway..."
docker push "$REGISTRY/smart-gateway:$VERSION"

# Build Tenant Operator (Go)
print_info "Building Tenant Operator..."
cd tenant-operator

# Build Go binary
CGO_ENABLED=0 GOOS=linux GOARCH=amd64 go build -a -o bin/manager main.go

# Build Docker image
docker build -t "$REGISTRY/tenant-operator:$VERSION" .

print_info "Pushing Tenant Operator..."
docker push "$REGISTRY/tenant-operator:$VERSION"

cd "$ROOT_DIR"

print_info "All images built and pushed successfully!"
print_info "Images:"
for service in "${!SERVICES[@]}"; do
    image_name=$(echo "$service" | tr '[:upper:]' '[:lower:]' | sed 's/service$//' | sed 's/-$//')
    echo "  - $REGISTRY/$image_name-service:$VERSION"
done
echo "  - $REGISTRY/smart-gateway:$VERSION"
echo "  - $REGISTRY/tenant-operator:$VERSION"

exit 0

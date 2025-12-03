#!/bin/bash
# Script to configure Azure AD App Registrations and Federated Identity Credentials for AKS Workload Identity

set -e

# Configuration variables
RESOURCE_GROUP="${RESOURCE_GROUP:-medlogic-rg}"
AKS_CLUSTER_NAME="${AKS_CLUSTER_NAME:-medlogic-aks}"
LOCATION="${LOCATION:-eastus}"
KEY_VAULT_NAME="${KEY_VAULT_NAME:-medlogic-kv}"
SQL_SERVER_NAME="${SQL_SERVER_NAME:-medlogic-sql}"

echo "=== AKS Workload Identity Setup ==="
echo "Resource Group: $RESOURCE_GROUP"
echo "AKS Cluster: $AKS_CLUSTER_NAME"
echo "Location: $LOCATION"
echo ""

# Get AKS OIDC Issuer URL
echo "Getting AKS OIDC Issuer URL..."
OIDC_ISSUER=$(az aks show --resource-group $RESOURCE_GROUP --name $AKS_CLUSTER_NAME --query "oidcIssuerProfile.issuerUrl" -o tsv)
echo "OIDC Issuer: $OIDC_ISSUER"

# Get Azure Tenant ID
AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Azure Tenant ID: $AZURE_TENANT_ID"

# Function to create Azure AD App Registration and Federated Credential
create_workload_identity() {
    local APP_NAME=$1
    local NAMESPACE=$2
    local SERVICE_ACCOUNT=$3
    
    echo ""
    echo "=== Creating Workload Identity for $APP_NAME ==="
    
    # Create Azure AD App Registration
    echo "Creating Azure AD App Registration: $APP_NAME..."
    APP_ID=$(az ad app create --display-name $APP_NAME --query appId -o tsv)
    echo "App ID: $APP_ID"
    
    # Create Service Principal
    echo "Creating Service Principal..."
    SP_ID=$(az ad sp create --id $APP_ID --query id -o tsv)
    echo "Service Principal ID: $SP_ID"
    
    # Create Federated Identity Credential
    echo "Creating Federated Identity Credential..."
    az ad app federated-credential create \
        --id $APP_ID \
        --parameters "{
            \"name\": \"${APP_NAME}-federated-credential\",
            \"issuer\": \"${OIDC_ISSUER}\",
            \"subject\": \"system:serviceaccount:${NAMESPACE}:${SERVICE_ACCOUNT}\",
            \"audiences\": [\"api://AzureADTokenExchange\"]
        }"
    
    echo "Workload Identity created successfully for $APP_NAME"
    echo "Client ID: $APP_ID"
    
    # Return the App ID
    echo $APP_ID
}

# Create Workload Identities for each service
echo ""
echo "=== Creating Workload Identities ==="

TENANT_CATALOG_CLIENT_ID=$(create_workload_identity "tenant-catalog-workload-id" "platform-system" "tenant-catalog-sa")
DEVICE_REGISTRY_CLIENT_ID=$(create_workload_identity "device-registry-workload-id" "platform-system" "device-registry-sa")
MEDLOGIC_SERVICE_CLIENT_ID=$(create_workload_identity "medlogic-service-workload-id" "platform-system" "medlogic-service-sa")

# Grant permissions to Key Vault
echo ""
echo "=== Granting Key Vault Permissions ==="

# Get Key Vault resource ID
KEY_VAULT_ID=$(az keyvault show --name $KEY_VAULT_NAME --resource-group $RESOURCE_GROUP --query id -o tsv)

# Grant Key Vault Secrets User role to each service principal
for CLIENT_ID in $TENANT_CATALOG_CLIENT_ID $DEVICE_REGISTRY_CLIENT_ID $MEDLOGIC_SERVICE_CLIENT_ID; do
    SP_ID=$(az ad sp show --id $CLIENT_ID --query id -o tsv)
    echo "Granting Key Vault Secrets User role to $CLIENT_ID..."
    az role assignment create \
        --role "Key Vault Secrets User" \
        --assignee-object-id $SP_ID \
        --assignee-principal-type ServicePrincipal \
        --scope $KEY_VAULT_ID
done

# Grant permissions to SQL Server
echo ""
echo "=== Granting SQL Server Permissions ==="

# Get SQL Server resource ID
SQL_SERVER_ID=$(az sql server show --name $SQL_SERVER_NAME --resource-group $RESOURCE_GROUP --query id -o tsv)

# Grant SQL DB Contributor role to each service principal
for CLIENT_ID in $TENANT_CATALOG_CLIENT_ID $DEVICE_REGISTRY_CLIENT_ID $MEDLOGIC_SERVICE_CLIENT_ID; do
    SP_ID=$(az ad sp show --id $CLIENT_ID --query id -o tsv)
    echo "Granting SQL DB Contributor role to $CLIENT_ID..."
    az role assignment create \
        --role "SQL DB Contributor" \
        --assignee-object-id $SP_ID \
        --assignee-principal-type ServicePrincipal \
        --scope $SQL_SERVER_ID
done

# Create environment file with Client IDs
echo ""
echo "=== Creating Environment Configuration ==="
cat > workload-identity-env.sh <<EOF
# Azure Workload Identity Environment Variables
export AZURE_TENANT_ID="$AZURE_TENANT_ID"
export TENANT_CATALOG_CLIENT_ID="$TENANT_CATALOG_CLIENT_ID"
export DEVICE_REGISTRY_CLIENT_ID="$DEVICE_REGISTRY_CLIENT_ID"
export MEDLOGIC_SERVICE_CLIENT_ID="$MEDLOGIC_SERVICE_CLIENT_ID"
EOF

echo "Environment configuration saved to workload-identity-env.sh"
echo ""
echo "=== Setup Complete ==="
echo "To apply the Kubernetes configuration, run:"
echo "  source workload-identity-env.sh"
echo "  envsubst < k8s/workload-identity-setup.yaml | kubectl apply -f -"
echo "  kubectl apply -f k8s/tenant-catalog-deployment.yaml"
echo "  kubectl apply -f k8s/device-registry-deployment.yaml"

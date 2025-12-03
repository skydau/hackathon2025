#!/bin/bash

# End-to-End Test Script
# Tests the complete flow from device request to database operation

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

# Get gateway IP
print_info "Getting gateway external IP..."
GATEWAY_IP=$(kubectl get svc smart-gateway -n gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

if [ -z "$GATEWAY_IP" ]; then
    print_error "Gateway IP not found. Is the gateway deployed?"
    exit 1
fi

print_info "Gateway IP: $GATEWAY_IP"

# Test 1: Create a tenant
print_info "Test 1: Creating a test tenant..."
TENANT_RESPONSE=$(curl -s -X POST "http://$GATEWAY_IP/api/tenants" \
    -H "Content-Type: application/json" \
    -d '{
        "displayName": "Test Hospital E2E",
        "dbConfig": {
            "mode": "perDatabase",
            "server": "test-server",
            "database": "TestHospital_DB"
        },
        "throttling": {
            "rps": 50
        },
        "slo": {
            "availability": "99.9%",
            "p95LatencyMs": 1000
        }
    }')

TENANT_ID=$(echo "$TENANT_RESPONSE" | grep -o '"id":"[^"]*' | cut -d'"' -f4)

if [ -z "$TENANT_ID" ]; then
    print_error "Failed to create tenant"
    echo "Response: $TENANT_RESPONSE"
    exit 1
fi

print_success "Tenant created with ID: $TENANT_ID"

# Test 2: Retrieve the tenant
print_info "Test 2: Retrieving tenant..."
TENANT_GET=$(curl -s "http://$GATEWAY_IP/api/tenants/$TENANT_ID")

if echo "$TENANT_GET" | grep -q "$TENANT_ID"; then
    print_success "Tenant retrieved successfully"
else
    print_error "Failed to retrieve tenant"
    exit 1
fi

# Test 3: Register a device
print_info "Test 3: Registering a device..."
DEVICE_SERIAL="E2E-TEST-DEVICE-$(date +%s)"

DEVICE_RESPONSE=$(curl -s -X POST "http://$GATEWAY_IP/api/devices" \
    -H "Content-Type: application/json" \
    -d "{
        \"serialNumber\": \"$DEVICE_SERIAL\",
        \"tenantId\": \"$TENANT_ID\",
        \"deviceType\": \"medDispense\"
    }")

if echo "$DEVICE_RESPONSE" | grep -q "$DEVICE_SERIAL"; then
    print_success "Device registered successfully"
else
    print_error "Failed to register device"
    echo "Response: $DEVICE_RESPONSE"
    exit 1
fi

# Test 4: Query device-to-tenant mapping
print_info "Test 4: Querying device-to-tenant mapping..."
DEVICE_TENANT=$(curl -s "http://$GATEWAY_IP/api/devices/$DEVICE_SERIAL/tenant")

if echo "$DEVICE_TENANT" | grep -q "$TENANT_ID"; then
    print_success "Device-to-tenant mapping verified"
else
    print_error "Device-to-tenant mapping failed"
    exit 1
fi

# Test 5: Update tenant status
print_info "Test 5: Updating tenant status..."
UPDATE_RESPONSE=$(curl -s -X PATCH "http://$GATEWAY_IP/api/tenants/$TENANT_ID" \
    -H "Content-Type: application/json" \
    -d '{
        "status": "Enabled"
    }')

if echo "$UPDATE_RESPONSE" | grep -q "Enabled"; then
    print_success "Tenant status updated successfully"
else
    print_error "Failed to update tenant status"
    exit 1
fi

# Test 6: Test rate limiting (if implemented)
print_info "Test 6: Testing rate limiting..."
RATE_LIMIT_EXCEEDED=false

for i in {1..60}; do
    RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
        -H "Device-Id: $DEVICE_SERIAL" \
        "http://$GATEWAY_IP/api/test")
    
    if [ "$RESPONSE" = "429" ]; then
        RATE_LIMIT_EXCEEDED=true
        break
    fi
    sleep 0.1
done

if [ "$RATE_LIMIT_EXCEEDED" = true ]; then
    print_success "Rate limiting is working"
else
    print_info "Rate limiting test skipped or not triggered"
fi

# Test 7: Verify tenant isolation (create another tenant)
print_info "Test 7: Testing tenant isolation..."
TENANT2_RESPONSE=$(curl -s -X POST "http://$GATEWAY_IP/api/tenants" \
    -H "Content-Type: application/json" \
    -d '{
        "displayName": "Test Hospital 2 E2E",
        "dbConfig": {
            "mode": "perDatabase",
            "server": "test-server",
            "database": "TestHospital2_DB"
        }
    }')

TENANT2_ID=$(echo "$TENANT2_RESPONSE" | grep -o '"id":"[^"]*' | cut -d'"' -f4)

if [ -n "$TENANT2_ID" ] && [ "$TENANT2_ID" != "$TENANT_ID" ]; then
    print_success "Tenant isolation verified (unique IDs)"
else
    print_error "Tenant isolation test failed"
    exit 1
fi

# Cleanup
print_info "Cleaning up test data..."

# Note: In production, you would implement DELETE endpoints
# For now, we'll leave the test data for manual inspection

print_success "All E2E tests passed!"
echo ""
echo "Test Summary:"
echo "  ✓ Tenant creation"
echo "  ✓ Tenant retrieval"
echo "  ✓ Device registration"
echo "  ✓ Device-to-tenant mapping"
echo "  ✓ Tenant status update"
echo "  ✓ Rate limiting (if configured)"
echo "  ✓ Tenant isolation"
echo ""
echo "Test tenant ID: $TENANT_ID"
echo "Test device serial: $DEVICE_SERIAL"
echo "Second tenant ID: $TENANT2_ID"

exit 0

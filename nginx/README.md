# Smart Gateway (NGINX)

## Overview

The Smart Gateway is an NGINX-based intelligent gateway that serves as the entry point for all device requests in the multi-tenant medical platform. It performs the following key functions:

1. **Device Identification**: Extracts device ID from incoming requests
2. **Tenant Resolution**: Queries the Device Registry to map device ID to tenant ID
3. **Tenant Injection**: Injects `X-Tenant-Id` header into requests
4. **Rate Limiting**: Applies tenant-level rate limiting
5. **Request Routing**: Forwards requests to backend services

## Architecture

```
Device Request → Smart Gateway → Device Registry (lookup tenant)
                      ↓
                 Inject X-Tenant-Id
                      ↓
                 Rate Limiting
                      ↓
                 Backend Service
```

## Components

### 1. nginx.conf
Main NGINX configuration file that:
- Defines upstream services (Device Registry, Tenant Catalog, Backend)
- Configures Lua script execution
- Sets up shared memory for rate limiting
- Defines routing rules

### 2. tenant_router.lua
Lua script that:
- Extracts `Device-Id` header from requests
- Calls Device Registry API to get tenant ID
- Injects `X-Tenant-Id` header
- Handles errors (missing device, unauthorized, service unavailable)

### 3. rate_limiter.lua
Lua script that:
- Implements token bucket algorithm for rate limiting
- Fetches tenant-specific rate limits from Tenant Catalog
- Caches rate limit configurations
- Returns 429 when rate limit exceeded
- Adds rate limit headers to responses

## Request Flow

1. Device sends request with `Device-Id` header
2. `tenant_router.lua` extracts device ID
3. Gateway queries Device Registry: `GET /api/devices/{deviceId}/tenant`
4. Device Registry returns `{ "tenantId": "hospital-a" }`
5. Gateway injects `X-Tenant-Id: hospital-a` header
6. `rate_limiter.lua` checks rate limit for tenant
7. If within limit, request forwarded to backend
8. If exceeded, returns 429 Too Many Requests

## Configuration

### Environment Variables

- `DEVICE_REGISTRY_URL`: URL of Device Registry service (default: `http://device-registry-service.default.svc.cluster.local:8080`)
- `TENANT_CATALOG_URL`: URL of Tenant Catalog service (default: `http://tenant-catalog-service.default.svc.cluster.local:8080`)

### Rate Limiting

- **Default Rate**: 50 requests/second per tenant
- **Burst Size**: 100 requests
- **Config Refresh**: Every 60 seconds
- **Cache Duration**: 2 minutes

Tenant-specific rate limits are fetched from Tenant Catalog and cached.

## Building

```bash
cd nginx
docker build -t smart-gateway:latest .
```

## Running Locally

```bash
docker run -p 8080:80 \
  -e DEVICE_REGISTRY_URL=http://host.docker.internal:5001 \
  -e TENANT_CATALOG_URL=http://host.docker.internal:5002 \
  smart-gateway:latest
```

## Deploying to Kubernetes

```bash
kubectl apply -f k8s/smart-gateway-deployment.yaml
```

This creates:
- Namespace: `gateway`
- Deployment: 3 replicas with pod anti-affinity
- Service: LoadBalancer for external access
- Service: ClusterIP for internal access

## Testing

### Test Device Request

```bash
curl -H "Device-Id: DEVICE001" http://localhost:8080/api/transactions
```

Expected flow:
1. Gateway extracts `DEVICE001`
2. Queries Device Registry
3. Gets tenant ID
4. Injects `X-Tenant-Id` header
5. Forwards to backend

### Test Rate Limiting

```bash
# Send many requests rapidly
for i in {1..200}; do
  curl -H "Device-Id: DEVICE001" http://localhost:8080/api/test
done
```

Expected: After ~100 requests, should receive 429 responses.

### Test Unauthorized Device

```bash
curl -H "Device-Id: UNKNOWN" http://localhost:8080/api/test
```

Expected: 401 Unauthorized

### Test Missing Device Header

```bash
curl http://localhost:8080/api/test
```

Expected: 400 Bad Request

## Monitoring

### Logs

```bash
# Access logs (includes tenant_id)
kubectl logs -n gateway -l app=smart-gateway -f

# Error logs
kubectl logs -n gateway -l app=smart-gateway -f | grep ERROR
```

### Metrics

Rate limit headers are included in responses:
- `X-RateLimit-Limit`: Maximum requests per second
- `X-RateLimit-Remaining`: Remaining tokens in bucket

### Health Check

```bash
curl http://localhost:8080/health
```

## Error Responses

### 400 Bad Request
```json
{
  "error": {
    "code": "MISSING_DEVICE_ID",
    "message": "Device-Id header is required",
    "timestamp": 1234567890.123
  }
}
```

### 401 Unauthorized
```json
{
  "error": {
    "code": "DEVICE_NOT_AUTHORIZED",
    "message": "Device is not registered or authorized",
    "timestamp": 1234567890.123
  }
}
```

### 429 Too Many Requests
```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests. Please try again later.",
    "tenantId": "hospital-a",
    "limit": 50,
    "timestamp": 1234567890.123
  }
}
```

### 502 Bad Gateway
```json
{
  "error": {
    "code": "UPSTREAM_ERROR",
    "message": "Failed to retrieve device information",
    "timestamp": 1234567890.123
  }
}
```

### 503 Service Unavailable
```json
{
  "error": {
    "code": "SERVICE_UNAVAILABLE",
    "message": "Device Registry service is unavailable",
    "timestamp": 1234567890.123
  }
}
```

## Performance Tuning

### Worker Processes
Adjust based on CPU cores:
```nginx
worker_processes auto;  # Uses all available cores
```

### Connection Pooling
Upstream keepalive connections:
```nginx
upstream device_registry {
    server device-registry-service.default.svc.cluster.local:8080;
    keepalive 32;  # Adjust based on load
}
```

### Shared Memory
Rate limiting dictionary size:
```nginx
lua_shared_dict tenant_rate_limit 20m;  # Increase for more tenants
```

## Security Considerations

1. **Device Authentication**: Only registered devices can access the system
2. **Rate Limiting**: Prevents DoS attacks at tenant level
3. **Tenant Isolation**: Each tenant's rate limit is independent
4. **Error Handling**: Detailed errors logged but sanitized responses to clients
5. **Timeouts**: Prevents hanging connections (5s for Device Registry, 2s for Tenant Catalog)

## Troubleshooting

### Gateway returns 503
- Check Device Registry service is running
- Verify DNS resolution: `kubectl exec -n gateway <pod> -- nslookup device-registry-service.default.svc.cluster.local`

### Rate limiting not working
- Check shared dictionary size: `lua_shared_dict tenant_rate_limit 20m`
- Verify Tenant Catalog is accessible
- Check logs for config fetch errors

### High latency
- Increase keepalive connections
- Check Device Registry response times
- Consider caching device-to-tenant mappings

## Requirements Validation

This implementation satisfies the following requirements:

- **4.1**: Extracts device ID from request header
- **4.2**: Queries Device Registry for tenant ID
- **4.3**: Injects X-Tenant-Id header
- **4.4**: Forwards request to backend service
- **4.5**: Returns 401 for unauthorized devices
- **5.1**: Applies tenant-level rate limiting
- **5.2**: Returns 429 when rate exceeded
- **5.3**: Tenant isolation in rate limiting
- **5.4**: Uses tenant-specific rate limits from Tenant Catalog
- **5.5**: Applies default rate limit when no tenant ID

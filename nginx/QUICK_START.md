# Smart Gateway Quick Start Guide

## Prerequisites

1. Docker installed
2. Kubernetes cluster (or kind/minikube for local testing)
3. Device Registry Service deployed
4. Tenant Catalog Service deployed

## Quick Start

### 1. Build the Gateway Image

```bash
cd nginx
docker build -t smart-gateway:latest .
```

### 2. Deploy to Kubernetes

```bash
kubectl apply -f ../k8s/smart-gateway-deployment.yaml
```

### 3. Verify Deployment

```bash
# Check pods are running
kubectl get pods -n gateway

# Check service
kubectl get svc -n gateway

# View logs
kubectl logs -n gateway -l app=smart-gateway -f
```

### 4. Test the Gateway

#### Register a Device

```bash
# Register device in Device Registry
curl -X POST http://device-registry:8080/api/devices \
  -H "Content-Type: application/json" \
  -d '{
    "serialNumber": "DEVICE001",
    "tenantId": "hospital-a",
    "deviceType": "medDispense"
  }'
```

#### Send Request Through Gateway

```bash
# Get gateway external IP
GATEWAY_IP=$(kubectl get svc -n gateway smart-gateway -o jsonpath='{.status.loadBalancer.ingress[0].ip}')

# Send request with Device-Id header
curl -H "Device-Id: DEVICE001" \
  http://$GATEWAY_IP/api/test
```

Expected response: 200 OK with X-Tenant-Id injected

#### Test Rate Limiting

```bash
# Send many requests rapidly
for i in {1..200}; do
  curl -H "Device-Id: DEVICE001" \
    http://$GATEWAY_IP/api/test \
    -w "\nStatus: %{http_code}\n"
done
```

Expected: First ~100 requests succeed, then 429 responses

## Configuration

### Adjust Rate Limits

Update tenant configuration in Tenant Catalog:

```bash
curl -X PATCH http://tenant-catalog:8080/api/tenants/hospital-a \
  -H "Content-Type: application/json" \
  -d '{
    "throttling": {
      "rps": 100
    }
  }'
```

Gateway will pick up new limit within 60 seconds.

### Scale Gateway

```bash
# Scale to 5 replicas
kubectl scale deployment smart-gateway -n gateway --replicas=5
```

## Monitoring

### View Access Logs

```bash
kubectl logs -n gateway -l app=smart-gateway | grep "tenant_id="
```

### Check Health

```bash
curl http://$GATEWAY_IP/health
```

### Monitor Rate Limiting

Watch for 429 responses in logs:

```bash
kubectl logs -n gateway -l app=smart-gateway | grep "429"
```

## Troubleshooting

### Gateway Returns 503

**Problem**: Device Registry unavailable

**Solution**:
```bash
# Check Device Registry
kubectl get pods -n default -l app=device-registry

# Check DNS resolution
kubectl exec -n gateway <gateway-pod> -- nslookup device-registry-service.default.svc.cluster.local
```

### Gateway Returns 401

**Problem**: Device not registered

**Solution**:
```bash
# Verify device exists
curl http://device-registry:8080/api/devices/DEVICE001
```

### Rate Limiting Not Working

**Problem**: Tenant Catalog unreachable

**Solution**:
```bash
# Check Tenant Catalog
kubectl get pods -n default -l app=tenant-catalog

# Check gateway logs for errors
kubectl logs -n gateway -l app=smart-gateway | grep ERROR
```

## Local Development

### Run with Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'
services:
  gateway:
    build: ./nginx
    ports:
      - "8080:80"
    environment:
      - DEVICE_REGISTRY_URL=http://device-registry:8080
      - TENANT_CATALOG_URL=http://tenant-catalog:8080
    depends_on:
      - device-registry
      - tenant-catalog
  
  device-registry:
    image: device-registry:latest
    ports:
      - "5001:8080"
  
  tenant-catalog:
    image: tenant-catalog:latest
    ports:
      - "5002:8080"
```

Run:
```bash
docker-compose up
```

## Performance Tuning

### Increase Worker Processes

Edit `nginx.conf`:
```nginx
worker_processes 4;  # Match CPU cores
```

### Increase Shared Memory

Edit `nginx.conf`:
```nginx
lua_shared_dict tenant_rate_limit 50m;  # Increase for more tenants
```

### Adjust Keepalive Connections

Edit `nginx.conf`:
```nginx
upstream device_registry {
    server device-registry-service.default.svc.cluster.local:8080;
    keepalive 64;  # Increase for higher load
}
```

## Security Best Practices

1. **Use TLS**: Configure SSL/TLS certificates for production
2. **Network Policies**: Restrict gateway access to specific namespaces
3. **Resource Limits**: Set appropriate CPU/memory limits
4. **Rate Limiting**: Tune rate limits based on tenant SLAs
5. **Monitoring**: Set up alerts for high error rates

## Next Steps

1. Configure TLS certificates
2. Set up Prometheus metrics export
3. Configure Grafana dashboards
4. Implement request logging to Loki
5. Set up alerting rules

## Support

For issues or questions:
1. Check logs: `kubectl logs -n gateway -l app=smart-gateway`
2. Review README.md for detailed documentation
3. Check IMPLEMENTATION_SUMMARY.md for architecture details

# SLO Monitoring and Alerting Guide

## Overview

This guide describes the Service Level Objective (SLO) monitoring and alerting system for the multi-tenant medical platform. The system enables per-tenant SLO configuration, automated monitoring, and alerting when SLOs are violated.

## Architecture

### Components

1. **Tenant Catalog Service**: Stores SLO configuration for each tenant
2. **Prometheus**: Collects metrics and evaluates alert rules
3. **Alert Manager**: Routes and manages alerts
4. **Grafana**: Visualizes SLO metrics and achievement rates

### SLO Configuration

Each tenant can define two types of SLOs:

- **Availability**: Target success rate (e.g., "99.9%")
- **P95 Latency**: 95th percentile latency target in milliseconds (e.g., 1000ms)

## Configuration

### Tenant SLO Configuration

SLOs are configured when creating or updating a tenant:

```json
{
  "displayName": "Hospital A",
  "dbConfig": { ... },
  "slo": {
    "availability": "99.9%",
    "p95LatencyMs": 1000
  }
}
```

### Prometheus Alert Rules

The Prometheus alert rules are defined in `prometheus-slo-rules.yaml`:

#### Error Rate Alert

Triggers when a tenant's error rate exceeds 5% for 5 minutes:

```yaml
- alert: TenantErrorRateExceeded
  expr: |
    (
      sum(rate(http_requests_total{status=~"5.."}[5m])) by (tenant_id)
      /
      sum(rate(http_requests_total[5m])) by (tenant_id)
    ) * 100 > 5
  for: 5m
  labels:
    severity: warning
    slo_type: error_rate
  annotations:
    summary: "Tenant {{ $labels.tenant_id }} error rate exceeded SLO"
    description: "Tenant {{ $labels.tenant_id }} has error rate of {{ $value | humanizePercentage }}"
    tenant_id: "{{ $labels.tenant_id }}"
    violation_details: "Error rate: {{ $value | humanizePercentage }}, Threshold: 5%"
```

#### P95 Latency Alert

Triggers when a tenant's P95 latency exceeds their configured threshold for 5 minutes:

```yaml
- alert: TenantP95LatencyExceeded
  expr: |
    histogram_quantile(0.95, 
      sum(rate(http_request_duration_seconds_bucket[5m])) by (tenant_id, le)
    ) * 1000 > on(tenant_id) group_left
    tenant_slo_p95_latency_ms
  for: 5m
  labels:
    severity: warning
    slo_type: latency
  annotations:
    summary: "Tenant {{ $labels.tenant_id }} P95 latency exceeded SLO"
    description: "Tenant {{ $labels.tenant_id }} has P95 latency of {{ $value | humanize }}ms"
    tenant_id: "{{ $labels.tenant_id }}"
    violation_details: "P95 latency: {{ $value | humanize }}ms"
```

## Deployment

### 1. Deploy Prometheus Rules

```bash
kubectl apply -f k8s/prometheus-slo-rules.yaml
```

### 2. Configure Prometheus

Add the rules file to your Prometheus configuration:

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: prometheus-config
  namespace: observability
data:
  prometheus.yml: |
    global:
      scrape_interval: 15s
      evaluation_interval: 15s
    
    rule_files:
      - /etc/prometheus/rules/slo-rules.yml
    
    scrape_configs:
      - job_name: 'tenant-services'
        kubernetes_sd_configs:
          - role: pod
        relabel_configs:
          - source_labels: [__meta_kubernetes_pod_label_tenant_id]
            target_label: tenant_id
```

### 3. Configure Alert Manager

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: alertmanager-config
  namespace: observability
data:
  alertmanager.yml: |
    global:
      resolve_timeout: 5m
    
    route:
      group_by: ['tenant_id', 'slo_type']
      group_wait: 10s
      group_interval: 10s
      repeat_interval: 12h
      receiver: 'tenant-slo-alerts'
    
    receivers:
      - name: 'tenant-slo-alerts'
        webhook_configs:
          - url: 'http://alert-webhook:8080/alerts'
            send_resolved: true
```

## Metrics Requirements

### Required Metrics

Your services must expose the following metrics with `tenant_id` labels:

1. **HTTP Request Counter**:
   ```
   http_requests_total{tenant_id="hospital-a", status="200"}
   http_requests_total{tenant_id="hospital-a", status="500"}
   ```

2. **HTTP Request Duration Histogram**:
   ```
   http_request_duration_seconds_bucket{tenant_id="hospital-a", le="0.1"}
   http_request_duration_seconds_bucket{tenant_id="hospital-a", le="0.5"}
   http_request_duration_seconds_bucket{tenant_id="hospital-a", le="1.0"}
   ```

### Example: ASP.NET Core Metrics

```csharp
using Prometheus;

public class TenantMetricsMiddleware
{
    private static readonly Counter RequestCounter = Metrics
        .CreateCounter("http_requests_total", "Total HTTP requests",
            new CounterConfiguration
            {
                LabelNames = new[] { "tenant_id", "status" }
            });

    private static readonly Histogram RequestDuration = Metrics
        .CreateHistogram("http_request_duration_seconds", "HTTP request duration",
            new HistogramConfiguration
            {
                LabelNames = new[] { "tenant_id" },
                Buckets = Histogram.ExponentialBuckets(0.01, 2, 10)
            });

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var tenantId = context.Items["TenantId"] as string ?? "unknown";
        
        using (RequestDuration.WithLabels(tenantId).NewTimer())
        {
            await next(context);
        }
        
        RequestCounter.WithLabels(tenantId, context.Response.StatusCode.ToString()).Inc();
    }
}
```

## Monitoring and Visualization

### Grafana Dashboards

Create dashboards to visualize:

1. **SLO Achievement Rate by Tenant**
   ```promql
   tenant_slo_achievement_rate
   ```

2. **Error Rate by Tenant**
   ```promql
   (
     sum(rate(http_requests_total{status=~"5.."}[5m])) by (tenant_id)
     /
     sum(rate(http_requests_total[5m])) by (tenant_id)
   ) * 100
   ```

3. **P95 Latency by Tenant**
   ```promql
   histogram_quantile(0.95, 
     sum(rate(http_request_duration_seconds_bucket[5m])) by (tenant_id, le)
   ) * 1000
   ```

### Example Dashboard JSON

```json
{
  "dashboard": {
    "title": "Tenant SLO Dashboard",
    "panels": [
      {
        "title": "Error Rate by Tenant",
        "targets": [
          {
            "expr": "(sum(rate(http_requests_total{status=~\"5..\"}[5m])) by (tenant_id) / sum(rate(http_requests_total[5m])) by (tenant_id)) * 100"
          }
        ]
      },
      {
        "title": "P95 Latency by Tenant",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (tenant_id, le)) * 1000"
          }
        ]
      }
    ]
  }
}
```

## Alert Handling

### Alert Payload

When an alert fires, it includes:

```json
{
  "status": "firing",
  "labels": {
    "alertname": "TenantErrorRateExceeded",
    "tenant_id": "hospital-a",
    "severity": "warning",
    "slo_type": "error_rate"
  },
  "annotations": {
    "summary": "Tenant hospital-a error rate exceeded SLO",
    "description": "Tenant hospital-a has error rate of 7.5% which exceeds the 5% threshold",
    "tenant_id": "hospital-a",
    "violation_details": "Error rate: 7.5%, Threshold: 5%"
  }
}
```

### Integration with Incident Management

Forward alerts to your incident management system:

```yaml
receivers:
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: '<your-service-key>'
        description: '{{ .Annotations.summary }}'
        details:
          tenant_id: '{{ .Labels.tenant_id }}'
          violation: '{{ .Annotations.violation_details }}'
```

## Testing

### Property-Based Tests

The system includes property-based tests that verify:

1. **SLO Configuration Storage** (Property 55)
   - Validates that SLO configuration is correctly stored and retrieved

2. **SLO Achievement Rate Calculation** (Property 56)
   - Validates that SLO achievement is calculated correctly based on error rate and latency

### Integration Tests

Integration tests verify:

1. **Error Rate Alerting** (Requirement 12.3)
   - High error rates trigger alerts
   - Low error rates do not trigger alerts
   - Alerts include tenant ID and violation details

2. **Latency Alerting** (Requirement 12.4)
   - High latency triggers alerts
   - Low latency does not trigger alerts
   - Alerts include tenant ID and violation details

Run tests:

```bash
# Property tests
dotnet test tests/TenantCatalogService.Tests --filter "FullyQualifiedName~SLOPropertyTests"

# Integration tests
dotnet test tests/SLOMonitoring.Tests
```

## Troubleshooting

### Alerts Not Firing

1. **Check Prometheus targets**:
   ```bash
   kubectl port-forward -n observability svc/prometheus 9090:9090
   # Visit http://localhost:9090/targets
   ```

2. **Verify metrics are being collected**:
   ```promql
   http_requests_total{tenant_id="hospital-a"}
   ```

3. **Check alert rules**:
   ```bash
   # Visit http://localhost:9090/alerts
   ```

### Missing Tenant Labels

Ensure all services include the `tenant_id` label in their metrics:

```csharp
// Add tenant context middleware
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
    {
        context.Items["TenantId"] = tenantId.ToString();
    }
    await next();
});
```

### High Alert Volume

If you're receiving too many alerts:

1. Adjust the `for` duration in alert rules (default: 5m)
2. Increase thresholds if they're too strict
3. Implement alert grouping and deduplication in Alert Manager

## Best Practices

1. **Set Realistic SLOs**: Start with achievable targets and tighten over time
2. **Monitor SLO Burn Rate**: Track how quickly you're consuming your error budget
3. **Regular Review**: Review and adjust SLOs quarterly based on actual performance
4. **Alert Fatigue**: Ensure alerts are actionable and not too noisy
5. **Documentation**: Keep runbooks for common SLO violations

## References

- [Prometheus Alerting](https://prometheus.io/docs/alerting/latest/overview/)
- [SLO Best Practices](https://sre.google/workbook/implementing-slos/)
- [Grafana Dashboards](https://grafana.com/docs/grafana/latest/dashboards/)

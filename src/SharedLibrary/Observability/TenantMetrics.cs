using System.Diagnostics.Metrics;

namespace SharedLibrary.Observability;

/// <summary>
/// Provides tenant-aware metrics collection using System.Diagnostics.Metrics
/// </summary>
public class TenantMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errorCounter;

    public TenantMetrics(string serviceName)
    {
        _meter = new Meter(serviceName, "1.0.0");
        
        _requestCounter = _meter.CreateCounter<long>(
            "http_requests_total",
            description: "Total number of HTTP requests");
        
        _requestDuration = _meter.CreateHistogram<double>(
            "http_request_duration_seconds",
            unit: "s",
            description: "HTTP request duration in seconds");
        
        _errorCounter = _meter.CreateCounter<long>(
            "http_errors_total",
            description: "Total number of HTTP errors");
    }

    public void RecordRequest(string tenantId, string method, string path)
    {
        _requestCounter.Add(1, 
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("path", path));
    }

    public void RecordRequestDuration(string tenantId, string method, string path, double durationSeconds)
    {
        _requestDuration.Record(durationSeconds,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("path", path));
    }

    public void RecordError(string tenantId, string method, string path, int statusCode)
    {
        _errorCounter.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("method", method),
            new KeyValuePair<string, object?>("path", path),
            new KeyValuePair<string, object?>("status_code", statusCode));
    }
}

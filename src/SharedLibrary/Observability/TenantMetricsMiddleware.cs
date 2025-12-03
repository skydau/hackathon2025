using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace SharedLibrary.Observability;

/// <summary>
/// Middleware that records tenant-aware metrics for HTTP requests
/// </summary>
public class TenantMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TenantMetrics _metrics;

    public TenantMetricsMiddleware(RequestDelegate next, TenantMetrics metrics)
    {
        _next = next;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
            
            stopwatch.Stop();
            
            var tenantId = context.Items["TenantId"] as string ?? "unknown";
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "/";
            var durationSeconds = stopwatch.Elapsed.TotalSeconds;

            // Record request metrics
            _metrics.RecordRequest(tenantId, method, path);
            _metrics.RecordRequestDuration(tenantId, method, path, durationSeconds);

            // Record errors for 4xx and 5xx status codes
            if (context.Response.StatusCode >= 400)
            {
                _metrics.RecordError(tenantId, method, path, context.Response.StatusCode);
            }
        }
        catch (Exception)
        {
            stopwatch.Stop();
            
            var tenantId = context.Items["TenantId"] as string ?? "unknown";
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "/";
            
            _metrics.RecordError(tenantId, method, path, 500);
            
            throw;
        }
    }
}

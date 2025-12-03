using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SharedLibrary.Observability;

/// <summary>
/// Middleware that extracts tenant ID from request headers and adds it to logging context
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantIdHeader = "X-Tenant-Id";

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string? tenantId = null;
        
        // Extract tenant ID from header
        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdValues))
        {
            tenantId = tenantIdValues.FirstOrDefault();
        }

        // If no tenant ID in header, use "unknown" for system requests
        tenantId ??= "unknown";

        // Store in HttpContext for access by other components
        context.Items["TenantId"] = tenantId;

        // Push tenant ID to Serilog LogContext
        using (LogContext.PushProperty("tenant_id", tenantId))
        {
            await _next(context);
        }
    }
}

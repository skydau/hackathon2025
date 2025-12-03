using Microsoft.AspNetCore.Http;
using TenantDBRouter.Services;

namespace TenantDBRouter.Middleware;

public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private const string TenantIdHeader = "X-Tenant-Id";

    public TenantContextMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantContextAccessor)
    {
        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var tenantId) && tenantId.Count > 0)
        {
            var extractedId = tenantId[0];
            if (!string.IsNullOrEmpty(extractedId))
            {
                tenantContextAccessor.TenantId = extractedId;
            }
        }

        await _next(context);
    }
}

using Microsoft.AspNetCore.Builder;
using TenantDBRouter.Middleware;

namespace TenantDBRouter.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantContextMiddleware>();
    }
}

using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Moq;
using TenantDBRouter.Middleware;
using TenantDBRouter.Services;

namespace TenantDBRouter.Tests;

public class TenantContextMiddlewarePropertyTests
{
    // **Feature: multi-tenant-medical-platform, Property 26: DB Router提取租户ID**
    // **Validates: Requirements 6.1**
    [Property(MaxTest = 100)]
    public Property ExtractsTenantIdFromHeader()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString tenantIdWrapper) =>
            {
                // Arrange
                var tenantId = tenantIdWrapper.Get;
                var context = new DefaultHttpContext();
                context.Request.Headers["X-Tenant-Id"] = tenantId;

                var tenantContextAccessor = new TenantContextAccessor();
                var nextCalled = false;
                string? capturedTenantId = null;
                RequestDelegate next = (ctx) =>
                {
                    nextCalled = true;
                    capturedTenantId = tenantContextAccessor.TenantId;
                    return Task.CompletedTask;
                };

                var middleware = new TenantContextMiddleware(next);

                // Act
                middleware.InvokeAsync(context, tenantContextAccessor).Wait();

                // Assert
                return nextCalled && capturedTenantId == tenantId;
            }
        );
    }

    [Property(MaxTest = 100)]
    public Property HandlesRequestsWithoutTenantIdHeader()
    {
        return Prop.ForAll(
            Arb.Default.String(),
            (string _) =>
            {
                // Arrange
                var context = new DefaultHttpContext();
                // No X-Tenant-Id header set

                var tenantContextAccessor = new TenantContextAccessor();
                var nextCalled = false;
                string? capturedTenantId = null;
                RequestDelegate next = (ctx) =>
                {
                    nextCalled = true;
                    capturedTenantId = tenantContextAccessor.TenantId;
                    return Task.CompletedTask;
                };

                var middleware = new TenantContextMiddleware(next);

                // Act
                middleware.InvokeAsync(context, tenantContextAccessor).Wait();

                // Assert
                return nextCalled && capturedTenantId == null;
            }
        );
    }
}

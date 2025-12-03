using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using SharedLibrary.Observability;

namespace Observability.Tests;

/// <summary>
/// **Feature: multi-tenant-medical-platform, Property 51: 日志包含租户字段**
/// **Validates: Requirements 11.2**
/// 
/// Property: Logs include tenant field
/// For any system-written log entry, the log should include a tenant_id field
/// </summary>
public class LogsTenantFieldPropertyTests
{
    [Property(MaxTest = 100)]
    public Property AllLogsShouldIncludeTenantIdField()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateLogMessage(),
            (tenantId, logMessage) =>
            {
                // Arrange
                var sink = new InMemorySink();
                
                var logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var context = new DefaultHttpContext();
                context.Request.Headers["X-Tenant-Id"] = tenantId;

                var middleware = new TenantContextMiddleware((ctx) =>
                {
                    // Simulate logging within the request context
                    logger.Information(logMessage);
                    return Task.CompletedTask;
                });

                // Act
                middleware.InvokeAsync(context).Wait();

                // Assert
                var logEvents = sink.LogEvents.ToList();
                var hasLogs = logEvents.Count > 0;
                
                if (!hasLogs)
                {
                    return false.Label("No logs were captured");
                }

                var allLogsHaveTenantId = logEvents.All(logEvent =>
                    logEvent.Properties.ContainsKey("tenant_id") &&
                    logEvent.Properties["tenant_id"].ToString().Trim('"') == tenantId);

                return allLogsHaveTenantId
                    .Label($"All {logEvents.Count} logs should include tenant_id={tenantId}");
            });
    }

    [Property(MaxTest = 100)]
    public Property TenantIdFieldShouldMatchRequestHeader()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateLogMessage(),
            (tenantId, logMessage) =>
            {
                // Arrange
                var sink = new InMemorySink();
                
                var logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var context = new DefaultHttpContext();
                context.Request.Headers["X-Tenant-Id"] = tenantId;

                var middleware = new TenantContextMiddleware((ctx) =>
                {
                    logger.Information(logMessage);
                    return Task.CompletedTask;
                });

                // Act
                middleware.InvokeAsync(context).Wait();

                // Assert
                var logEvents = sink.LogEvents.ToList();
                
                if (logEvents.Count == 0)
                {
                    return false.Label("No logs were captured");
                }

                var capturedTenantIds = logEvents
                    .Where(e => e.Properties.ContainsKey("tenant_id"))
                    .Select(e => e.Properties["tenant_id"].ToString().Trim('"'))
                    .ToList();

                var allMatch = capturedTenantIds.All(id => id == tenantId);
                var hasCaptures = capturedTenantIds.Count > 0;

                return (allMatch && hasCaptures)
                    .Label($"All {capturedTenantIds.Count} log entries should have tenant_id={tenantId}");
            });
    }

    [Property(MaxTest = 100)]
    public Property LogsWithoutTenantHeaderShouldUseUnknown()
    {
        return Prop.ForAll(
            GenerateLogMessage(),
            (logMessage) =>
            {
                // Arrange
                var sink = new InMemorySink();
                
                var logger = new LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .WriteTo.Sink(sink)
                    .CreateLogger();

                var context = new DefaultHttpContext();
                // No X-Tenant-Id header

                var middleware = new TenantContextMiddleware((ctx) =>
                {
                    logger.Information(logMessage);
                    return Task.CompletedTask;
                });

                // Act
                middleware.InvokeAsync(context).Wait();

                // Assert
                var logEvents = sink.LogEvents.ToList();
                
                if (logEvents.Count == 0)
                {
                    return false.Label("No logs were captured");
                }

                var allLogsHaveUnknownTenant = logEvents.All(logEvent =>
                    logEvent.Properties.ContainsKey("tenant_id") &&
                    logEvent.Properties["tenant_id"].ToString().Trim('"') == "unknown");

                return allLogsHaveUnknownTenant
                    .Label($"All {logEvents.Count} logs without tenant header should have tenant_id=unknown");
            });
    }

    // Generators
    private static Arbitrary<string> GenerateValidTenantId()
    {
        return Gen.Elements("tenant-a", "tenant-b", "hospital-123", "clinic-xyz", "org-456")
            .ToArbitrary();
    }

    private static Arbitrary<string> GenerateLogMessage()
    {
        return Gen.Elements(
            "Processing request",
            "Request completed successfully",
            "Error occurred during processing",
            "Database query executed",
            "Cache hit",
            "Cache miss"
        ).ToArbitrary();
    }
}

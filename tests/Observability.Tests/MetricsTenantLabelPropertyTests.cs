using FsCheck;
using FsCheck.Xunit;
using SharedLibrary.Observability;
using System.Diagnostics.Metrics;

namespace Observability.Tests;

/// <summary>
/// **Feature: multi-tenant-medical-platform, Property 50: 指标包含租户标签**
/// **Validates: Requirements 11.1**
/// 
/// Property: Metrics include tenant labels
/// For any system-emitted Prometheus metric, the metric should include a tenant_id label
/// </summary>
public class MetricsTenantLabelPropertyTests
{
    [Property(MaxTest = 100)]
    public Property AllMetricsShouldIncludeTenantIdLabel()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateHttpMethod(),
            GenerateHttpPath(),
            (tenantId, method, path) =>
            {
                // Arrange
                var metrics = new TenantMetrics("test-service");
                var capturedMeasurements = new List<(string Name, KeyValuePair<string, object?>[] Tags)>();
                
                using var meterListener = new MeterListener();
                meterListener.InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == "test-service")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                };
                
                meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
                {
                    capturedMeasurements.Add((instrument.Name, tags.ToArray()));
                });
                
                meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
                {
                    capturedMeasurements.Add((instrument.Name, tags.ToArray()));
                });
                
                meterListener.Start();

                // Act - Record various metrics
                metrics.RecordRequest(tenantId, method, path);
                metrics.RecordRequestDuration(tenantId, method, path, 0.123);
                metrics.RecordError(tenantId, method, path, 500);

                // Assert - All captured metrics should have tenant_id label
                var allMetricsHaveTenantId = capturedMeasurements.All(m =>
                    m.Tags.Any(tag => tag.Key == "tenant_id" && tag.Value?.ToString() == tenantId));

                return allMetricsHaveTenantId.Label($"All metrics should include tenant_id={tenantId}");
            });
    }

    [Property(MaxTest = 100)]
    public Property TenantIdLabelShouldMatchRequestTenant()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateHttpMethod(),
            GenerateHttpPath(),
            (tenantId, method, path) =>
            {
                // Arrange
                var metrics = new TenantMetrics("test-service");
                var capturedTenantIds = new List<string>();
                
                using var meterListener = new MeterListener();
                meterListener.InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == "test-service")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                };
                
                meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
                {
                    foreach (var tag in tags)
                    {
                        if (tag.Key == "tenant_id")
                        {
                            capturedTenantIds.Add(tag.Value?.ToString() ?? "");
                            break;
                        }
                    }
                });
                
                meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
                {
                    foreach (var tag in tags)
                    {
                        if (tag.Key == "tenant_id")
                        {
                            capturedTenantIds.Add(tag.Value?.ToString() ?? "");
                            break;
                        }
                    }
                });
                
                meterListener.Start();

                // Act
                metrics.RecordRequest(tenantId, method, path);
                metrics.RecordRequestDuration(tenantId, method, path, 0.5);

                // Assert - All captured tenant IDs should match the input tenant ID
                var allMatch = capturedTenantIds.All(id => id == tenantId);
                var hasCaptures = capturedTenantIds.Count > 0;

                return (allMatch && hasCaptures)
                    .Label($"All {capturedTenantIds.Count} metrics should have tenant_id={tenantId}");
            });
    }

    // Generators
    private static Arbitrary<string> GenerateValidTenantId()
    {
        return Gen.Elements("tenant-a", "tenant-b", "hospital-123", "clinic-xyz", "org-456")
            .ToArbitrary();
    }

    private static Arbitrary<string> GenerateHttpMethod()
    {
        return Gen.Elements("GET", "POST", "PUT", "DELETE", "PATCH")
            .ToArbitrary();
    }

    private static Arbitrary<string> GenerateHttpPath()
    {
        return Gen.Elements("/api/tenants", "/api/devices", "/api/transactions", "/health")
            .ToArbitrary();
    }
}

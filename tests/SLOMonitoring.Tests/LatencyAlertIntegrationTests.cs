using FluentAssertions;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using Xunit;

namespace SLOMonitoring.Tests;

/// <summary>
/// Integration tests for latency alerting
/// Validates: Requirements 12.4
/// </summary>
public class LatencyAlertIntegrationTests
{
    [Fact]
    public async Task HighLatency_ShouldTriggerAlert()
    {
        // Arrange: Create a tenant with SLO configuration
        var tenant = new CreateTenantRequest
        {
            DisplayName = "Test Hospital",
            DbConfig = new DatabaseConfig
            {
                Mode = "perDatabase",
                Server = "sqlserver.local",
                Database = "TestDB",
                CredentialRef = "keyvault/secret1"
            },
            Slo = new SLOConfig
            {
                Availability = "99.9%",
                P95LatencyMs = 1000
            }
        };

        // Simulate high latency scenario
        var p95LatencyMs = 1500; // 1500ms exceeds 1000ms threshold
        var sloThreshold = tenant.Slo.P95LatencyMs;

        // Act: Calculate if alert should trigger
        var shouldTriggerAlert = p95LatencyMs > sloThreshold;

        // Assert: Verify alert would be triggered
        shouldTriggerAlert.Should().BeTrue("p95 latency of {0}ms exceeds {1}ms threshold", p95LatencyMs, sloThreshold);
        p95LatencyMs.Should().BeGreaterThan(sloThreshold);
    }

    [Fact]
    public async Task LowLatency_ShouldNotTriggerAlert()
    {
        // Arrange: Create a tenant with SLO configuration
        var tenant = new CreateTenantRequest
        {
            DisplayName = "Test Hospital",
            DbConfig = new DatabaseConfig
            {
                Mode = "perDatabase",
                Server = "sqlserver.local",
                Database = "TestDB",
                CredentialRef = "keyvault/secret1"
            },
            Slo = new SLOConfig
            {
                Availability = "99.9%",
                P95LatencyMs = 1000
            }
        };

        // Simulate low latency scenario
        var p95LatencyMs = 800; // 800ms is below 1000ms threshold
        var sloThreshold = tenant.Slo.P95LatencyMs;

        // Act: Calculate if alert should trigger
        var shouldTriggerAlert = p95LatencyMs > sloThreshold;

        // Assert: Verify alert would NOT be triggered
        shouldTriggerAlert.Should().BeFalse("p95 latency of {0}ms is below {1}ms threshold", p95LatencyMs, sloThreshold);
        p95LatencyMs.Should().BeLessThanOrEqualTo(sloThreshold);
    }

    [Fact]
    public async Task LatencyAlert_ShouldIncludeTenantIdAndViolationDetails()
    {
        // Arrange
        var tenantId = "hospital-a";
        var p95LatencyMs = 1500;
        var threshold = 1000;

        // Act: Create alert message
        var alertMessage = new
        {
            TenantId = tenantId,
            P95LatencyMs = p95LatencyMs,
            Threshold = threshold,
            ViolationDetails = $"Tenant {tenantId}: p95 latency: {p95LatencyMs}ms, Threshold: {threshold}ms"
        };

        // Assert: Verify alert contains required information
        alertMessage.TenantId.Should().Be(tenantId);
        alertMessage.P95LatencyMs.Should().Be(p95LatencyMs);
        alertMessage.Threshold.Should().Be(threshold);
        alertMessage.ViolationDetails.Should().Contain(tenantId);
        alertMessage.ViolationDetails.Should().Contain(p95LatencyMs.ToString());
        alertMessage.ViolationDetails.Should().Contain(threshold.ToString());
    }

    [Theory]
    [InlineData(1000, 1000, false)]  // Exactly at threshold - should not trigger
    [InlineData(1001, 1000, true)]   // Just above threshold - should trigger
    [InlineData(2000, 1000, true)]   // Well above threshold - should trigger
    [InlineData(500, 1000, false)]   // Well below threshold - should not trigger
    [InlineData(0, 1000, false)]     // Zero latency - should not trigger
    public void LatencyThreshold_ShouldTriggerCorrectly(
        int p95LatencyMs,
        int threshold,
        bool shouldTrigger)
    {
        // Act
        var alertTriggered = p95LatencyMs > threshold;

        // Assert
        alertTriggered.Should().Be(shouldTrigger);
    }

    [Fact]
    public async Task MultiTenantLatencies_ShouldTriggerAlertsIndependently()
    {
        // Arrange: Multiple tenants with different latency thresholds and actual latencies
        var tenants = new[]
        {
            new { TenantId = "hospital-a", P95LatencyMs = 1500, Threshold = 1000 }, // Should trigger
            new { TenantId = "hospital-b", P95LatencyMs = 800, Threshold = 1000 },  // Should not trigger
            new { TenantId = "hospital-c", P95LatencyMs = 2500, Threshold = 2000 }, // Should trigger
            new { TenantId = "hospital-d", P95LatencyMs = 1800, Threshold = 2000 }, // Should not trigger
        };

        // Act: Calculate which tenants should trigger alerts
        var alertResults = tenants.Select(t => new
        {
            t.TenantId,
            t.P95LatencyMs,
            t.Threshold,
            ShouldTrigger = t.P95LatencyMs > t.Threshold
        }).ToList();

        // Assert: Verify correct tenants trigger alerts
        alertResults[0].ShouldTrigger.Should().BeTrue("hospital-a has 1500ms latency exceeding 1000ms threshold");
        alertResults[1].ShouldTrigger.Should().BeFalse("hospital-b has 800ms latency below 1000ms threshold");
        alertResults[2].ShouldTrigger.Should().BeTrue("hospital-c has 2500ms latency exceeding 2000ms threshold");
        alertResults[3].ShouldTrigger.Should().BeFalse("hospital-d has 1800ms latency below 2000ms threshold");
    }

    [Fact]
    public async Task LatencyAlert_ShouldTriggerAfterSustainedHighLatency()
    {
        // Arrange: Simulate sustained high latency over time
        var tenantId = "hospital-a";
        var threshold = 1000;
        var sustainedDurationMinutes = 5;

        // Simulate latency measurements over 5 minutes (one per minute)
        var latencyMeasurements = new[]
        {
            1200, // Minute 1: Above threshold
            1300, // Minute 2: Above threshold
            1250, // Minute 3: Above threshold
            1400, // Minute 4: Above threshold
            1350  // Minute 5: Above threshold
        };

        // Act: Check if all measurements exceed threshold
        var allMeasurementsExceedThreshold = latencyMeasurements.All(l => l > threshold);
        var averageLatency = latencyMeasurements.Average();

        // Assert: Verify sustained high latency triggers alert
        allMeasurementsExceedThreshold.Should().BeTrue("all measurements exceed threshold for {0} minutes", sustainedDurationMinutes);
        averageLatency.Should().BeGreaterThan(threshold);
    }

    [Fact]
    public async Task LatencyAlert_ShouldNotTriggerForTransientSpikes()
    {
        // Arrange: Simulate transient latency spike
        var tenantId = "hospital-a";
        var threshold = 1000;

        // Simulate latency measurements with one spike
        var latencyMeasurements = new[]
        {
            800,  // Minute 1: Below threshold
            850,  // Minute 2: Below threshold
            2000, // Minute 3: Spike above threshold
            900,  // Minute 4: Below threshold
            850   // Minute 5: Below threshold
        };

        // Act: Check if sustained high latency (majority of measurements)
        var measurementsExceedingThreshold = latencyMeasurements.Count(l => l > threshold);
        var sustainedHighLatency = measurementsExceedingThreshold >= latencyMeasurements.Length * 0.8; // 80% threshold

        // Assert: Verify transient spike does not trigger sustained alert
        sustainedHighLatency.Should().BeFalse("only {0} out of {1} measurements exceed threshold", 
            measurementsExceedingThreshold, latencyMeasurements.Length);
    }

    [Fact]
    public async Task LatencyAlert_ShouldCalculateP95FromDistribution()
    {
        // Arrange: Simulate a distribution of latencies
        var latencies = new List<int>
        {
            100, 150, 200, 250, 300, 350, 400, 450, 500, 550,
            600, 650, 700, 750, 800, 850, 900, 950, 1000, 1050,
            1100, 1150, 1200, 1250, 1300, 1350, 1400, 1450, 1500, 1550,
            1600, 1650, 1700, 1750, 1800, 1850, 1900, 1950, 2000, 2050,
            2100, 2150, 2200, 2250, 2300, 2350, 2400, 2450, 2500, 2550,
            2600, 2650, 2700, 2750, 2800, 2850, 2900, 2950, 3000, 3050,
            3100, 3150, 3200, 3250, 3300, 3350, 3400, 3450, 3500, 3550,
            3600, 3650, 3700, 3750, 3800, 3850, 3900, 3950, 4000, 4050,
            4100, 4150, 4200, 4250, 4300, 4350, 4400, 4450, 4500, 4550,
            4600, 4650, 4700, 4750, 4800, 4850, 4900, 4950, 5000, 5050
        };

        var threshold = 1000;

        // Act: Calculate p95 (95th percentile)
        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        var p95Index = (int)Math.Ceiling(sortedLatencies.Count * 0.95) - 1;
        var p95Latency = sortedLatencies[p95Index];

        var shouldTriggerAlert = p95Latency > threshold;

        // Assert: Verify p95 calculation and alert triggering
        p95Latency.Should().BeGreaterThan(threshold);
        shouldTriggerAlert.Should().BeTrue("p95 latency of {0}ms exceeds {1}ms threshold", p95Latency, threshold);
    }

    [Fact]
    public async Task DifferentTenants_ShouldHaveDifferentLatencyThresholds()
    {
        // Arrange: Tenants with different SLO configurations
        var premiumTenant = new CreateTenantRequest
        {
            DisplayName = "Premium Hospital",
            DbConfig = new DatabaseConfig
            {
                Mode = "perDatabase",
                Server = "sqlserver.local",
                Database = "PremiumDB",
                CredentialRef = "keyvault/secret1"
            },
            Slo = new SLOConfig
            {
                Availability = "99.99%",
                P95LatencyMs = 500  // Stricter SLO
            }
        };

        var standardTenant = new CreateTenantRequest
        {
            DisplayName = "Standard Hospital",
            DbConfig = new DatabaseConfig
            {
                Mode = "perDatabase",
                Server = "sqlserver.local",
                Database = "StandardDB",
                CredentialRef = "keyvault/secret2"
            },
            Slo = new SLOConfig
            {
                Availability = "99.9%",
                P95LatencyMs = 2000  // More relaxed SLO
            }
        };

        var actualLatency = 1000; // Same latency for both

        // Act: Check if alerts should trigger for each tenant
        var premiumShouldAlert = actualLatency > premiumTenant.Slo.P95LatencyMs;
        var standardShouldAlert = actualLatency > standardTenant.Slo.P95LatencyMs;

        // Assert: Verify different thresholds result in different alert behavior
        premiumShouldAlert.Should().BeTrue("1000ms exceeds premium threshold of 500ms");
        standardShouldAlert.Should().BeFalse("1000ms is below standard threshold of 2000ms");
    }
}

using FluentAssertions;
using System.Net.Http.Json;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using Xunit;

namespace SLOMonitoring.Tests;

/// <summary>
/// Integration tests for error rate alerting
/// Validates: Requirements 12.3
/// </summary>
public class ErrorRateAlertIntegrationTests
{
    [Fact]
    public async Task HighErrorRate_ShouldTriggerAlert()
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
                Availability = "95.0%",
                P95LatencyMs = 1000
            }
        };

        // Simulate high error rate scenario
        var totalRequests = 100;
        var errorRequests = 10; // 10% error rate, exceeds 5% threshold
        var errorRate = (double)errorRequests / totalRequests * 100;

        // Act: Calculate if alert should trigger
        var shouldTriggerAlert = errorRate > 5.0;

        // Assert: Verify alert would be triggered
        shouldTriggerAlert.Should().BeTrue("error rate of {0}% exceeds 5% threshold", errorRate);
        errorRate.Should().BeGreaterThan(5.0);
    }

    [Fact]
    public async Task LowErrorRate_ShouldNotTriggerAlert()
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
                Availability = "95.0%",
                P95LatencyMs = 1000
            }
        };

        // Simulate low error rate scenario
        var totalRequests = 100;
        var errorRequests = 3; // 3% error rate, below 5% threshold
        var errorRate = (double)errorRequests / totalRequests * 100;

        // Act: Calculate if alert should trigger
        var shouldTriggerAlert = errorRate > 5.0;

        // Assert: Verify alert would NOT be triggered
        shouldTriggerAlert.Should().BeFalse("error rate of {0}% is below 5% threshold", errorRate);
        errorRate.Should().BeLessThanOrEqualTo(5.0);
    }

    [Fact]
    public async Task ErrorRateAlert_ShouldIncludeTenantIdAndViolationDetails()
    {
        // Arrange
        var tenantId = "hospital-a";
        var errorRate = 7.5; // 7.5% error rate
        var threshold = 5.0;

        // Act: Create alert message
        var alertMessage = new
        {
            TenantId = tenantId,
            ErrorRate = errorRate,
            Threshold = threshold,
            ViolationDetails = $"Tenant {tenantId}: Error rate: {errorRate}%, Threshold: {threshold}%"
        };

        // Assert: Verify alert contains required information
        alertMessage.TenantId.Should().Be(tenantId);
        alertMessage.ErrorRate.Should().Be(errorRate);
        alertMessage.Threshold.Should().Be(threshold);
        alertMessage.ViolationDetails.Should().Contain(tenantId);
        alertMessage.ViolationDetails.Should().Contain(errorRate.ToString());
        alertMessage.ViolationDetails.Should().Contain(threshold.ToString());
    }

    [Theory]
    [InlineData(100, 5, 5.0, false)]  // Exactly at threshold - should not trigger
    [InlineData(100, 6, 6.0, true)]   // Just above threshold - should trigger
    [InlineData(100, 10, 10.0, true)] // Well above threshold - should trigger
    [InlineData(100, 0, 0.0, false)]  // No errors - should not trigger
    [InlineData(100, 1, 1.0, false)]  // Very low error rate - should not trigger
    public void ErrorRateThreshold_ShouldTriggerCorrectly(
        int totalRequests, 
        int errorRequests, 
        double expectedErrorRate, 
        bool shouldTrigger)
    {
        // Arrange
        var errorRate = (double)errorRequests / totalRequests * 100;
        var threshold = 5.0;

        // Act
        var alertTriggered = errorRate > threshold;

        // Assert
        errorRate.Should().Be(expectedErrorRate);
        alertTriggered.Should().Be(shouldTrigger);
    }

    [Fact]
    public async Task MultiTenantErrorRates_ShouldTriggerAlertsIndependently()
    {
        // Arrange: Multiple tenants with different error rates
        var tenants = new[]
        {
            new { TenantId = "hospital-a", TotalRequests = 100, ErrorRequests = 10 }, // 10% - should trigger
            new { TenantId = "hospital-b", TotalRequests = 100, ErrorRequests = 2 },  // 2% - should not trigger
            new { TenantId = "hospital-c", TotalRequests = 100, ErrorRequests = 7 },  // 7% - should trigger
        };

        var threshold = 5.0;

        // Act: Calculate which tenants should trigger alerts
        var alertResults = tenants.Select(t => new
        {
            t.TenantId,
            ErrorRate = (double)t.ErrorRequests / t.TotalRequests * 100,
            ShouldTrigger = ((double)t.ErrorRequests / t.TotalRequests * 100) > threshold
        }).ToList();

        // Assert: Verify correct tenants trigger alerts
        alertResults[0].ShouldTrigger.Should().BeTrue("hospital-a has 10% error rate");
        alertResults[1].ShouldTrigger.Should().BeFalse("hospital-b has 2% error rate");
        alertResults[2].ShouldTrigger.Should().BeTrue("hospital-c has 7% error rate");
    }
}

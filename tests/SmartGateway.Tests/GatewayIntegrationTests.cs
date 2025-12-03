using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace SmartGateway.Tests;

/// <summary>
/// Integration tests for Smart Gateway end-to-end request flow
/// Tests requirements: 4.1, 4.2, 4.3, 4.4
/// </summary>
public class GatewayIntegrationTests : IClassFixture<GatewayTestFixture>
{
    private readonly GatewayTestFixture _fixture;

    public GatewayIntegrationTests(GatewayTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Gateway_WithValidDevice_ShouldInjectTenantIdAndForwardRequest()
    {
        // Arrange
        var deviceId = "DEVICE-TEST-001";
        var expectedTenantId = "hospital-a";

        // Register device in Device Registry
        await _fixture.RegisterDeviceAsync(deviceId, expectedTenantId);

        // Create request with Device-Id header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request.Headers.Add("Device-Id", deviceId);

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify X-Tenant-Id was injected (captured by test backend)
        var capturedHeaders = await _fixture.GetCapturedHeadersAsync();
        capturedHeaders.Should().ContainKey("X-Tenant-Id");
        capturedHeaders["X-Tenant-Id"].Should().Be(expectedTenantId);
    }

    [Fact]
    public async Task Gateway_WithMissingDeviceId_ShouldReturn400()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        // No Device-Id header

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Code.Should().Be("MISSING_DEVICE_ID");
    }

    [Fact]
    public async Task Gateway_WithUnregisteredDevice_ShouldReturn401()
    {
        // Arrange
        var deviceId = "UNREGISTERED-DEVICE";
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request.Headers.Add("Device-Id", deviceId);

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Error.Code.Should().Be("DEVICE_NOT_AUTHORIZED");
    }

    [Fact]
    public async Task Gateway_WithMultipleDevices_ShouldMapToCorrectTenants()
    {
        // Arrange
        var device1 = "DEVICE-TENANT-A";
        var device2 = "DEVICE-TENANT-B";
        var tenantA = "hospital-a";
        var tenantB = "hospital-b";

        await _fixture.RegisterDeviceAsync(device1, tenantA);
        await _fixture.RegisterDeviceAsync(device2, tenantB);

        // Act - Request from device 1
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request1.Headers.Add("Device-Id", device1);
        var response1 = await _fixture.GatewayClient.SendAsync(request1);

        // Act - Request from device 2
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request2.Headers.Add("Device-Id", device2);
        var response2 = await _fixture.GatewayClient.SendAsync(request2);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify correct tenant IDs were injected
        var headers1 = await _fixture.GetCapturedHeadersAsync();
        headers1["X-Tenant-Id"].Should().Be(tenantA);

        var headers2 = await _fixture.GetCapturedHeadersAsync();
        headers2["X-Tenant-Id"].Should().Be(tenantB);
    }

    [Fact]
    public async Task Gateway_WhenDeviceRegistryUnavailable_ShouldReturn503()
    {
        // Arrange
        await _fixture.StopDeviceRegistryAsync();
        
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        request.Headers.Add("Device-Id", "DEVICE-001");

        try
        {
            // Act
            var response = await _fixture.GatewayClient.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            error.Should().NotBeNull();
            error!.Error.Code.Should().Be("SERVICE_UNAVAILABLE");
        }
        finally
        {
            // Cleanup
            await _fixture.RestartDeviceRegistryAsync();
        }
    }

    [Fact]
    public async Task Gateway_ShouldForwardRequestToBackendService()
    {
        // Arrange
        var deviceId = "DEVICE-FORWARD-TEST";
        var tenantId = "hospital-test";
        await _fixture.RegisterDeviceAsync(deviceId, tenantId);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions");
        request.Headers.Add("Device-Id", deviceId);
        request.Content = JsonContent.Create(new { amount = 100.50, description = "Test transaction" });

        // Act
        var response = await _fixture.GatewayClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify request was forwarded with correct headers
        var capturedRequest = await _fixture.GetCapturedRequestAsync();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().ContainKey("X-Tenant-Id");
        capturedRequest.Headers["X-Tenant-Id"].Should().Be(tenantId);
        capturedRequest.Method.Should().Be("POST");
        capturedRequest.Path.Should().Be("/api/transactions");
    }
}

public class ErrorResponse
{
    public ErrorDetail Error { get; set; } = null!;
}

public class ErrorDetail
{
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? TenantId { get; set; }
    public double Timestamp { get; set; }
}

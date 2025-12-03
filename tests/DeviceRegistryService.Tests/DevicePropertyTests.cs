using System.Net;
using System.Net.Http.Json;
using DeviceRegistryService.DTOs;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace DeviceRegistryService.Tests;

public class DevicePropertyTests : IClassFixture<DeviceApiTestFixture>
{
    private readonly DeviceApiTestFixture _factory;

    public DevicePropertyTests(DeviceApiTestFixture factory)
    {
        _factory = factory;
    }

    // **Feature: multi-tenant-medical-platform, Property 11: 设备注册创建映射**
    // **Validates: Requirements 3.1**
    [Property(MaxTest = 100)]
    public Property DeviceRegistrationCreatesMapping()
    {
        return Prop.ForAll(
            GenerateValidDeviceRequest(),
            (request) =>
            {
                var client = _factory.CreateClientWithCleanDatabase();

                // Register the device
                var registerResponse = client.PostAsJsonAsync("/api/devices", request).Result;
                
                if (!registerResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var createdDevice = registerResponse.Content.ReadFromJsonAsync<DeviceResponse>().Result;
                
                if (createdDevice == null)
                {
                    return false;
                }

                // Verify the mapping was created by querying the device
                var getResponse = client.GetAsync($"/api/devices/{request.SerialNumber}").Result;
                
                if (!getResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var retrievedDevice = getResponse.Content.ReadFromJsonAsync<DeviceResponse>().Result;
                
                return retrievedDevice != null &&
                       retrievedDevice.SerialNumber == request.SerialNumber &&
                       retrievedDevice.TenantId == request.TenantId &&
                       retrievedDevice.DeviceType == request.DeviceType;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 12: 设备查询往返一致性**
    // **Validates: Requirements 3.2**
    [Property(MaxTest = 100)]
    public Property DeviceQueryRoundTripConsistency()
    {
        return Prop.ForAll(
            GenerateValidDeviceRequest(),
            (request) =>
            {
                var client = _factory.CreateClientWithCleanDatabase();

                // Register the device
                var registerResponse = client.PostAsJsonAsync("/api/devices", request).Result;
                
                if (!registerResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                // Query the device by serial number
                var getResponse = client.GetAsync($"/api/devices/{request.SerialNumber}").Result;
                
                if (!getResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                var device = getResponse.Content.ReadFromJsonAsync<DeviceResponse>().Result;
                
                // Verify the returned data matches what was registered
                return device != null &&
                       device.SerialNumber == request.SerialNumber &&
                       device.TenantId == request.TenantId &&
                       device.DeviceType == request.DeviceType;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 13: 未注册设备返回错误**
    // **Validates: Requirements 3.3**
    [Property(MaxTest = 100)]
    public Property UnregisteredDeviceReturnsError()
    {
        return Prop.ForAll(
            GenerateNonEmptyString(),
            (serialNumber) =>
            {
                var client = _factory.CreateClientWithCleanDatabase();

                // Query a device that doesn't exist
                var response = client.GetAsync($"/api/devices/{serialNumber}").Result;
                
                // Should return 404 Not Found
                return response.StatusCode == HttpStatusCode.NotFound;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 14: 设备租户关联可更新**
    // **Validates: Requirements 3.4**
    [Property(MaxTest = 100)]
    public Property DeviceTenantAssociationCanBeUpdated()
    {
        return Prop.ForAll(
            GenerateValidDeviceRequest(),
            GenerateNonEmptyString(),
            (initialRequest, newTenantId) =>
            {
                var client = _factory.CreateClientWithCleanDatabase();

                // Register the device
                var registerResponse = client.PostAsJsonAsync("/api/devices", initialRequest).Result;
                
                if (!registerResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                // Update the tenant association
                var updateRequest = new UpdateDeviceRequest { TenantId = newTenantId };
                var updateResponse = client.PatchAsJsonAsync($"/api/devices/{initialRequest.SerialNumber}", updateRequest).Result;
                
                if (!updateResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                // Verify the update
                var getResponse = client.GetAsync($"/api/devices/{initialRequest.SerialNumber}").Result;
                var device = getResponse.Content.ReadFromJsonAsync<DeviceResponse>().Result;
                
                return device != null && device.TenantId == newTenantId;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 15: 重复注册返回冲突**
    // **Validates: Requirements 3.5**
    [Property(MaxTest = 100)]
    public Property DuplicateRegistrationReturnsConflict()
    {
        return Prop.ForAll(
            GenerateValidDeviceRequest(),
            (request) =>
            {
                var client = _factory.CreateClientWithCleanDatabase();

                // Register the device first time
                var firstResponse = client.PostAsJsonAsync("/api/devices", request).Result;
                
                if (!firstResponse.IsSuccessStatusCode)
                {
                    return false;
                }

                // Try to register the same device again
                var secondResponse = client.PostAsJsonAsync("/api/devices", request).Result;
                
                // Should return 409 Conflict
                return secondResponse.StatusCode == HttpStatusCode.Conflict;
            });
    }

    // Helper method to generate valid device requests
    private static Arbitrary<RegisterDeviceRequest> GenerateValidDeviceRequest()
    {
        return Arb.From(
            from serialNumber in GenerateNonEmptyString().Generator
            from tenantId in GenerateNonEmptyString().Generator
            from deviceType in GenerateNonEmptyString().Generator
            select new RegisterDeviceRequest
            {
                SerialNumber = serialNumber,
                TenantId = tenantId,
                DeviceType = deviceType
            });
    }

    // Helper method to generate non-empty strings
    private static Arbitrary<string> GenerateNonEmptyString()
    {
        return Arb.From(
            Gen.Elements("device", "tenant", "type", "serial", "id", "test", "medical", "station")
                .SelectMany(prefix => 
                    Arb.Generate<PositiveInt>().Select(num => $"{prefix}-{num.Get}")));
    }
}

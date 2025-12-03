using System.Net.Http.Json;

namespace AdminUI.Services;

public class DeviceRegistryClient
{
    private readonly HttpClient _httpClient;

    public DeviceRegistryClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DeviceDto>> GetDevicesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/devices");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<DeviceDto>>() ?? new List<DeviceDto>();
        }
        catch
        {
            return new List<DeviceDto>();
        }
    }

    public async Task<DeviceDto?> RegisterDeviceAsync(RegisterDeviceRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/devices", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DeviceDto>();
    }
}

public class DeviceDto
{
    public string SerialNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

public class RegisterDeviceRequest
{
    public string SerialNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "medDispense";
}

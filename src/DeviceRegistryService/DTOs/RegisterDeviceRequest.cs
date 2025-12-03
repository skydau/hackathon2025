namespace DeviceRegistryService.DTOs;

public class RegisterDeviceRequest
{
    public string SerialNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
}

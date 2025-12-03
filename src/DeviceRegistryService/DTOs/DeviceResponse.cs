namespace DeviceRegistryService.DTOs;

public class DeviceResponse
{
    public string SerialNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

namespace DeviceRegistryService.Models;

public class Device
{
    public string SerialNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

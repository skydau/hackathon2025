namespace MedDispenseSimulator.Models;

/// <summary>
/// 设备配置模型
/// </summary>
public class DeviceConfig
{
    /// <summary>
    /// 设备序列号
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 租户ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备位置
    /// </summary>
    public string Location { get; set; } = string.Empty;
    
    /// <summary>
    /// 设备描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
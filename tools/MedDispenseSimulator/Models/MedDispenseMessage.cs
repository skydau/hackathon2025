namespace MedDispenseSimulator.Models;

/// <summary>
/// 药品配送消息模型
/// </summary>
public class MedDispenseMessage
{
    /// <summary>
    /// 消息ID
    /// </summary>
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 设备序列号
    /// </summary>
    public string DeviceSerialNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// 租户ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;
    
    /// <summary>
    /// 患者ID
    /// </summary>
    public string PatientId { get; set; } = string.Empty;
    
    /// <summary>
    /// 药品信息
    /// </summary>
    public List<MedicationItem> Medications { get; set; } = new();
    
    /// <summary>
    /// 配送时间
    /// </summary>
    public DateTime DispenseTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 总金额
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// 操作员ID
    /// </summary>
    public string OperatorId { get; set; } = string.Empty;
    
    /// <summary>
    /// 备注
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// 药品项目
/// </summary>
public class MedicationItem
{
    /// <summary>
    /// 药品代码
    /// </summary>
    public string MedicationCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 药品名称
    /// </summary>
    public string MedicationName { get; set; } = string.Empty;
    
    /// <summary>
    /// 数量
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// 单价
    /// </summary>
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// 小计
    /// </summary>
    public decimal Subtotal => Quantity * UnitPrice;
}
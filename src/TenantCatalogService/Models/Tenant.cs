using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantCatalogService.Models;

public class Tenant
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required]
    public TenantStatus Status { get; set; }
    
    // 数据库配置字段 - 扁平化存储
    [Required]
    [Column("DbServer")]
    public string DbServer { get; set; } = string.Empty;
    
    [Required]
    [Column("DbDatabase")]
    public string DbDatabase { get; set; } = string.Empty;
    
    [Required]
    [Column("DbUsername")]
    public string DbUsername { get; set; } = string.Empty;
    
    [Required]
    [Column("DbPasswordRef")]
    public string DbPasswordRef { get; set; } = string.Empty;
    
    // 限流配置字段
    [Column("ThrottlingRps")]
    public int? ThrottlingRps { get; set; }
    
    // SLO配置字段
    [Column("SloAvailability")]
    public string? SloAvailability { get; set; }
    
    [Column("SloP95LatencyMs")]
    public int? SloP95LatencyMs { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    // 计算属性 - 为了保持向后兼容性
    [NotMapped]
    public DatabaseConfig DbConfig 
    { 
        get => new DatabaseConfig
        {
            Mode = "perDatabase", // 默认模式
            Server = DbServer,
            Database = DbDatabase,
            CredentialRef = DbPasswordRef
        };
        set
        {
            DbServer = value.Server;
            DbDatabase = value.Database;
            DbPasswordRef = value.CredentialRef;
        }
    }
    
    [NotMapped]
    public ThrottlingConfig? Throttling 
    { 
        get => ThrottlingRps.HasValue ? new ThrottlingConfig { Rps = ThrottlingRps.Value } : null;
        set => ThrottlingRps = value?.Rps;
    }
    
    [NotMapped]
    public SLOConfig? Slo 
    { 
        get => !string.IsNullOrEmpty(SloAvailability) || SloP95LatencyMs.HasValue 
            ? new SLOConfig 
            { 
                Availability = SloAvailability ?? string.Empty, 
                P95LatencyMs = SloP95LatencyMs ?? 0 
            } 
            : null;
        set
        {
            SloAvailability = value?.Availability;
            SloP95LatencyMs = value?.P95LatencyMs;
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantCatalogService.Models;

public class ResourceUsage
{
    [Key]
    public long Id { get; set; } // 匹配数据库中的 BIGINT IDENTITY
    
    [Required]
    public string TenantId { get; set; } = string.Empty;
    
    [Column("CpuUsage")]
    public decimal? CpuUsage { get; set; } // 匹配数据库中的 DECIMAL(18,4)
    
    [Column("MemoryUsageMB")]
    public decimal? MemoryUsageMB { get; set; } // 匹配数据库中的 DECIMAL(18,4)
    
    [Column("StorageUsageGB")]
    public decimal? StorageUsageGB { get; set; } // 匹配数据库中的 DECIMAL(18,4)
    
    [Column("NetworkUsageGB")]
    public decimal? NetworkUsageGB { get; set; } // 匹配数据库中的 DECIMAL(18,4)
    
    [Required]
    [Column("RecordedAt")]
    public DateTime RecordedAt { get; set; } // 匹配数据库中的 RecordedAt
    
    // 为了保持向后兼容性的计算属性
    [NotMapped]
    public DateTime Timestamp 
    { 
        get => RecordedAt; 
        set => RecordedAt = value; 
    }
    
    [NotMapped]
    public double CpuCores 
    { 
        get => (double)(CpuUsage ?? 0); 
        set => CpuUsage = (decimal)value; 
    }
    
    [NotMapped]
    public double MemoryGb 
    { 
        get => (double)((MemoryUsageMB ?? 0) / 1024); // 转换 MB 到 GB
        set => MemoryUsageMB = (decimal)(value * 1024); 
    }
    
    [NotMapped]
    public double StorageGb 
    { 
        get => (double)(StorageUsageGB ?? 0); 
        set => StorageUsageGB = (decimal)value; 
    }
    
    [NotMapped]
    public double NetworkGb 
    { 
        get => (double)(NetworkUsageGB ?? 0); 
        set => NetworkUsageGB = (decimal)value; 
    }
}

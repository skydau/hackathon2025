using System.ComponentModel.DataAnnotations;

namespace TenantCatalogService.DTOs;

public class ResourceUsageRequest
{
    [Required]
    public string TenantId { get; set; } = string.Empty;
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    [Range(0, double.MaxValue)]
    public double CpuCores { get; set; }
    
    [Range(0, double.MaxValue)]
    public double MemoryGb { get; set; }
    
    [Range(0, double.MaxValue)]
    public double StorageGb { get; set; }
    
    [Range(0, double.MaxValue)]
    public double NetworkGb { get; set; }
}

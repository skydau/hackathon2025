using System.ComponentModel.DataAnnotations;

namespace TenantCatalogService.Models;

public class ResourceUsage
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string TenantId { get; set; } = string.Empty;
    
    [Required]
    public DateTime Timestamp { get; set; }
    
    public double CpuCores { get; set; }
    
    public double MemoryGb { get; set; }
    
    public double StorageGb { get; set; }
    
    public double NetworkGb { get; set; }
}

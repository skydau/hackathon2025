using System.ComponentModel.DataAnnotations;

namespace TenantCatalogService.Models;

public class Tenant
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required]
    public TenantStatus Status { get; set; }
    
    [Required]
    public DatabaseConfig DbConfig { get; set; } = new();
    
    public ThrottlingConfig? Throttling { get; set; }
    
    public SLOConfig? Slo { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;
using TenantCatalogService.Models;

namespace TenantCatalogService.DTOs;

public class CreateTenantRequest
{
    [Required]
    public string DisplayName { get; set; } = string.Empty;
    
    [Required]
    public DatabaseConfig DbConfig { get; set; } = new();
    
    public ThrottlingConfig? Throttling { get; set; }
    
    public SLOConfig? Slo { get; set; }
}

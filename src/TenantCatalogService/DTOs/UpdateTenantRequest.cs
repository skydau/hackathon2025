using TenantCatalogService.Models;

namespace TenantCatalogService.DTOs;

public class UpdateTenantRequest
{
    public TenantStatus? Status { get; set; }
    public string? DisplayName { get; set; }
    public DatabaseConfig? DbConfig { get; set; }
    public ThrottlingConfig? Throttling { get; set; }
    public SLOConfig? Slo { get; set; }
}

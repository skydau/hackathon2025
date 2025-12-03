using TenantCatalogService.Models;

namespace TenantCatalogService.DTOs;

public class TenantResponse
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
    public DatabaseConfig DbConfig { get; set; } = new();
    public ThrottlingConfig? Throttling { get; set; }
    public SLOConfig? Slo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

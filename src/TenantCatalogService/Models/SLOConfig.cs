namespace TenantCatalogService.Models;

public class SLOConfig
{
    public string Availability { get; set; } = string.Empty;
    public int P95LatencyMs { get; set; }
}

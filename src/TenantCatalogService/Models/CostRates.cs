namespace TenantCatalogService.Models;

public class CostRates
{
    public double CpuPerCoreHour { get; set; } = 0.05;
    public double MemoryPerGbHour { get; set; } = 0.01;
    public double StoragePerGbMonth { get; set; } = 0.10;
    public double NetworkPerGb { get; set; } = 0.12;
}

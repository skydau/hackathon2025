namespace TenantCatalogService.Models;

public class CostReport
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public CostBreakdown Breakdown { get; set; } = new();
    public double TotalCost { get; set; }
}

public class CostBreakdown
{
    public double CpuCost { get; set; }
    public double MemoryCost { get; set; }
    public double StorageCost { get; set; }
    public double NetworkCost { get; set; }
}

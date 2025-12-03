namespace TenantCatalogService.DTOs;

public class CostReportResponse
{
    public string TenantId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public CostBreakdownDto Breakdown { get; set; } = new();
    public double TotalCost { get; set; }
}

public class CostBreakdownDto
{
    public double CpuCost { get; set; }
    public double MemoryCost { get; set; }
    public double StorageCost { get; set; }
    public double NetworkCost { get; set; }
}

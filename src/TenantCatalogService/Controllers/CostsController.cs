using Microsoft.AspNetCore.Mvc;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using TenantCatalogService.Services;

namespace TenantCatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CostsController : ControllerBase
{
    private readonly ICostCalculationService _costService;
    private readonly ILogger<CostsController> _logger;

    public CostsController(ICostCalculationService costService, ILogger<CostsController> logger)
    {
        _costService = costService;
        _logger = logger;
    }

    [HttpPost("usage")]
    public async Task<IActionResult> RecordResourceUsage([FromBody] ResourceUsageRequest request)
    {
        var usage = new ResourceUsage
        {
            TenantId = request.TenantId,
            Timestamp = request.Timestamp,
            CpuCores = request.CpuCores,
            MemoryGb = request.MemoryGb,
            StorageGb = request.StorageGb,
            NetworkGb = request.NetworkGb
        };

        await _costService.RecordResourceUsageAsync(usage);
        
        _logger.LogInformation("Recorded resource usage for tenant {TenantId}", request.TenantId);
        
        return Ok(new { message = "Resource usage recorded successfully" });
    }

    [HttpGet("tenants/{tenantId}/monthly/{year}/{month}")]
    public async Task<ActionResult<CostReportResponse>> GetMonthlyCostReport(string tenantId, int year, int month)
    {
        if (month < 1 || month > 12)
        {
            return BadRequest(new { error = "Month must be between 1 and 12" });
        }

        var report = await _costService.GenerateMonthlyCostReportAsync(tenantId, year, month);
        
        return Ok(MapToResponse(report));
    }

    [HttpGet("tenants/{tenantId}/range")]
    public async Task<ActionResult<CostReportResponse>> GetCostReport(
        string tenantId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return BadRequest(new { error = "Start date must be before end date" });
        }

        var report = await _costService.GenerateCostReportAsync(tenantId, startDate, endDate);
        
        return Ok(MapToResponse(report));
    }

    [HttpGet("tenants/{tenantId}/usage")]
    public async Task<ActionResult<List<ResourceUsage>>> GetResourceUsageHistory(
        string tenantId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return BadRequest(new { error = "Start date must be before end date" });
        }

        var history = await _costService.GetResourceUsageHistoryAsync(tenantId, startDate, endDate);
        
        return Ok(history);
    }

    private static CostReportResponse MapToResponse(CostReport report)
    {
        return new CostReportResponse
        {
            TenantId = report.TenantId,
            StartDate = report.StartDate,
            EndDate = report.EndDate,
            Breakdown = new CostBreakdownDto
            {
                CpuCost = report.Breakdown.CpuCost,
                MemoryCost = report.Breakdown.MemoryCost,
                StorageCost = report.Breakdown.StorageCost,
                NetworkCost = report.Breakdown.NetworkCost
            },
            TotalCost = report.TotalCost
        };
    }
}

using TenantCatalogService.Models;
using TenantCatalogService.Repositories;

namespace TenantCatalogService.Services;

public class CostCalculationService : ICostCalculationService
{
    private readonly IResourceUsageRepository _usageRepository;
    private readonly CostRates _rates;
    private readonly ILogger<CostCalculationService> _logger;

    public CostCalculationService(
        IResourceUsageRepository usageRepository,
        CostRates rates,
        ILogger<CostCalculationService> logger)
    {
        _usageRepository = usageRepository;
        _rates = rates;
        _logger = logger;
    }

    public async Task<CostReport> GenerateMonthlyCostReportAsync(string tenantId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);
        
        return await GenerateCostReportAsync(tenantId, startDate, endDate);
    }

    public async Task<CostReport> GenerateCostReportAsync(string tenantId, DateTime startDate, DateTime endDate)
    {
        var usageRecords = await _usageRepository.GetByTenantAndDateRangeAsync(tenantId, startDate, endDate);
        
        if (!usageRecords.Any())
        {
            _logger.LogWarning("No usage records found for tenant {TenantId} between {StartDate} and {EndDate}", 
                tenantId, startDate, endDate);
            
            return new CostReport
            {
                TenantId = tenantId,
                StartDate = startDate,
                EndDate = endDate,
                Breakdown = new CostBreakdown(),
                TotalCost = 0
            };
        }

        var totalHours = (endDate - startDate).TotalHours;
        
        // Calculate average usage
        var avgCpu = usageRecords.Average(r => r.CpuCores);
        var avgMemory = usageRecords.Average(r => r.MemoryGb);
        var avgStorage = usageRecords.Average(r => r.StorageGb);
        var totalNetwork = usageRecords.Sum(r => r.NetworkGb);

        // Calculate costs
        var cpuCost = avgCpu * totalHours * _rates.CpuPerCoreHour;
        var memoryCost = avgMemory * totalHours * _rates.MemoryPerGbHour;
        var storageCost = avgStorage * (totalHours / 730.0) * _rates.StoragePerGbMonth; // 730 hours per month average
        var networkCost = totalNetwork * _rates.NetworkPerGb;

        var breakdown = new CostBreakdown
        {
            CpuCost = Math.Round(cpuCost, 2),
            MemoryCost = Math.Round(memoryCost, 2),
            StorageCost = Math.Round(storageCost, 2),
            NetworkCost = Math.Round(networkCost, 2)
        };

        var totalCost = breakdown.CpuCost + breakdown.MemoryCost + breakdown.StorageCost + breakdown.NetworkCost;

        _logger.LogInformation("Generated cost report for tenant {TenantId}: Total ${TotalCost}", tenantId, totalCost);

        return new CostReport
        {
            TenantId = tenantId,
            StartDate = startDate,
            EndDate = endDate,
            Breakdown = breakdown,
            TotalCost = Math.Round(totalCost, 2)
        };
    }

    public async Task RecordResourceUsageAsync(ResourceUsage usage)
    {
        // Id 是自增字段，不需要手动设置
        await _usageRepository.CreateAsync(usage);
        
        _logger.LogDebug("记录租户 {TenantId} 在 {Timestamp} 的资源使用情况", 
            usage.TenantId, usage.Timestamp);
    }

    public async Task<List<ResourceUsage>> GetResourceUsageHistoryAsync(string tenantId, DateTime startDate, DateTime endDate)
    {
        return await _usageRepository.GetByTenantAndDateRangeAsync(tenantId, startDate, endDate);
    }
}

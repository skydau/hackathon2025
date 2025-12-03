using Microsoft.AspNetCore.Mvc;
using TenantCatalogService.DTOs;
using TenantCatalogService.Models;
using TenantCatalogService.Repositories;

namespace TenantCatalogService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly ITenantRepository _repository;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(ITenantRepository repository, ILogger<TenantsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TenantResponse>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid().ToString(),
            DisplayName = request.DisplayName,
            Status = TenantStatus.Provisioning,
            DbConfig = request.DbConfig,
            Throttling = request.Throttling,
            Slo = request.Slo,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var created = await _repository.CreateAsync(tenant);
        
        _logger.LogInformation("Created tenant {TenantId} with name {DisplayName}", created.Id, created.DisplayName);

        var response = MapToResponse(created);
        return CreatedAtAction(nameof(GetTenant), new { id = created.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TenantResponse>> GetTenant(string id)
    {
        var tenant = await _repository.GetByIdAsync(id);
        
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found", id);
            return NotFound(new { error = "Tenant not found" });
        }

        return Ok(MapToResponse(tenant));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TenantResponse>> UpdateTenant(string id, [FromBody] UpdateTenantRequest request)
    {
        var tenant = await _repository.GetByIdAsync(id);
        
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found for update", id);
            return NotFound(new { error = "Tenant not found" });
        }

        if (request.Status.HasValue)
        {
            tenant.Status = request.Status.Value;
        }

        if (!string.IsNullOrEmpty(request.DisplayName))
        {
            tenant.DisplayName = request.DisplayName;
        }

        if (request.DbConfig != null)
        {
            tenant.DbConfig = request.DbConfig;
        }

        if (request.Throttling != null)
        {
            tenant.Throttling = request.Throttling;
        }

        if (request.Slo != null)
        {
            tenant.Slo = request.Slo;
        }

        tenant.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(tenant);
        
        _logger.LogInformation("Updated tenant {TenantId}", id);

        return Ok(MapToResponse(updated));
    }

    [HttpGet("{id}/db-config")]
    public async Task<ActionResult<DatabaseConfig>> GetDbConfig(string id)
    {
        var tenant = await _repository.GetByIdAsync(id);
        
        if (tenant == null)
        {
            _logger.LogWarning("Tenant {TenantId} not found for db-config", id);
            return NotFound(new { error = "Tenant not found" });
        }

        return Ok(tenant.DbConfig);
    }

    private static TenantResponse MapToResponse(Tenant tenant)
    {
        return new TenantResponse
        {
            Id = tenant.Id,
            DisplayName = tenant.DisplayName,
            Status = tenant.Status,
            DbConfig = tenant.DbConfig,
            Throttling = tenant.Throttling,
            Slo = tenant.Slo,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt
        };
    }
}

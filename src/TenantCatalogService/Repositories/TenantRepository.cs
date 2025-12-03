using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using TenantCatalogService.Models;

namespace TenantCatalogService.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly TenantCatalogDbContext _context;

    public TenantRepository(TenantCatalogDbContext context)
    {
        _context = context;
    }

    public async Task<Tenant> CreateAsync(Tenant tenant)
    {
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<Tenant?> GetByIdAsync(string id)
    {
        return await _context.Tenants.FindAsync(id);
    }

    public async Task<Tenant> UpdateAsync(Tenant tenant)
    {
        _context.Tenants.Update(tenant);
        await _context.SaveChangesAsync();
        return tenant;
    }

    public async Task<IEnumerable<Tenant>> GetAllAsync()
    {
        return await _context.Tenants.ToListAsync();
    }
}

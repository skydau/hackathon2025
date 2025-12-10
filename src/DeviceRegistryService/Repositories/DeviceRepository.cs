using DeviceRegistryService.Data;
using DeviceRegistryService.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceRegistryService.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly DeviceRegistryDbContext _context;

    public DeviceRepository(DeviceRegistryDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetBySerialNumberAsync(string serialNumber)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == serialNumber);
    }

    public async Task<Device> CreateAsync(Device device)
    {
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<Device?> UpdateAsync(string serialNumber, Device device)
    {
        var existing = await GetBySerialNumberAsync(serialNumber);
        if (existing == null)
        {
            return null;
        }

        existing.TenantId = device.TenantId;
        existing.DeviceType = device.DeviceType;
        existing.LastSeenAt = device.LastSeenAt;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> ExistsAsync(string serialNumber)
    {
        return await _context.Devices
            .AnyAsync(d => d.SerialNumber == serialNumber);
    }

    public async Task<IEnumerable<Device>> GetAllAsync()
    {
        return await _context.Devices
            .OrderByDescending(d => d.RegisteredAt)
            .ToListAsync();
    }
}

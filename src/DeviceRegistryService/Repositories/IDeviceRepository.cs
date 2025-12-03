using DeviceRegistryService.Models;

namespace DeviceRegistryService.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetBySerialNumberAsync(string serialNumber);
    Task<Device> CreateAsync(Device device);
    Task<Device?> UpdateAsync(string serialNumber, Device device);
    Task<bool> ExistsAsync(string serialNumber);
}

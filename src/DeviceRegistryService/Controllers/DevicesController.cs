using DeviceRegistryService.DTOs;
using DeviceRegistryService.Models;
using DeviceRegistryService.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace DeviceRegistryService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IDeviceRepository deviceRepository, ILogger<DevicesController> logger)
    {
        _deviceRepository = deviceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Register a new device
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DeviceResponse>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SerialNumber) || 
            string.IsNullOrWhiteSpace(request.TenantId) || 
            string.IsNullOrWhiteSpace(request.DeviceType))
        {
            return BadRequest(new { error = "SerialNumber, TenantId, and DeviceType are required" });
        }

        // Check if device already exists
        if (await _deviceRepository.ExistsAsync(request.SerialNumber))
        {
            return Conflict(new { error = "Device with this serial number already exists" });
        }

        var device = new Device
        {
            SerialNumber = request.SerialNumber,
            TenantId = request.TenantId,
            DeviceType = request.DeviceType,
            RegisteredAt = DateTime.UtcNow
        };

        var created = await _deviceRepository.CreateAsync(device);

        var response = new DeviceResponse
        {
            SerialNumber = created.SerialNumber,
            TenantId = created.TenantId,
            DeviceType = created.DeviceType,
            RegisteredAt = created.RegisteredAt,
            LastSeenAt = created.LastSeenAt
        };

        return CreatedAtAction(nameof(GetDevice), new { serialNumber = created.SerialNumber }, response);
    }

    /// <summary>
    /// Get device information by serial number
    /// </summary>
    [HttpGet("{serialNumber}")]
    public async Task<ActionResult<DeviceResponse>> GetDevice(string serialNumber)
    {
        var device = await _deviceRepository.GetBySerialNumberAsync(serialNumber);
        
        if (device == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        var response = new DeviceResponse
        {
            SerialNumber = device.SerialNumber,
            TenantId = device.TenantId,
            DeviceType = device.DeviceType,
            RegisteredAt = device.RegisteredAt,
            LastSeenAt = device.LastSeenAt
        };

        return Ok(response);
    }

    /// <summary>
    /// Get tenant ID for a device (used by Smart Gateway)
    /// </summary>
    [HttpGet("{serialNumber}/tenant")]
    public async Task<ActionResult<TenantIdResponse>> GetDeviceTenant(string serialNumber)
    {
        var device = await _deviceRepository.GetBySerialNumberAsync(serialNumber);
        
        if (device == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        return Ok(new TenantIdResponse { TenantId = device.TenantId });
    }

    /// <summary>
    /// Update device information
    /// </summary>
    [HttpPatch("{serialNumber}")]
    public async Task<ActionResult<DeviceResponse>> UpdateDevice(string serialNumber, [FromBody] UpdateDeviceRequest request)
    {
        var existing = await _deviceRepository.GetBySerialNumberAsync(serialNumber);
        
        if (existing == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        var updated = new Device
        {
            SerialNumber = existing.SerialNumber,
            TenantId = request.TenantId ?? existing.TenantId,
            DeviceType = request.DeviceType ?? existing.DeviceType,
            RegisteredAt = existing.RegisteredAt,
            LastSeenAt = DateTime.UtcNow
        };

        var result = await _deviceRepository.UpdateAsync(serialNumber, updated);

        if (result == null)
        {
            return NotFound(new { error = "Device not found" });
        }

        var response = new DeviceResponse
        {
            SerialNumber = result.SerialNumber,
            TenantId = result.TenantId,
            DeviceType = result.DeviceType,
            RegisteredAt = result.RegisteredAt,
            LastSeenAt = result.LastSeenAt
        };

        return Ok(response);
    }
}

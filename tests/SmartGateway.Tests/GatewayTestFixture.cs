using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SmartGateway.Tests;

/// <summary>
/// Test fixture that sets up a simulated gateway environment
/// Includes mock Device Registry, Tenant Catalog, and Backend services
/// </summary>
public class GatewayTestFixture : IAsyncLifetime
{
    private WebApplication? _deviceRegistryApp;
    private WebApplication? _tenantCatalogApp;
    private WebApplication? _backendApp;
    private HttpClient? _gatewayClient;

    private readonly ConcurrentDictionary<string, string> _deviceToTenant = new();
    private readonly ConcurrentDictionary<string, TenantConfig> _tenantConfigs = new();
    private readonly ConcurrentDictionary<string, string> _capturedHeaders = new();
    private CapturedRequest? _lastCapturedRequest;

    public HttpClient GatewayClient => _gatewayClient ?? throw new InvalidOperationException("Fixture not initialized");

    public async Task InitializeAsync()
    {
        // Start Device Registry mock
        _deviceRegistryApp = await StartDeviceRegistryAsync();

        // Start Tenant Catalog mock
        _tenantCatalogApp = await StartTenantCatalogAsync();

        // Start Backend mock
        _backendApp = await StartBackendAsync();

        // Create gateway client (in real scenario, this would point to NGINX)
        // For testing, we simulate the gateway behavior in .NET
        _gatewayClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:8080")
        };
    }

    public async Task DisposeAsync()
    {
        _gatewayClient?.Dispose();
        
        if (_deviceRegistryApp != null)
            await _deviceRegistryApp.DisposeAsync();
        
        if (_tenantCatalogApp != null)
            await _tenantCatalogApp.DisposeAsync();
        
        if (_backendApp != null)
            await _backendApp.DisposeAsync();
    }

    public async Task<WebApplication> StartDeviceRegistryAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:5001");
        
        var app = builder.Build();

        app.MapGet("/api/devices/{serialNumber}/tenant", (string serialNumber) =>
        {
            if (_deviceToTenant.TryGetValue(serialNumber, out var tenantId))
            {
                return Results.Ok(new { tenantId });
            }
            return Results.NotFound(new { error = "Device not found" });
        });

        app.MapPost("/api/devices", async (HttpContext context) =>
        {
            var request = await context.Request.ReadFromJsonAsync<RegisterDeviceRequest>();
            if (request == null)
                return Results.BadRequest();

            _deviceToTenant[request.SerialNumber] = request.TenantId;
            return Results.Ok(new { serialNumber = request.SerialNumber, tenantId = request.TenantId });
        });

        await app.StartAsync();
        return app;
    }

    public async Task<WebApplication> StartTenantCatalogAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:5002");
        
        var app = builder.Build();

        app.MapGet("/api/tenants/{id}", (string id) =>
        {
            if (_tenantConfigs.TryGetValue(id, out var config))
            {
                return Results.Ok(config);
            }
            return Results.NotFound();
        });

        await app.StartAsync();
        return app;
    }

    public async Task<WebApplication> StartBackendAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://localhost:8081");
        
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            // Capture headers
            _capturedHeaders.Clear();
            foreach (var header in context.Request.Headers)
            {
                _capturedHeaders[header.Key] = header.Value.ToString();
            }

            // Capture request details
            _lastCapturedRequest = new CapturedRequest
            {
                Method = context.Request.Method,
                Path = context.Request.Path,
                Headers = new Dictionary<string, string>(_capturedHeaders)
            };

            await next();
        });

        app.MapGet("/api/test", () => Results.Ok(new { message = "Test endpoint" }));
        app.MapPost("/api/transactions", () => Results.Ok(new { id = 1, status = "created" }));

        await app.StartAsync();
        return app;
    }

    public async Task RegisterDeviceAsync(string deviceId, string tenantId)
    {
        var client = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
        await client.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest
        {
            SerialNumber = deviceId,
            TenantId = tenantId,
            DeviceType = "medDispense"
        });

        // Also register tenant config
        _tenantConfigs[tenantId] = new TenantConfig
        {
            Id = tenantId,
            DisplayName = $"Tenant {tenantId}",
            Throttling = new ThrottlingConfig { Rps = 50 }
        };
    }

    public Task<Dictionary<string, string>> GetCapturedHeadersAsync()
    {
        return Task.FromResult(new Dictionary<string, string>(_capturedHeaders));
    }

    public Task<CapturedRequest?> GetCapturedRequestAsync()
    {
        return Task.FromResult(_lastCapturedRequest);
    }

    public async Task StopDeviceRegistryAsync()
    {
        if (_deviceRegistryApp != null)
        {
            await _deviceRegistryApp.StopAsync();
        }
    }

    public async Task RestartDeviceRegistryAsync()
    {
        if (_deviceRegistryApp != null)
        {
            await _deviceRegistryApp.StartAsync();
        }
    }
}

public class RegisterDeviceRequest
{
    public string SerialNumber { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string DeviceType { get; set; } = null!;
}

public class TenantConfig
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public ThrottlingConfig Throttling { get; set; } = null!;
}

public class ThrottlingConfig
{
    public int Rps { get; set; }
}

public class CapturedRequest
{
    public string Method { get; set; } = null!;
    public string Path { get; set; } = null!;
    public Dictionary<string, string> Headers { get; set; } = null!;
}

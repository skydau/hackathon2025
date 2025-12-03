using System.Net.Http.Json;

namespace AdminUI.Services;

public class TenantCatalogClient
{
    private readonly HttpClient _httpClient;

    public TenantCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TenantDto>> GetTenantsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tenants");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<TenantDto>>() ?? new List<TenantDto>();
        }
        catch
        {
            return new List<TenantDto>();
        }
    }

    public async Task<TenantDto?> GetTenantAsync(string id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TenantDto>($"/api/tenants/{id}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/tenants", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantDto>();
    }
}

public class TenantDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DatabaseConfigDto? DbConfig { get; set; }
    public ThrottlingConfigDto? Throttling { get; set; }
    public SloConfigDto? Slo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DatabaseConfigDto
{
    public string Mode { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string? Schema { get; set; }
}

public class ThrottlingConfigDto
{
    public int Rps { get; set; }
}

public class SloConfigDto
{
    public string Availability { get; set; } = string.Empty;
    public int P95LatencyMs { get; set; }
}

public class CreateTenantRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string DbMode { get; set; } = "perDatabase";
    public string DbServer { get; set; } = string.Empty;
    public string DbDatabase { get; set; } = string.Empty;
    public string? DbSchema { get; set; }
    public int ThrottlingRps { get; set; } = 100;
    public string? SloAvailability { get; set; }
    public int? SloP95LatencyMs { get; set; }
}

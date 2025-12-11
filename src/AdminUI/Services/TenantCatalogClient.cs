using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminUI.Services;

public enum TenantStatus
{
    Provisioning,
    Enabled,
    Disabled,
    Decommissioned
}

public class TenantCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public TenantCatalogClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        
        // 配置 JSON 序列化选项，确保枚举以字符串形式序列化
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async Task<List<TenantDto>> GetTenantsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tenants");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<TenantDto>>(_jsonOptions) ?? new List<TenantDto>();
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
            return await _httpClient.GetFromJsonAsync<TenantDto>($"/api/tenants/{id}", _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<TenantDto?> CreateTenantAsync(CreateTenantRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/tenants", request, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantDto>(_jsonOptions);
    }
}

public class TenantDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public TenantStatus Status { get; set; }
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
    public string CredentialRef { get; set; } = string.Empty;
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
    public DatabaseConfigDto DbConfig { get; set; } = new();
    public ThrottlingConfigDto? Throttling { get; set; }
    public SloConfigDto? Slo { get; set; }
}

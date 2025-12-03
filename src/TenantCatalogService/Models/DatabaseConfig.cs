namespace TenantCatalogService.Models;

public class DatabaseConfig
{
    public string Mode { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public string? Schema { get; set; }
    public string CredentialRef { get; set; } = string.Empty;
}

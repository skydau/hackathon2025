namespace TenantDBRouter.Models;

public class TenantDbConfigResponse
{
    public string TenantId { get; set; } = string.Empty;
    public DatabaseConfig DbConfig { get; set; } = new();
}

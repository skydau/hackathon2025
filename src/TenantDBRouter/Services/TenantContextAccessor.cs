namespace TenantDBRouter.Services;

public class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<string?> _tenantId = new();

    public string? TenantId
    {
        get => _tenantId.Value;
        set => _tenantId.Value = value;
    }
}

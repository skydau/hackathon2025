namespace TenantDBRouter.Services;

public interface ITenantContextAccessor
{
    string? TenantId { get; set; }
}

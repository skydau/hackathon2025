namespace TenantDBRouter.Exceptions;

public class TenantIdMissingException : Exception
{
    public TenantIdMissingException() 
        : base("X-Tenant-Id header is missing from the request context")
    {
    }

    public TenantIdMissingException(string message) : base(message)
    {
    }

    public TenantIdMissingException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}

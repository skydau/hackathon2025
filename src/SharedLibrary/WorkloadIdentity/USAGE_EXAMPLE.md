# Workload Identity Usage Examples

## ASP.NET Core Application Setup

### 1. Register Services in Program.cs

```csharp
using SharedLibrary.WorkloadIdentity;

var builder = WebApplication.CreateBuilder(args);

// Add Workload Identity services
builder.Services.AddWorkloadIdentity(builder.Configuration);

// Or specify Key Vault URL directly
// builder.Services.AddWorkloadIdentity("https://medlogic-kv.vault.azure.net/");

// Add other services
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure middleware
app.UseRouting();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

### 2. Access Key Vault Secrets

```csharp
using Microsoft.AspNetCore.Mvc;
using SharedLibrary.WorkloadIdentity;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly KeyVaultSecretProvider _secretProvider;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(
        KeyVaultSecretProvider secretProvider,
        ILogger<ConfigController> logger)
    {
        _secretProvider = secretProvider;
        _logger = logger;
    }

    [HttpGet("database-connection")]
    public async Task<IActionResult> GetDatabaseConnection()
    {
        try
        {
            // Retrieve database password from Key Vault
            var password = await _secretProvider.GetSecretAsync("database-password");
            
            // Use the password to build connection string
            var connectionString = $"Server=myserver;Database=mydb;User Id=myuser;Password={password}";
            
            _logger.LogInformation("Successfully retrieved database credentials");
            
            return Ok(new { status = "configured" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve database credentials");
            return StatusCode(500, new { error = "Failed to retrieve credentials" });
        }
    }
}
```

### 3. Connect to SQL Server with Azure AD Authentication

```csharp
using Microsoft.AspNetCore.Mvc;
using SharedLibrary.WorkloadIdentity;
using Microsoft.Data.SqlClient;

[ApiController]
[Route("api/[controller]")]
public class TenantsController : ControllerBase
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        SqlConnectionFactory connectionFactory,
        ILogger<TenantsController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetTenants()
    {
        try
        {
            // Create connection using Workload Identity (no password needed)
            using var connection = _connectionFactory.CreateConnection(
                "medlogic-sql.database.windows.net",
                "TenantCatalog"
            );

            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, DisplayName, Status FROM Tenants";

            var tenants = new List<object>();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                tenants.Add(new
                {
                    Id = reader.GetString(0),
                    DisplayName = reader.GetString(1),
                    Status = reader.GetString(2)
                });
            }

            return Ok(tenants);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve tenants");
            return StatusCode(500, new { error = "Failed to retrieve tenants" });
        }
    }
}
```

### 4. Using with Entity Framework Core

```csharp
using Microsoft.EntityFrameworkCore;
using SharedLibrary.WorkloadIdentity;

public class TenantCatalogDbContext : DbContext
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly string _server;
    private readonly string _database;

    public TenantCatalogDbContext(
        DbContextOptions<TenantCatalogDbContext> options,
        SqlConnectionFactory connectionFactory,
        IConfiguration configuration)
        : base(options)
    {
        _connectionFactory = connectionFactory;
        _server = configuration["SQL_SERVER"] ?? "medlogic-sql.database.windows.net";
        _database = configuration["SQL_DATABASE"] ?? "TenantCatalog";
    }

    public DbSet<Tenant> Tenants { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Create connection using Workload Identity
            var connection = _connectionFactory.CreateConnection(_server, _database);
            optionsBuilder.UseSqlServer(connection);
        }
    }
}

// Register in Program.cs
builder.Services.AddDbContext<TenantCatalogDbContext>();
```

### 5. Health Check with Workload Identity

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedLibrary.WorkloadIdentity;

public class WorkloadIdentityHealthCheck : IHealthCheck
{
    private readonly AzureCredentialProvider _credentialProvider;
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly KeyVaultSecretProvider _secretProvider;
    private readonly IConfiguration _configuration;

    public WorkloadIdentityHealthCheck(
        AzureCredentialProvider credentialProvider,
        SqlConnectionFactory connectionFactory,
        KeyVaultSecretProvider secretProvider,
        IConfiguration configuration)
    {
        _credentialProvider = credentialProvider;
        _connectionFactory = connectionFactory;
        _secretProvider = secretProvider;
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            // Check if Workload Identity is configured
            var isConfigured = _credentialProvider.IsWorkloadIdentityConfigured();
            data["workload_identity_configured"] = isConfigured;

            if (!isConfigured)
            {
                return HealthCheckResult.Degraded(
                    "Workload Identity not configured",
                    data: data);
            }

            // Test Key Vault access
            try
            {
                await _secretProvider.GetSecretAsync("health-check-secret", cancellationToken);
                data["key_vault_access"] = "ok";
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Secret not found is okay - we're testing authentication
                data["key_vault_access"] = "ok";
            }
            catch (Exception ex)
            {
                data["key_vault_access"] = "failed";
                data["key_vault_error"] = ex.Message;
                return HealthCheckResult.Unhealthy(
                    "Key Vault access failed",
                    ex,
                    data);
            }

            // Test SQL Server connection
            var sqlServer = _configuration["SQL_SERVER"] ?? "medlogic-sql.database.windows.net";
            var database = _configuration["SQL_DATABASE"] ?? "TenantCatalog";
            
            var canConnect = await _connectionFactory.TestConnectionAsync(
                sqlServer,
                database,
                cancellationToken);

            data["sql_server_access"] = canConnect ? "ok" : "failed";

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "SQL Server connection failed",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Workload Identity is functioning correctly",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Workload Identity health check failed",
                ex,
                data);
        }
    }
}

// Register in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<WorkloadIdentityHealthCheck>("workload_identity");
```

### 6. Configuration in appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "SharedLibrary.WorkloadIdentity": "Debug"
    }
  },
  "Azure": {
    "KeyVault": {
      "Url": "https://medlogic-kv.vault.azure.net/"
    }
  },
  "SQL_SERVER": "medlogic-sql.database.windows.net",
  "SQL_DATABASE": "TenantCatalog"
}
```

### 7. Environment Variables (Set by Kubernetes)

These are automatically injected when running in AKS with Workload Identity:

```bash
AZURE_CLIENT_ID=<app-registration-client-id>
AZURE_TENANT_ID=<azure-tenant-id>
AZURE_FEDERATED_TOKEN_FILE=/var/run/secrets/azure/tokens/azure-identity-token
```

## Testing Locally

For local development without Workload Identity, `DefaultAzureCredential` will fall back to:

1. Azure CLI credentials (if logged in with `az login`)
2. Visual Studio credentials
3. Environment variables (AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, AZURE_TENANT_ID)

### Local Development Setup

```bash
# Login with Azure CLI
az login

# Set environment variables for local testing
export KEY_VAULT_URL="https://medlogic-kv.vault.azure.net/"
export SQL_SERVER="medlogic-sql.database.windows.net"
export SQL_DATABASE="TenantCatalog"

# Run the application
dotnet run
```

## Best Practices

### 1. Always Use Dependency Injection

```csharp
// ✅ Good - Use DI
public class MyService
{
    private readonly KeyVaultSecretProvider _secretProvider;
    
    public MyService(KeyVaultSecretProvider secretProvider)
    {
        _secretProvider = secretProvider;
    }
}

// ❌ Bad - Don't create instances directly
public class MyService
{
    public void DoSomething()
    {
        var provider = new KeyVaultSecretProvider(...); // Don't do this
    }
}
```

### 2. Cache Secrets Appropriately

```csharp
// ✅ Good - KeyVaultSecretProvider has built-in caching
var secret = await _secretProvider.GetSecretAsync("my-secret");

// ❌ Bad - Don't fetch secrets on every request
[HttpGet]
public async Task<IActionResult> Get()
{
    var secret = await _secretProvider.GetSecretAsync("my-secret"); // Called on every request
    // ...
}

// ✅ Better - Cache at startup or use background service
public class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        // Fetch secrets once at startup
        var secretProvider = app.ApplicationServices.GetRequiredService<KeyVaultSecretProvider>();
        var secret = await secretProvider.GetSecretAsync("my-secret");
        // Store in configuration or singleton service
    }
}
```

### 3. Handle Errors Gracefully

```csharp
try
{
    var secret = await _secretProvider.GetSecretAsync("my-secret");
}
catch (Azure.RequestFailedException ex) when (ex.Status == 404)
{
    _logger.LogWarning("Secret not found: my-secret");
    // Use default value or fail gracefully
}
catch (Azure.RequestFailedException ex) when (ex.Status == 403)
{
    _logger.LogError("Access denied to Key Vault");
    // Check RBAC permissions
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to retrieve secret");
    throw;
}
```

### 4. Use Connection Pooling

```csharp
// ✅ Good - SqlConnectionFactory manages connections efficiently
using var connection = _connectionFactory.CreateConnection(server, database);
await connection.OpenAsync();
// Use connection
// Connection is automatically returned to pool when disposed

// ❌ Bad - Don't create new SqlConnection instances directly
using var connection = new SqlConnection(connectionString); // Bypasses factory
```

### 5. Monitor and Log

```csharp
_logger.LogInformation(
    "Accessing Key Vault secret: {SecretName}",
    secretName);

_logger.LogInformation(
    "Connecting to SQL Server: {Server}/{Database}",
    server,
    database);
```

## Troubleshooting

### Issue: "Unable to get token"

**Solution**: Verify Workload Identity configuration:
```bash
kubectl get sa -n platform-system <service-account> -o yaml
kubectl get pod -n platform-system <pod-name> -o yaml | grep azure.workload.identity
```

### Issue: "Access Denied" to Key Vault

**Solution**: Grant RBAC role:
```bash
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee <CLIENT_ID> \
  --scope <KEY_VAULT_ID>
```

### Issue: SQL Server connection fails

**Solution**: 
1. Enable Azure AD authentication on SQL Server
2. Create SQL user for the managed identity
3. Grant appropriate database roles

```sql
CREATE USER [app-name-workload-id] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [app-name-workload-id];
ALTER ROLE db_datawriter ADD MEMBER [app-name-workload-id];
```

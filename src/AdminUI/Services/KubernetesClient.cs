using System.Diagnostics;
using System.Text.Json;

namespace AdminUI.Services;

public class KubernetesClient
{
    private readonly ILogger<KubernetesClient> _logger;

    public KubernetesClient(ILogger<KubernetesClient> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CreateTenantCRDAsync(string tenantName, string displayName, string dbServer, string dbName)
    {
        try
        {
            var tenantYaml = $@"apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: {tenantName}
spec:
  displayName: ""{displayName}""
  db:
    mode: perDatabase
    server: ""{dbServer}""
    database: {dbName}
  throttling:
    rps: 100
  slo:
    availability: ""99.9%""
    p95_latency_ms: 1000";

            // Write YAML to temp file
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, tenantYaml);

            try
            {
                // Execute kubectl apply
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kubectl",
                        Arguments = $"apply -f {tempFile}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Successfully created Tenant CRD: {TenantName}, Output: {Output}", tenantName, output);
                    return true;
                }
                else
                {
                    _logger.LogError("Failed to create Tenant CRD: {TenantName}, Error: {Error}", tenantName, error);
                    return false;
                }
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating Tenant CRD: {TenantName}", tenantName);
            return false;
        }
    }

    public async Task<List<KubernetesTenantStatus>> GetTenantsAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kubectl",
                    Arguments = "get tenants -o json",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var tenantList = JsonSerializer.Deserialize<TenantList>(output, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return tenantList?.Items?.Select(t => new KubernetesTenantStatus
                {
                    Name = t.Metadata?.Name ?? "",
                    DisplayName = t.Spec?.DisplayName ?? "",
                    Phase = t.Status?.Phase ?? "Unknown",
                    Database = t.Spec?.Db?.Database ?? "",
                    CreatedAt = t.Metadata?.CreationTimestamp ?? DateTime.UtcNow
                }).ToList() ?? new List<KubernetesTenantStatus>();
            }
            else
            {
                _logger.LogError("Failed to get tenants: {Error}", error);
                return new List<KubernetesTenantStatus>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get tenants from Kubernetes");
            return new List<KubernetesTenantStatus>();
        }
    }
}

public class KubernetesTenantStatus
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TenantList
{
    public List<TenantItem>? Items { get; set; }
}

public class TenantItem
{
    public TenantMetadata? Metadata { get; set; }
    public TenantSpec? Spec { get; set; }
    public TenantStatusInfo? Status { get; set; }
}

public class TenantMetadata
{
    public string? Name { get; set; }
    public DateTime CreationTimestamp { get; set; }
}

public class TenantSpec
{
    public string? DisplayName { get; set; }
    public TenantDb? Db { get; set; }
}

public class TenantDb
{
    public string? Database { get; set; }
}

public class TenantStatusInfo
{
    public string? Phase { get; set; }
}
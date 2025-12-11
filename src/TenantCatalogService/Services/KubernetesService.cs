using System.Diagnostics;
using System.Text.Json;
using TenantCatalogService.Models;

namespace TenantCatalogService.Services;

public class KubernetesService : IKubernetesService
{
    private readonly ILogger<KubernetesService> _logger;

    public KubernetesService(ILogger<KubernetesService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> CreateTenantCRDAsync(Tenant tenant)
    {
        try
        {
            var tenantCRD = CreateTenantCRDManifest(tenant);
            var result = await ExecuteKubectlCommand($"apply -f -", tenantCRD);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully created Tenant CRD for tenant {TenantId}", tenant.Id);
                return true;
            }
            else
            {
                _logger.LogError("Failed to create Tenant CRD for tenant {TenantId}: {Error}", tenant.Id, result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while creating Tenant CRD for tenant {TenantId}", tenant.Id);
            return false;
        }
    }

    public async Task<bool> UpdateTenantCRDAsync(Tenant tenant)
    {
        try
        {
            var tenantCRD = CreateTenantCRDManifest(tenant);
            var result = await ExecuteKubectlCommand($"apply -f -", tenantCRD);
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully updated Tenant CRD for tenant {TenantId}", tenant.Id);
                return true;
            }
            else
            {
                _logger.LogError("Failed to update Tenant CRD for tenant {TenantId}: {Error}", tenant.Id, result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while updating Tenant CRD for tenant {TenantId}", tenant.Id);
            return false;
        }
    }

    public async Task<bool> DeleteTenantCRDAsync(string tenantId)
    {
        try
        {
            var result = await ExecuteKubectlCommand($"delete tenant {tenantId} --ignore-not-found=true");
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully deleted Tenant CRD for tenant {TenantId}", tenantId);
                return true;
            }
            else
            {
                _logger.LogError("Failed to delete Tenant CRD for tenant {TenantId}: {Error}", tenantId, result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while deleting Tenant CRD for tenant {TenantId}", tenantId);
            return false;
        }
    }

    private string CreateTenantCRDManifest(Tenant tenant)
    {
        // 生成数据库名称
        var dbName = !string.IsNullOrEmpty(tenant.DbDatabase) ? tenant.DbDatabase : $"{SanitizeName(tenant.DisplayName)}_DB";
        
        var manifest = $@"apiVersion: tenants.medlogic.io/v1
kind: Tenant
metadata:
  name: {SanitizeName(tenant.DisplayName.ToLower())}
spec:
  displayName: ""{tenant.DisplayName}""
  db:
    mode: ""perDatabase""
    server: ""172.19.112.1,1433""
    database: ""{dbName}""
  throttling:
    rps: {tenant.ThrottlingRps ?? 100}
  slo:
    availability: ""{tenant.SloAvailability ?? "99.9%"}""
    p95_latency_ms: {tenant.SloP95LatencyMs ?? 1000}
";

        return manifest;
    }

    private async Task<(bool Success, string Output, string Error)> ExecuteKubectlCommand(string arguments, string? input = null)
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = input != null,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                return (false, "", "Failed to start kubectl process");
            }

            // 如果有输入，写入stdin
            if (input != null)
            {
                await process.StandardInput.WriteAsync(input);
                process.StandardInput.Close();
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();

            var success = process.ExitCode == 0;
            return (success, output, error);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    private static string SanitizeName(string name)
    {
        // 移除特殊字符，只保留字母数字和连字符
        var sanitized = System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9-]", "-");
        // 移除前导和尾随连字符
        sanitized = sanitized.Trim('-');
        // 确保不为空
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "tenant";
        }
        return sanitized;
    }
}
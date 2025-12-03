using Serilog.Core;
using Serilog.Events;
using System.Net.Http.Json;
using System.Text.Json;

namespace SharedLibrary.Observability;

/// <summary>
/// Custom Serilog sink that sends logs to Loki with X-Scope-OrgID header for tenant isolation
/// </summary>
public class LokiHttpSink : ILogEventSink
{
    private readonly HttpClient _httpClient;
    private readonly string _lokiUrl;

    public LokiHttpSink(string lokiUrl)
    {
        _lokiUrl = lokiUrl;
        _httpClient = new HttpClient();
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            // Extract tenant_id from log properties
            var tenantId = "unknown";
            if (logEvent.Properties.TryGetValue("tenant_id", out var tenantIdProperty))
            {
                tenantId = tenantIdProperty.ToString().Trim('"');
            }

            // Build Loki push request
            var timestamp = new DateTimeOffset(logEvent.Timestamp.UtcDateTime).ToUnixTimeMilliseconds() * 1000000; // Convert to nanoseconds
            var message = logEvent.RenderMessage();
            
            var labels = new Dictionary<string, string>
            {
                ["tenant_id"] = tenantId,
                ["level"] = logEvent.Level.ToString(),
                ["app"] = "medlogic"
            };

            var stream = new
            {
                stream = labels,
                values = new[] { new[] { timestamp.ToString(), message } }
            };

            var payload = new { streams = new[] { stream } };

            // Send to Loki with X-Scope-OrgID header for tenant isolation
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_lokiUrl}/loki/api/v1/push");
            request.Headers.Add("X-Scope-OrgID", tenantId);
            request.Content = JsonContent.Create(payload);

            // Fire and forget - don't block logging
            _ = _httpClient.SendAsync(request);
        }
        catch
        {
            // Silently fail to avoid breaking the application
        }
    }
}

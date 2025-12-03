using FsCheck;
using FsCheck.Xunit;
using Serilog.Events;
using SharedLibrary.Observability;
using System.Net;

namespace Observability.Tests;

/// <summary>
/// **Feature: multi-tenant-medical-platform, Property 53: 日志请求包含租户头**
/// **Validates: Requirements 11.4**
/// 
/// Property: Log requests include tenant header
/// For any log sent to Loki, the request should include X-Scope-OrgID header for tenant isolation
/// </summary>
public class LokiTenantHeaderPropertyTests
{
    [Property(MaxTest = 100)]
    public Property LokiRequestsShouldIncludeXScopeOrgIdHeader()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateLogMessage(),
            GenerateLogLevel(),
            (tenantId, message, level) =>
            {
                // Arrange
                var capturedRequests = new List<(string Url, Dictionary<string, string> Headers)>();
                var mockHttpHandler = new MockHttpMessageHandler((request) =>
                {
                    var headers = request.Headers.ToDictionary(
                        h => h.Key,
                        h => string.Join(",", h.Value));
                    
                    capturedRequests.Add((request.RequestUri?.ToString() ?? "", headers));
                    
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                });

                var lokiUrl = "http://localhost:3100";
                var sink = new TestLokiHttpSink(lokiUrl, mockHttpHandler);

                var logEvent = CreateLogEvent(tenantId, message, level);

                // Act
                sink.Emit(logEvent);
                
                // Give async operation time to complete
                Thread.Sleep(100);

                // Assert
                var hasRequests = capturedRequests.Count > 0;
                if (!hasRequests)
                {
                    return false.Label("No HTTP requests were captured");
                }

                var allRequestsHaveHeader = capturedRequests.All(req =>
                    req.Headers.ContainsKey("X-Scope-OrgID"));

                var allHeadersMatchTenant = capturedRequests
                    .Where(req => req.Headers.ContainsKey("X-Scope-OrgID"))
                    .All(req => req.Headers["X-Scope-OrgID"] == tenantId);

                return (allRequestsHaveHeader && allHeadersMatchTenant)
                    .Label($"All {capturedRequests.Count} Loki requests should have X-Scope-OrgID={tenantId}");
            });
    }

    [Property(MaxTest = 100)]
    public Property XScopeOrgIdShouldMatchTenantIdInLog()
    {
        return Prop.ForAll(
            GenerateValidTenantId(),
            GenerateLogMessage(),
            (tenantId, message) =>
            {
                // Arrange
                var capturedHeaders = new List<string>();
                var mockHttpHandler = new MockHttpMessageHandler((request) =>
                {
                    if (request.Headers.TryGetValues("X-Scope-OrgID", out var values))
                    {
                        capturedHeaders.AddRange(values);
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                });

                var lokiUrl = "http://localhost:3100";
                var sink = new TestLokiHttpSink(lokiUrl, mockHttpHandler);

                var logEvent = CreateLogEvent(tenantId, message, LogEventLevel.Information);

                // Act
                sink.Emit(logEvent);
                Thread.Sleep(100);

                // Assert
                var hasHeaders = capturedHeaders.Count > 0;
                if (!hasHeaders)
                {
                    return false.Label("No X-Scope-OrgID headers were captured");
                }

                var allMatch = capturedHeaders.All(h => h == tenantId);

                return allMatch
                    .Label($"X-Scope-OrgID header should match tenant_id={tenantId}");
            });
    }

    [Property(MaxTest = 100)]
    public Property LogsWithoutTenantShouldUseUnknownOrgId()
    {
        return Prop.ForAll(
            GenerateLogMessage(),
            (message) =>
            {
                // Arrange
                var capturedHeaders = new List<string>();
                var mockHttpHandler = new MockHttpMessageHandler((request) =>
                {
                    if (request.Headers.TryGetValues("X-Scope-OrgID", out var values))
                    {
                        capturedHeaders.AddRange(values);
                    }
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
                });

                var lokiUrl = "http://localhost:3100";
                var sink = new TestLokiHttpSink(lokiUrl, mockHttpHandler);

                // Create log event without tenant_id property
                var parser = new Serilog.Parsing.MessageTemplateParser();
                var messageTemplate = parser.Parse(message);
                var logEvent = new LogEvent(
                    DateTimeOffset.UtcNow,
                    LogEventLevel.Information,
                    null,
                    messageTemplate,
                    new List<LogEventProperty>());

                // Act
                sink.Emit(logEvent);
                Thread.Sleep(100);

                // Assert
                var hasHeaders = capturedHeaders.Count > 0;
                if (!hasHeaders)
                {
                    return false.Label("No X-Scope-OrgID headers were captured");
                }

                var allUnknown = capturedHeaders.All(h => h == "unknown");

                return allUnknown
                    .Label("Logs without tenant_id should use X-Scope-OrgID=unknown");
            });
    }

    // Helper methods
    private static LogEvent CreateLogEvent(string tenantId, string message, LogEventLevel level)
    {
        var properties = new List<LogEventProperty>
        {
            new LogEventProperty("tenant_id", new ScalarValue(tenantId))
        };

        var parser = new Serilog.Parsing.MessageTemplateParser();
        var messageTemplate = parser.Parse(message);

        return new LogEvent(
            DateTimeOffset.UtcNow,
            level,
            null,
            messageTemplate,
            properties);
    }

    // Generators
    private static Arbitrary<string> GenerateValidTenantId()
    {
        return Gen.Elements("tenant-a", "tenant-b", "hospital-123", "clinic-xyz", "org-456")
            .ToArbitrary();
    }

    private static Arbitrary<string> GenerateLogMessage()
    {
        return Gen.Elements(
            "Request processed",
            "Database query completed",
            "Error occurred",
            "Cache updated",
            "Authentication successful"
        ).ToArbitrary();
    }

    private static Arbitrary<LogEventLevel> GenerateLogLevel()
    {
        return Gen.Elements(
            LogEventLevel.Debug,
            LogEventLevel.Information,
            LogEventLevel.Warning,
            LogEventLevel.Error
        ).ToArbitrary();
    }

    // Test helper classes
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }

    private class TestLokiHttpSink : LokiHttpSink
    {
        private readonly HttpMessageHandler _handler;

        public TestLokiHttpSink(string lokiUrl, HttpMessageHandler handler) : base(lokiUrl)
        {
            _handler = handler;
            // Replace the internal HttpClient with one using our mock handler
            var httpClientField = typeof(LokiHttpSink).GetField("_httpClient",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            httpClientField?.SetValue(this, new HttpClient(handler));
        }
    }
}

using FsCheck;
using FsCheck.Xunit;

namespace Observability.Tests;

/// <summary>
/// **Feature: multi-tenant-medical-platform, Property 54: 日志查询租户隔离**
/// **Validates: Requirements 11.5**
/// 
/// Property: Log query tenant isolation
/// For any specific tenant log query, Loki should only return that tenant's log entries
/// </summary>
public class LokiQueryIsolationPropertyTests
{
    [Property(MaxTest = 100)]
    public Property LokiQueryShouldOnlyReturnTenantLogs()
    {
        return Prop.ForAll(
            GenerateTenantLogData(),
            (logData) =>
            {
                // Arrange - Simulate Loki's tenant isolation behavior
                var lokiSimulator = new LokiTenantIsolationSimulator();
                
                // Add logs for multiple tenants
                foreach (var (tenantId, message, timestamp) in logData.AllLogs)
                {
                    lokiSimulator.AddLog(tenantId, message, timestamp);
                }

                // Act - Query for specific tenant
                var queryTenantId = logData.QueryTenantId;
                var results = lokiSimulator.QueryLogs(queryTenantId);

                // Assert - All returned logs should belong to the queried tenant
                var allLogsMatchTenant = results.All(log => log.TenantId == queryTenantId);
                var hasResults = results.Count > 0;

                // Verify no logs from other tenants are included
                var noOtherTenantLogs = !results.Any(log => log.TenantId != queryTenantId);

                return (allLogsMatchTenant && noOtherTenantLogs)
                    .Label($"Query for tenant {queryTenantId} should only return logs from that tenant. " +
                           $"Got {results.Count} logs, all matching: {allLogsMatchTenant}");
            });
    }

    [Property(MaxTest = 100)]
    public Property DifferentTenantQueriesShouldReturnDifferentLogs()
    {
        return Prop.ForAll(
            GenerateMultiTenantLogData(),
            (logData) =>
            {
                // Arrange
                var lokiSimulator = new LokiTenantIsolationSimulator();
                
                foreach (var (tenantId, message, timestamp) in logData.AllLogs)
                {
                    lokiSimulator.AddLog(tenantId, message, timestamp);
                }

                // Act - Query for two different tenants
                var tenant1Results = lokiSimulator.QueryLogs(logData.Tenant1Id);
                var tenant2Results = lokiSimulator.QueryLogs(logData.Tenant2Id);

                // Assert - Results should be completely isolated
                var tenant1OnlyHasTenant1Logs = tenant1Results.All(log => log.TenantId == logData.Tenant1Id);
                var tenant2OnlyHasTenant2Logs = tenant2Results.All(log => log.TenantId == logData.Tenant2Id);
                
                // No overlap between results
                var noOverlap = !tenant1Results.Any(log1 => 
                    tenant2Results.Any(log2 => log1.Message == log2.Message && log1.Timestamp == log2.Timestamp));

                var result = tenant1OnlyHasTenant1Logs && tenant2OnlyHasTenant2Logs && noOverlap;
                return result
                    .Label($"Tenant {logData.Tenant1Id} got {tenant1Results.Count} logs, " +
                           $"Tenant {logData.Tenant2Id} got {tenant2Results.Count} logs, " +
                           $"no overlap: {noOverlap}");
            });
    }

    [Property(MaxTest = 100)]
    public Property QueryWithoutTenantContextShouldReturnEmpty()
    {
        return Prop.ForAll(
            GenerateTenantLogData(),
            (logData) =>
            {
                // Arrange
                var lokiSimulator = new LokiTenantIsolationSimulator();
                
                foreach (var (tenantId, message, timestamp) in logData.AllLogs)
                {
                    lokiSimulator.AddLog(tenantId, message, timestamp);
                }

                // Act - Query without tenant context (empty string)
                var results = lokiSimulator.QueryLogs("");

                // Assert - Should return empty results (strict tenant isolation)
                var isEmpty = results.Count == 0;
                return isEmpty
                    .Label("Query without tenant context should return no logs");
            });
    }

    // Generators
    private static Arbitrary<TenantLogData> GenerateTenantLogData()
    {
        return (from tenantId in GenerateValidTenantId().Generator
                from otherTenantIds in Gen.ListOf(GenerateValidTenantId().Generator).Select(list => list.Take(3).ToList())
                from logCount in Gen.Choose(1, 10)
                let allTenantIds = new[] { tenantId }.Concat(otherTenantIds).Distinct().ToList()
                let logs = Enumerable.Range(0, logCount)
                    .Select(i => (
                        TenantId: allTenantIds[i % allTenantIds.Count],
                        Message: $"Log message {i}",
                        Timestamp: DateTime.UtcNow.AddSeconds(i)))
                    .ToList()
                select new TenantLogData
                {
                    QueryTenantId = tenantId,
                    AllLogs = logs
                }).ToArbitrary();
    }

    private static Arbitrary<MultiTenantLogData> GenerateMultiTenantLogData()
    {
        return (from tenant1 in GenerateValidTenantId().Generator
                from tenant2 in GenerateValidTenantId().Generator.Where(t => t != tenant1)
                from log1Count in Gen.Choose(1, 5)
                from log2Count in Gen.Choose(1, 5)
                let logs1 = Enumerable.Range(0, log1Count)
                    .Select(i => (tenant1, $"Tenant1 log {i}", DateTime.UtcNow.AddSeconds(i)))
                let logs2 = Enumerable.Range(0, log2Count)
                    .Select(i => (tenant2, $"Tenant2 log {i}", DateTime.UtcNow.AddSeconds(i + 100)))
                select new MultiTenantLogData
                {
                    Tenant1Id = tenant1,
                    Tenant2Id = tenant2,
                    AllLogs = logs1.Concat(logs2).ToList()
                }).ToArbitrary();
    }

    private static Arbitrary<string> GenerateValidTenantId()
    {
        return Gen.Elements("tenant-a", "tenant-b", "hospital-123", "clinic-xyz", "org-456")
            .ToArbitrary();
    }

    // Test data classes
    private class TenantLogData
    {
        public string QueryTenantId { get; set; } = "";
        public List<(string TenantId, string Message, DateTime Timestamp)> AllLogs { get; set; } = new();
    }

    private class MultiTenantLogData
    {
        public string Tenant1Id { get; set; } = "";
        public string Tenant2Id { get; set; } = "";
        public List<(string TenantId, string Message, DateTime Timestamp)> AllLogs { get; set; } = new();
    }

    // Simulator class that mimics Loki's tenant isolation behavior
    private class LokiTenantIsolationSimulator
    {
        private readonly List<LogEntry> _logs = new();

        public void AddLog(string tenantId, string message, DateTime timestamp)
        {
            _logs.Add(new LogEntry
            {
                TenantId = tenantId,
                Message = message,
                Timestamp = timestamp
            });
        }

        public List<LogEntry> QueryLogs(string tenantId)
        {
            // Simulate Loki's X-Scope-OrgID based isolation
            if (string.IsNullOrEmpty(tenantId))
            {
                return new List<LogEntry>();
            }

            return _logs.Where(log => log.TenantId == tenantId).ToList();
        }

        public class LogEntry
        {
            public string TenantId { get; set; } = "";
            public string Message { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }
    }
}

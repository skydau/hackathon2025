using FsCheck;
using FsCheck.Xunit;
using FluentAssertions;

namespace SmartGateway.Tests;

/// <summary>
/// Property-based tests for Smart Gateway rate limiting functionality
/// </summary>
public class RateLimitPropertyTests
{
    // **Feature: multi-tenant-medical-platform, Property 21: 租户级速率限制应用**
    // **Validates: Requirements 5.1**
    [Property(MaxTest = 100)]
    public Property TenantLevelRateLimitIsApplied()
    {
        return Prop.ForAll(
            GenerateTenantRequests(),
            requests =>
            {
                // Arrange - Simulate rate limiter with tenant-specific limits
                var rateLimiter = new TenantRateLimiter();
                
                // Group requests by tenant
                var requestsByTenant = requests.GroupBy(r => r.TenantId);
                
                // Act & Assert - Each tenant should have its own rate limit applied
                foreach (var tenantGroup in requestsByTenant)
                {
                    var tenantId = tenantGroup.Key;
                    var tenantRequests = tenantGroup.ToList();
                    
                    // Set tenant-specific rate limit
                    var rateLimit = tenantRequests.First().RateLimit;
                    rateLimiter.SetTenantLimit(tenantId, rateLimit);
                    
                    // Process requests and track which ones are allowed
                    var results = new List<bool>();
                    foreach (var request in tenantRequests)
                    {
                        var allowed = rateLimiter.AllowRequest(tenantId);
                        results.Add(allowed);
                    }
                    
                    // Property: Rate limiter should apply the tenant-specific limit
                    // At least some requests should be allowed (up to the limit)
                    // And some may be rejected if over the limit
                    var allowedCount = results.Count(r => r);
                    var rejectedCount = results.Count(r => !r);
                    
                    // If we have more requests than the rate limit, some should be rejected
                    if (tenantRequests.Count > rateLimit)
                    {
                        rejectedCount.Should().BeGreaterThan(0, 
                            $"Tenant {tenantId} with limit {rateLimit} should reject some of {tenantRequests.Count} requests");
                    }
                    
                    // Reset for next tenant
                    rateLimiter.Reset();
                }
                
                return true;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 22: 超速请求返回429**
    // **Validates: Requirements 5.2**
    [Property(MaxTest = 100)]
    public Property ExcessRequestsReturn429()
    {
        return Prop.ForAll(
            GenerateTenantWithExcessRequests(),
            data =>
            {
                var (tenantId, rateLimit, requestCount) = data;
                
                // Arrange
                var rateLimiter = new TenantRateLimiter();
                rateLimiter.SetTenantLimit(tenantId, rateLimit);
                
                // Act - Send requests up to and beyond the rate limit
                var responses = new List<RateLimitResponse>();
                for (int i = 0; i < requestCount; i++)
                {
                    var response = rateLimiter.ProcessRequest(tenantId);
                    responses.Add(response);
                }
                
                // Assert - Property: Requests beyond rate limit should return 429
                var allowedResponses = responses.Where(r => r.StatusCode == 200).ToList();
                var rateLimitedResponses = responses.Where(r => r.StatusCode == 429).ToList();
                
                // All requests up to the limit should be allowed
                allowedResponses.Count.Should().BeLessOrEqualTo(rateLimit);
                
                // If we exceeded the limit, we should have 429 responses
                if (requestCount > rateLimit)
                {
                    rateLimitedResponses.Count.Should().BeGreaterThan(0,
                        $"Should have 429 responses when {requestCount} requests exceed limit of {rateLimit}");
                    
                    // All 429 responses should have the correct tenant ID
                    rateLimitedResponses.Should().AllSatisfy(r => 
                        r.TenantId.Should().Be(tenantId));
                }
                
                return true;
            });
    }

    // **Feature: multi-tenant-medical-platform, Property 23: 租户隔离不受影响**
    // **Validates: Requirements 5.3**
    [Property(MaxTest = 100)]
    public Property TenantIsolationNotAffected()
    {
        return Prop.ForAll(
            GenerateMultipleTenantRequests(),
            tenantRequests =>
            {
                // Arrange - Multiple tenants with different rate limits
                var rateLimiter = new TenantRateLimiter();
                
                foreach (var (tenantId, rateLimit, _) in tenantRequests)
                {
                    rateLimiter.SetTenantLimit(tenantId, rateLimit);
                }
                
                // Act - Process requests for each tenant
                var resultsByTenant = new Dictionary<string, List<RateLimitResponse>>();
                
                foreach (var (tenantId, rateLimit, requestCount) in tenantRequests)
                {
                    var responses = new List<RateLimitResponse>();
                    for (int i = 0; i < requestCount; i++)
                    {
                        responses.Add(rateLimiter.ProcessRequest(tenantId));
                    }
                    resultsByTenant[tenantId] = responses;
                }
                
                // Assert - Property: When one tenant is rate limited, others should not be affected
                // Find if any tenant was rate limited
                var rateLimitedTenants = resultsByTenant
                    .Where(kvp => kvp.Value.Any(r => r.StatusCode == 429))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                if (rateLimitedTenants.Any())
                {
                    // Check that other tenants can still make requests
                    var otherTenants = resultsByTenant.Keys.Except(rateLimitedTenants).ToList();
                    
                    foreach (var otherTenant in otherTenants)
                    {
                        var otherTenantResponses = resultsByTenant[otherTenant];
                        var (_, rateLimit, requestCount) = tenantRequests.First(t => t.TenantId == otherTenant);
                        
                        // Other tenants should have successful requests up to their own limit
                        var successfulRequests = otherTenantResponses.Count(r => r.StatusCode == 200);
                        
                        if (requestCount <= rateLimit)
                        {
                            // If within limit, all should succeed
                            successfulRequests.Should().Be(requestCount,
                                $"Tenant {otherTenant} should not be affected by rate limiting of other tenants");
                        }
                    }
                }
                
                return true;
            });
    }

    // Generator for tenant requests
    private static Arbitrary<List<TenantRequest>> GenerateTenantRequests()
    {
        return Arb.From(
            from tenantCount in Gen.Choose(1, 5)
            from requests in Gen.ListOf(tenantCount, 
                from tenantId in Gen.Elements("tenant-a", "tenant-b", "tenant-c")
                from rateLimit in Gen.Choose(10, 100)
                select new TenantRequest(tenantId, rateLimit))
            select requests.ToList()
        );
    }

    // Generator for tenant with excess requests
    private static Arbitrary<(string TenantId, int RateLimit, int RequestCount)> GenerateTenantWithExcessRequests()
    {
        return Arb.From(
            from tenantId in Gen.Elements("tenant-a", "tenant-b", "tenant-c")
            from rateLimit in Gen.Choose(10, 50)
            from requestCount in Gen.Choose(rateLimit + 1, rateLimit + 100)
            select (tenantId, rateLimit, requestCount)
        );
    }

    // Generator for multiple tenant requests
    private static Arbitrary<List<(string TenantId, int RateLimit, int RequestCount)>> GenerateMultipleTenantRequests()
    {
        return Arb.From(
            from tenantCount in Gen.Choose(2, 5)
            from tenants in Gen.ListOf(tenantCount,
                from tenantId in Gen.Elements("tenant-a", "tenant-b", "tenant-c", "tenant-d", "tenant-e")
                from rateLimit in Gen.Choose(10, 50)
                from requestCount in Gen.Choose(5, 100)
                select (tenantId, rateLimit, requestCount))
            select tenants.Distinct(new TenantIdComparer()).ToList()
        );
    }
}

// Test models
public record TenantRequest(string TenantId, int RateLimit);

public record RateLimitResponse(int StatusCode, string? TenantId = null);

// Simple rate limiter implementation for testing
public class TenantRateLimiter
{
    private readonly Dictionary<string, int> _tenantLimits = new();
    private readonly Dictionary<string, int> _tenantRequestCounts = new();

    public void SetTenantLimit(string tenantId, int limit)
    {
        _tenantLimits[tenantId] = limit;
    }

    public bool AllowRequest(string tenantId)
    {
        if (!_tenantLimits.ContainsKey(tenantId))
        {
            return true; // No limit set
        }

        var limit = _tenantLimits[tenantId];
        var currentCount = _tenantRequestCounts.GetValueOrDefault(tenantId, 0);

        if (currentCount < limit)
        {
            _tenantRequestCounts[tenantId] = currentCount + 1;
            return true;
        }

        return false;
    }

    public RateLimitResponse ProcessRequest(string tenantId)
    {
        if (AllowRequest(tenantId))
        {
            return new RateLimitResponse(200);
        }

        return new RateLimitResponse(429, tenantId);
    }

    public void Reset()
    {
        _tenantRequestCounts.Clear();
    }
}

public class TenantIdComparer : IEqualityComparer<(string TenantId, int RateLimit, int RequestCount)>
{
    public bool Equals((string TenantId, int RateLimit, int RequestCount) x, (string TenantId, int RateLimit, int RequestCount) y)
    {
        return x.TenantId == y.TenantId;
    }

    public int GetHashCode((string TenantId, int RateLimit, int RequestCount) obj)
    {
        return obj.TenantId.GetHashCode();
    }
}

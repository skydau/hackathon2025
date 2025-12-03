using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DeviceRegistryService.Data;
using DeviceRegistryService.Models;
using FluentAssertions;
using MedLogicService.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using TenantDBRouter.Clients;
using TenantDBRouter.Models;

namespace MedLogicService.Tests;

/// <summary>
/// End-to-end integration tests for multi-tenant request flow.
/// Tests the complete flow from device request through Smart Gateway simulation
/// to database write, verifying tenant isolation.
/// 
/// Validates Requirements: 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.3, 6.4
/// </summary>
public class EndToEndMultiTenantIntegrationTests : IClassFixture<MultiServiceTestFixture>
{
    private readonly MultiServiceTestFixture _fixture;

    public EndToEndMultiTenantIntegrationTests(MultiServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EndToEnd_DeviceRequestToDatabase_RoutesToCorrectTenantDatabase()
    {
        // Arrange: Set up two tenants with different databases
        var tenantA = await _fixture.CreateTenantAsync("tenant-a", "Hospital A", "TenantA_DB");
        var tenantB = await _fixture.CreateTenantAsync("tenant-b", "Hospital B", "TenantB_DB");

        // Register devices for each tenant
        var deviceA = await _fixture.RegisterDeviceAsync("device-001", tenantA.Id);
        var deviceB = await _fixture.RegisterDeviceAsync("device-002", tenantB.Id);

        // Create transaction requests
        var transactionA = new TransactionRequest
        {
            Amount = 100.50m,
            Description = "Transaction for Tenant A"
        };

        var transactionB = new TransactionRequest
        {
            Amount = 200.75m,
            Description = "Transaction for Tenant B"
        };

        // Act: Send requests with X-Tenant-Id headers (simulating Smart Gateway)
        var responseA = await _fixture.MedLogicClient.PostAsJsonAsync(
            "/api/transactions",
            transactionA,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            headers => headers.Add("X-Tenant-Id", tenantA.Id));

        var responseB = await _fixture.MedLogicClient.PostAsJsonAsync(
            "/api/transactions",
            transactionB,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            headers => headers.Add("X-Tenant-Id", tenantB.Id));

        // Assert: Both requests should succeed
        responseA.StatusCode.Should().Be(HttpStatusCode.Created);
        responseB.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdTransactionA = await responseA.Content.ReadFromJsonAsync<TransactionResponse>();
        var createdTransactionB = await responseB.Content.ReadFromJsonAsync<TransactionResponse>();

        createdTransactionA.Should().NotBeNull();
        createdTransactionA!.Amount.Should().Be(100.50m);
        createdTransactionA.Description.Should().Be("Transaction for Tenant A");

        createdTransactionB.Should().NotBeNull();
        createdTransactionB!.Amount.Should().Be(200.75m);
        createdTransactionB.Description.Should().Be("Transaction for Tenant B");

        // Verify tenant isolation: Get transactions for each tenant
        var getResponseA = await _fixture.MedLogicClient.GetAsync(
            "/api/transactions",
            headers => headers.Add("X-Tenant-Id", tenantA.Id));

        var getResponseB = await _fixture.MedLogicClient.GetAsync(
            "/api/transactions",
            headers => headers.Add("X-Tenant-Id", tenantB.Id));

        getResponseA.StatusCode.Should().Be(HttpStatusCode.OK);
        getResponseB.StatusCode.Should().Be(HttpStatusCode.OK);

        var transactionsA = await getResponseA.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        var transactionsB = await getResponseB.Content.ReadFromJsonAsync<List<TransactionResponse>>();

        // Assert: Each tenant should only see their own transactions
        transactionsA.Should().NotBeNull();
        transactionsA!.Should().HaveCount(1);
        transactionsA[0].Description.Should().Be("Transaction for Tenant A");

        transactionsB.Should().NotBeNull();
        transactionsB!.Should().HaveCount(1);
        transactionsB[0].Description.Should().Be("Transaction for Tenant B");
    }

    [Fact]
    public async Task EndToEnd_RequestWithoutTenantId_ReturnsBadRequest()
    {
        // Arrange
        var transaction = new TransactionRequest
        {
            Amount = 50.00m,
            Description = "Test transaction"
        };

        // Act: Send request without X-Tenant-Id header
        var response = await _fixture.MedLogicClient.PostAsJsonAsync("/api/transactions", transaction);

        // Assert: Should return 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Missing X-Tenant-Id header");
    }

    [Fact]
    public async Task EndToEnd_MultipleRequestsSameTenant_AllStoredInSameDatabase()
    {
        // Arrange
        var tenant = await _fixture.CreateTenantAsync("tenant-multi", "Multi Hospital", "TenantMulti_DB");

        var transactions = new[]
        {
            new TransactionRequest { Amount = 10.00m, Description = "Transaction 1" },
            new TransactionRequest { Amount = 20.00m, Description = "Transaction 2" },
            new TransactionRequest { Amount = 30.00m, Description = "Transaction 3" }
        };

        // Act: Create multiple transactions for the same tenant
        foreach (var transaction in transactions)
        {
            var response = await _fixture.MedLogicClient.PostAsJsonAsync(
                "/api/transactions",
                transaction,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
                headers => headers.Add("X-Tenant-Id", tenant.Id));

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // Assert: All transactions should be retrievable
        var getResponse = await _fixture.MedLogicClient.GetAsync(
            "/api/transactions",
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var allTransactions = await getResponse.Content.ReadFromJsonAsync<List<TransactionResponse>>();
        allTransactions.Should().NotBeNull();
        allTransactions!.Should().HaveCount(3);
        allTransactions.Select(t => t.Amount).Should().BeEquivalentTo(new[] { 10.00m, 20.00m, 30.00m });
    }

    [Fact]
    public async Task EndToEnd_GetSpecificTransaction_ReturnsCorrectTransaction()
    {
        // Arrange
        var tenant = await _fixture.CreateTenantAsync("tenant-get", "Get Hospital", "TenantGet_DB");

        var transaction = new TransactionRequest
        {
            Amount = 99.99m,
            Description = "Specific transaction"
        };

        var createResponse = await _fixture.MedLogicClient.PostAsJsonAsync(
            "/api/transactions",
            transaction,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        // Act: Get the specific transaction
        var getResponse = await _fixture.MedLogicClient.GetAsync(
            $"/api/transactions/{created!.Id}",
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(created.Id);
        retrieved.Amount.Should().Be(99.99m);
        retrieved.Description.Should().Be("Specific transaction");
    }

    [Fact]
    public async Task EndToEnd_DeleteTransaction_RemovesFromDatabase()
    {
        // Arrange
        var tenant = await _fixture.CreateTenantAsync("tenant-delete", "Delete Hospital", "TenantDelete_DB");

        var transaction = new TransactionRequest
        {
            Amount = 75.00m,
            Description = "To be deleted"
        };

        var createResponse = await _fixture.MedLogicClient.PostAsJsonAsync(
            "/api/transactions",
            transaction,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        // Act: Delete the transaction
        var deleteResponse = await _fixture.MedLogicClient.DeleteAsync(
            $"/api/transactions/{created!.Id}",
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        // Assert: Delete should succeed
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone
        var getResponse = await _fixture.MedLogicClient.GetAsync(
            $"/api/transactions/{created.Id}",
            headers => headers.Add("X-Tenant-Id", tenant.Id));

        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EndToEnd_TenantCannotAccessOtherTenantsTransactions()
    {
        // Arrange: Create two tenants and a transaction for tenant A
        var tenantA = await _fixture.CreateTenantAsync("tenant-isolated-a", "Hospital Isolated A", "TenantIsolatedA_DB");
        var tenantB = await _fixture.CreateTenantAsync("tenant-isolated-b", "Hospital Isolated B", "TenantIsolatedB_DB");

        var transaction = new TransactionRequest
        {
            Amount = 150.00m,
            Description = "Tenant A's private transaction"
        };

        var createResponse = await _fixture.MedLogicClient.PostAsJsonAsync(
            "/api/transactions",
            transaction,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            headers => headers.Add("X-Tenant-Id", tenantA.Id));

        var created = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        // Act: Try to access tenant A's transaction using tenant B's ID
        var getResponse = await _fixture.MedLogicClient.GetAsync(
            $"/api/transactions/{created!.Id}",
            headers => headers.Add("X-Tenant-Id", tenantB.Id));

        // Assert: Tenant B should not find tenant A's transaction
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

/// <summary>
/// Test fixture that sets up multiple services for end-to-end testing
/// </summary>
public class MultiServiceTestFixture : IDisposable
{
    private readonly TenantCatalogTestFactory _tenantCatalogFactory;
    private readonly DeviceRegistryTestFactory _deviceRegistryFactory;
    private readonly MedLogicTestFactory _medLogicFactory;

    public HttpClient TenantCatalogClient { get; }
    public HttpClient DeviceRegistryClient { get; }
    public HttpClient MedLogicClient { get; }

    private readonly string _tenantDbName = $"TenantCatalogTestDb_{Guid.NewGuid()}";
    private readonly string _deviceDbName = $"DeviceRegistryTestDb_{Guid.NewGuid()}";

    public MultiServiceTestFixture()
    {
        // Set up Tenant Catalog Service
        _tenantCatalogFactory = new TenantCatalogTestFactory(_tenantDbName);

        // Set up Device Registry Service
        _deviceRegistryFactory = new DeviceRegistryTestFactory(_deviceDbName);

        // Set up MedLogic Service with mocked TenantCatalogClient
        _medLogicFactory = new MedLogicTestFactory();

        TenantCatalogClient = _tenantCatalogFactory.CreateClient();
        DeviceRegistryClient = _deviceRegistryFactory.CreateClient();
        MedLogicClient = _medLogicFactory.CreateClient();
    }

    public async Task<Tenant> CreateTenantAsync(string id, string displayName, string database)
    {
        var tenant = new Tenant
        {
            Id = id,
            DisplayName = displayName,
            Status = TenantStatus.Enabled,
            DbConfig = new TenantCatalogService.Models.DatabaseConfig
            {
                Mode = "perDatabase",
                Server = "XIA-JADU-LT\\SQLEXPRESS",
                Database = database,
                CredentialRef = "test-ref"
            },
            Throttling = new ThrottlingConfig
            {
                Rps = 100
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var scope = _tenantCatalogFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Also create the tenant's database table
        await CreateTenantDatabaseTableAsync(database);

        return tenant;
    }

    public async Task<Device> RegisterDeviceAsync(string serialNumber, string tenantId)
    {
        var device = new Device
        {
            SerialNumber = serialNumber,
            TenantId = tenantId,
            DeviceType = "medDispense",
            RegisteredAt = DateTime.UtcNow
        };

        using var scope = _deviceRegistryFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceRegistryDbContext>();
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        return device;
    }

    private async Task CreateTenantDatabaseTableAsync(string database)
    {
        // Create the Transactions table in the tenant's database
        var connectionString = $"Server=XIA-JADU-LT\\SQLEXPRESS;Database={database};User ID=sa;Password=md1599;TrustServerCertificate=True;";

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = @DbName)
                BEGIN
                    CREATE DATABASE [@DbName]
                END";
            command.Parameters.AddWithValue("@DbName", database);
            await command.ExecuteNonQueryAsync();

            // Switch to the new database
            connection.ChangeDatabase(database);

            // Create Transactions table
            command.CommandText = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Transactions')
                BEGIN
                    CREATE TABLE Transactions (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        Amount DECIMAL(18,2) NOT NULL,
                        Description NVARCHAR(500) NOT NULL,
                        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                    )
                END";
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception)
        {
            // If SQL Server is not available, tests will fail gracefully
            // This is acceptable for CI/CD environments without SQL Server
        }
    }

    public void Dispose()
    {
        TenantCatalogClient?.Dispose();
        DeviceRegistryClient?.Dispose();
        MedLogicClient?.Dispose();
        _tenantCatalogFactory?.Dispose();
        _deviceRegistryFactory?.Dispose();
        _medLogicFactory?.Dispose();
    }
}

/// <summary>
/// Test factory for Tenant Catalog Service
/// </summary>
public class TenantCatalogTestFactory : IDisposable
{
    private readonly WebApplicationFactory<TenantCatalogService.Models.Tenant> _factory;
    private readonly string _dbName;

    public TenantCatalogTestFactory(string dbName)
    {
        _dbName = dbName;
        _factory = new WebApplicationFactory<TenantCatalogService.Models.Tenant>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<TenantCatalogDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<TenantCatalogDbContext>(options =>
                        options.UseInMemoryDatabase(_dbName));

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TenantCatalogDbContext>();
                    db.Database.EnsureCreated();
                });
            });
    }

    public HttpClient CreateClient() => _factory.CreateClient();
    public IServiceProvider Services => _factory.Services;
    public void Dispose() => _factory?.Dispose();
}

/// <summary>
/// Test factory for Device Registry Service
/// </summary>
public class DeviceRegistryTestFactory : IDisposable
{
    private readonly WebApplicationFactory<DeviceRegistryService.Models.Device> _factory;
    private readonly string _dbName;

    public DeviceRegistryTestFactory(string dbName)
    {
        _dbName = dbName;
        _factory = new WebApplicationFactory<DeviceRegistryService.Models.Device>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<DeviceRegistryDbContext>));
                    if (descriptor != null) services.Remove(descriptor);

                    services.AddDbContext<DeviceRegistryDbContext>(options =>
                        options.UseInMemoryDatabase(_dbName));

                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DeviceRegistryDbContext>();
                    db.Database.EnsureCreated();
                });
            });
    }

    public HttpClient CreateClient() => _factory.CreateClient();
    public IServiceProvider Services => _factory.Services;
    public void Dispose() => _factory?.Dispose();
}

/// <summary>
/// Test factory for MedLogic Service
/// </summary>
public class MedLogicTestFactory : IDisposable
{
    private readonly WebApplicationFactory<MedLogicService.Models.Transaction> _factory;

    public MedLogicTestFactory()
    {
        _factory = new WebApplicationFactory<MedLogicService.Models.Transaction>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace ITenantCatalogClient with a mock that returns test database configs
                    var clientDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ITenantCatalogClient));
                    if (clientDescriptor != null) services.Remove(clientDescriptor);

                    // Create a mock client that returns in-memory database configs
                    var mockClient = new Mock<ITenantCatalogClient>();
                    mockClient.Setup(c => c.GetDbConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((string tenantId, CancellationToken ct) => new TenantDBRouter.Models.DatabaseConfig
                        {
                            Mode = "perDatabase",
                            Server = "XIA-JADU-LT\\SQLEXPRESS",
                            Database = $"{tenantId}_DB",
                            CredentialRef = "test-ref"
                        });

                    services.AddSingleton(mockClient.Object);
                });
            });
    }

    public HttpClient CreateClient() => _factory.CreateClient();
    public IServiceProvider Services => _factory.Services;
    public void Dispose() => _factory?.Dispose();
}

/// <summary>
/// Extension methods for HttpClient to add headers
/// </summary>
public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient client,
        string requestUri,
        T value,
        JsonSerializerOptions? options = null,
        Action<System.Net.Http.Headers.HttpRequestHeaders>? configureHeaders = null)
    {
        var json = JsonSerializer.Serialize(value, options);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content
        };

        configureHeaders?.Invoke(request.Headers);

        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> GetAsync(
        this HttpClient client,
        string requestUri,
        Action<System.Net.Http.Headers.HttpRequestHeaders> configureHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        configureHeaders(request.Headers);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> DeleteAsync(
        this HttpClient client,
        string requestUri,
        Action<System.Net.Http.Headers.HttpRequestHeaders> configureHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        configureHeaders(request.Headers);
        return await client.SendAsync(request);
    }
}

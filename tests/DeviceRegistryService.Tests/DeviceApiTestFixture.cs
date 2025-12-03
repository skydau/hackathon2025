using DeviceRegistryService.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeviceRegistryService.Tests;

public class DeviceApiTestFixture : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"DeviceRegistryTestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<DeviceRegistryDbContext>));
            
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using in-memory database for testing
            services.AddDbContext<DeviceRegistryDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            // Build the service provider
            var sp = services.BuildServiceProvider();

            // Create a scope to obtain a reference to the database context
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<DeviceRegistryDbContext>();

            // Ensure the database is created
            db.Database.EnsureCreated();
        });
    }

    public HttpClient CreateClientWithCleanDatabase()
    {
        // Clean the database
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeviceRegistryDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        
        return CreateClient();
    }
}

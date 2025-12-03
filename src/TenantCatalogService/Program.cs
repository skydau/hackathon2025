using Microsoft.EntityFrameworkCore;
using Serilog;
using TenantCatalogService.Data;
using TenantCatalogService.Models;
using TenantCatalogService.Repositories;
using TenantCatalogService.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tenant-catalog-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Tenant Catalog Service");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();

    // Add DbContext
    builder.Services.AddDbContext<TenantCatalogDbContext>(options =>
        options.UseInMemoryDatabase("TenantCatalog"));

    // Add repositories
    builder.Services.AddScoped<ITenantRepository, TenantRepository>();
    builder.Services.AddScoped<IResourceUsageRepository, ResourceUsageRepository>();

    // Add cost rates configuration
    builder.Services.AddSingleton(new CostRates
    {
        CpuPerCoreHour = 0.05,
        MemoryPerGbHour = 0.01,
        StoragePerGbMonth = 0.10,
        NetworkPerGb = 0.12
    });

    // Add services
    builder.Services.AddScoped<ICostCalculationService, CostCalculationService>();

    // Add health checks
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseSerilogRequestLogging();

    app.MapControllers();
    
    // Health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


// Make the implicit Program class public for testing
public partial class Program { }

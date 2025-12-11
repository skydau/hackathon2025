using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TenantCatalogService.Data;

namespace TenantCatalogService.Tests;

public class TenantApiTestFixture : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName;

    public TenantApiTestFixture()
    {
        _dbName = $"TestDb_{Guid.NewGuid()}";
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // 移除现有的 DbContext 注册
                    var dbContextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<TenantCatalogDbContext>));
                    if (dbContextDescriptor != null)
                    {
                        services.Remove(dbContextDescriptor);
                    }
                    
                    // 移除现有的 TenantCatalogDbContext 注册
                    var contextDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(TenantCatalogDbContext));
                    if (contextDescriptor != null)
                    {
                        services.Remove(contextDescriptor);
                    }
                    
                    // 添加新的 DbContext，使用唯一的内存数据库
                    services.AddDbContext<TenantCatalogDbContext>(options =>
                    {
                        options.UseInMemoryDatabase(_dbName);
                    });
                });
            });
    }

    public HttpClient CreateClient()
    {
        return _factory.CreateClient();
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}

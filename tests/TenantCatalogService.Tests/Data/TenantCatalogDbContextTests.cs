using Microsoft.EntityFrameworkCore;
using TenantCatalogService.Data;
using Xunit;

namespace TenantCatalogService.Tests.Data;

public class TenantCatalogDbContextTests
{
    [Fact]
    public void DbContext_CanBeCreated()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestTenantCatalogDb")
            .Options;

        // Act
        using var context = new TenantCatalogDbContext(options);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.Database);
    }

    [Fact]
    public void DbContext_CanConnect()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestTenantCatalogDb_Connect")
            .Options;

        // Act & Assert
        using var context = new TenantCatalogDbContext(options);
        Assert.True(context.Database.CanConnect());
    }

    [Fact]
    public void DbContext_ModelIsConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TenantCatalogDbContext>()
            .UseInMemoryDatabase(databaseName: "TestTenantCatalogDb_Model")
            .Options;

        // Act
        using var context = new TenantCatalogDbContext(options);
        var model = context.Model;

        // Assert
        Assert.NotNull(model);
    }
}

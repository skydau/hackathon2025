using Microsoft.EntityFrameworkCore;
using DeviceRegistryService.Data;
using Xunit;

namespace DeviceRegistryService.Tests.Data;

public class DeviceRegistryDbContextTests
{
    [Fact]
    public void DbContext_CanBeCreated()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DeviceRegistryDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDeviceRegistryDb")
            .Options;

        // Act
        using var context = new DeviceRegistryDbContext(options);

        // Assert
        Assert.NotNull(context);
        Assert.NotNull(context.Database);
    }

    [Fact]
    public void DbContext_CanConnect()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DeviceRegistryDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDeviceRegistryDb_Connect")
            .Options;

        // Act & Assert
        using var context = new DeviceRegistryDbContext(options);
        Assert.True(context.Database.CanConnect());
    }

    [Fact]
    public void DbContext_ModelIsConfigured()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<DeviceRegistryDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDeviceRegistryDb_Model")
            .Options;

        // Act
        using var context = new DeviceRegistryDbContext(options);
        var model = context.Model;

        // Assert
        Assert.NotNull(model);
    }
}

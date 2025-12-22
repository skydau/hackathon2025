using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MedDispenseSimulator.Services;

namespace MedDispenseSimulator;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🏥 MedDispense 配送站模拟器");
        Console.WriteLine("========================================");
        
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            var logger = host.Services.GetService<ILogger<Program>>();
            logger?.LogCritical(ex, "应用程序启动失败");
            throw;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // 注册 HttpClient
                services.AddHttpClient<MedDispenseSimulatorService>();
                
                // 注册后台服务
                services.AddHostedService<MedDispenseSimulatorService>();
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            });
}
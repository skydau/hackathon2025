using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace SharedLibrary.Observability;

/// <summary>
/// Extension methods for configuring tenant-aware observability
/// </summary>
public static class ObservabilityServiceExtensions
{
    /// <summary>
    /// Adds tenant-aware metrics collection
    /// </summary>
    public static IServiceCollection AddTenantMetrics(this IServiceCollection services, string serviceName)
    {
        services.AddSingleton(new TenantMetrics(serviceName));
        return services;
    }

    /// <summary>
    /// Adds tenant context and metrics middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseTenantObservability(this IApplicationBuilder app)
    {
        // Add tenant context middleware first to extract tenant ID
        app.UseMiddleware<TenantContextMiddleware>();
        
        // Add metrics middleware to record tenant-aware metrics
        app.UseMiddleware<TenantMetricsMiddleware>();
        
        return app;
    }

    /// <summary>
    /// Configures Serilog with tenant-aware enrichment
    /// </summary>
    public static LoggerConfiguration WithTenantEnrichment(this LoggerConfiguration loggerConfig)
    {
        return loggerConfig
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", "medlogic");
    }

    /// <summary>
    /// Adds Loki sink with tenant isolation support
    /// </summary>
    public static LoggerConfiguration WriteToLoki(this LoggerConfiguration loggerConfig, string lokiUrl)
    {
        return loggerConfig.WriteTo.Sink(new LokiHttpSink(lokiUrl));
    }
}

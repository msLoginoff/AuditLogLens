using AuditLog.Abstractions;
using AuditLog.Enrichment;
using AuditLog.Interceptors;
using AuditLog.Legacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLog;

public static class AuditExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAuditChangeDetector, EfAuditChangeDetector>();
        services.AddScoped<IAuditEnricher, AuditEnrichmentFacade>();
        services.AddScoped<IAuditWriter, LegacyEfAuditWriter>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        return services;
    }

    public static DbContextOptionsBuilder AddAuditInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider provider)
    {
        return builder.AddInterceptors(
            provider.GetRequiredService<AuditSaveChangesInterceptor>());
    }
}
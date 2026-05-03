using AuditLogLens.Abstractions;
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Domain;
using AuditLogLens.Interceptors;
using AuditLogLens.Legacy;
using AuditLogLens.Writer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditLogLens;

public static class AuditExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAuditDomainEnrichmentPlanProvider, StaticAuditDomainEnrichmentPlanProvider>();
        services.AddScoped<AuditEntityEnricherRegistry>();
        services.AddScoped<IAuditChangeDetector, EfAuditChangeDetector>();
        services.AddScoped<IAuditEnricher, AuditEnrichmentFacade>();
        services.AddScoped<IAuditWriter, UnconfiguredAuditWriter>();
        services.AddSingleton<AuditSaveChangesSuppressor>();
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

    public static IServiceCollection AddEfAuditWriter<TAuditEntry, TAuditEntryMapper>(
        this IServiceCollection services)
        where TAuditEntry : class
        where TAuditEntryMapper : class, IAuditEntryMapper<TAuditEntry>
    {
        services.AddScoped<IAuditEntryMapper<TAuditEntry>, TAuditEntryMapper>();
        services.AddScoped<IAuditWriter, EfAuditWriter<TAuditEntry>>();

        return services;
    }
}
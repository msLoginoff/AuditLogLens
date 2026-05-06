using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Interceptors;
using AuditLogLens.Restrictions;
using AuditLogLens.Restrictions.Internal;
using AuditLogLens.Writing.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditLogLens;

public static class AuditExtensions
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services)
    {
        return services.AddAuditInfrastructure(_ => { });
    }

    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        Action<AuditOptions> configure)
    {
        services.AddSingleton<IAuditDomainEnrichmentPlanProvider, StaticAuditDomainEnrichmentPlanProvider>();
        services.AddScoped<AuditEntityEnricherRegistry>();
        services.AddScoped<IAuditChangeDetector, EfAuditChangeDetector>();
        services.AddScoped<IAuditEnricher, AuditEnrichmentFacade>();
        services.TryAddSingleton<IAuditRestrictions, DefaultAuditRestrictions>();
        services.TryAddScoped<IAuditEntryMapper<AuditLogLensEntry>, DefaultAuditLogLensEntryMapper>();
        services.TryAddScoped<IAuditWriter, EfAuditWriter<AuditLogLensEntry>>();
        services.AddSingleton<AuditSaveChangesSuppressor>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        var options = new AuditOptions();
        configure(options);
        services.AddSingleton(options);

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

    public static IServiceCollection AddAuditRestrictions<TAuditRestrictions>(
        this IServiceCollection services)
        where TAuditRestrictions : AuditRestrictionsBase
    {
        services.Replace(ServiceDescriptor.Singleton<IAuditRestrictions, TAuditRestrictions>());

        return services;
    }
}
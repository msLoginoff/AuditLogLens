using AuditLogLens.Configuration;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Internal;
using AuditLogLens.Enrichment.Internal.Planning;
using AuditLogLens.Interceptors;
using AuditLogLens.Manual;
using AuditLogLens.Pipeline;
using AuditLogLens.Pipeline.Internal;
using AuditLogLens.Restrictions;
using AuditLogLens.Restrictions.Internal;
using AuditLogLens.Writing;
using AuditLogLens.Writing.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditLogLens;

/// <summary>
/// Provides service registration and EF Core setup helpers for AuditLogLens.
/// </summary>
public static class AuditExtensions
{
    /// <summary>
    /// Adds AuditLogLens services with default options.
    /// </summary>
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services)
    {
        return services.AddAuditInfrastructure(_ => { });
    }

    /// <summary>
    /// Adds AuditLogLens services and configures audit options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The delegate used to configure audit options.</param>
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        Action<AuditOptions> configure)
    {
        services.AddSingleton<IDomainEnrichmentPlanProvider, StaticDomainEnrichmentPlanProvider>();
        services.AddScoped<AuditEntityEnricherRegistry>();
        services.AddScoped<AuditEnrichmentPlanResolver>();
        services.AddScoped<IAuditChangeDetector, EfAuditChangeDetector>();
        services.AddScoped<CollectionParentChangePromoter>();
        services.AddScoped<IAuditEnricher, AuditEnrichmentFacade>();
        services.TryAddScoped<IAuditPipeline, AuditPipeline>();
        services.TryAddSingleton<IAuditChangeFactory, AuditChangeFactory>();
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

    /// <summary>
    /// Adds the AuditLogLens SaveChanges interceptor to a DbContext options builder.
    /// </summary>
    /// <param name="builder">The DbContext options builder.</param>
    /// <param name="provider">The service provider used to resolve the interceptor.</param>
    public static DbContextOptionsBuilder AddAuditInterceptor(
        this DbContextOptionsBuilder builder,
        IServiceProvider provider)
    {
        return builder.AddInterceptors(
            provider.GetRequiredService<AuditSaveChangesInterceptor>());
    }

    /// <summary>
    /// Registers an EF Core audit writer that stores records as the specified audit entity type.
    /// </summary>
    /// <typeparam name="TAuditEntry">The EF entity type used to store audit records.</typeparam>
    /// <typeparam name="TAuditEntryMapper">The mapper that creates audit entries.</typeparam>
    public static IServiceCollection AddEfAuditWriter<TAuditEntry, TAuditEntryMapper>(
        this IServiceCollection services)
        where TAuditEntry : class
        where TAuditEntryMapper : class, IAuditEntryMapper<TAuditEntry>
    {
        services.AddScoped<IAuditEntryMapper<TAuditEntry>, TAuditEntryMapper>();
        services.AddScoped<IAuditWriter, EfAuditWriter<TAuditEntry>>();

        return services;
    }

    /// <summary>
    /// Replaces the default audit restrictions with an application-defined restrictions type.
    /// </summary>
    /// <typeparam name="TAuditRestrictions">The restrictions type to use.</typeparam>
    public static IServiceCollection AddAuditRestrictions<TAuditRestrictions>(
        this IServiceCollection services)
        where TAuditRestrictions : AuditRestrictionsBase
    {
        services.Replace(ServiceDescriptor.Singleton<IAuditRestrictions, TAuditRestrictions>());

        return services;
    }

    /// <summary>
    /// Adds an application enricher to the audit enrichment pipeline.
    /// </summary>
    /// <typeparam name="TEnricher">The enricher type to add.</typeparam>
    public static IServiceCollection AddAuditEnricher<TEnricher>(
        this IServiceCollection services)
        where TEnricher : AuditEntityEnricherBase
    {
        services.AddScoped<IAuditEntityEnricher, TEnricher>();

        return services;
    }
}

using System.Collections.Concurrent;
using System.Reflection;
using AuditLogLens.Enrichment.Planning;

namespace AuditLogLens.Enrichment.Domain;

public sealed class StaticAuditDomainEnrichmentPlanProvider : IAuditDomainEnrichmentPlanProvider
{
    private static readonly MethodInfo BuildPlanGenericMethod =
        typeof(StaticAuditDomainEnrichmentPlanProvider)
            .GetMethod(nameof(BuildPlanGeneric), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Method {nameof(BuildPlanGeneric)} was not found.");

    private readonly ConcurrentDictionary<Type, AuditEnrichmentPlan> _cache = new();

    public AuditEnrichmentPlan GetPlan(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        return _cache.GetOrAdd(entityType, BuildPlanForType);
    }

    private AuditEnrichmentPlan BuildPlanForType(Type entityType)
    {
        var auditConfigInterface = entityType
            .GetInterfaces()
            .FirstOrDefault(x =>
                x.IsGenericType
                && x.GetGenericTypeDefinition() == typeof(IHasAuditEnrichmentConfig<>));

        if (auditConfigInterface is null)
        {
            return AuditEnrichmentPlan.Empty;
        }

        var selfType = auditConfigInterface.GetGenericArguments()[0];
        if (selfType != entityType)
        {
            throw new InvalidOperationException(
                $"Type {entityType.FullName} must implement IHasAuditEnrichmentConfig<TSelf> with itself as TSelf.");
        }

        var closedMethod = BuildPlanGenericMethod.MakeGenericMethod(entityType);

        return (AuditEnrichmentPlan)(closedMethod.Invoke(null, null)
                                     ?? throw new InvalidOperationException(
                                         $"Failed to build audit enrichment plan for {entityType.FullName}."));
    }

    private static AuditEnrichmentPlan BuildPlanGeneric<TSelf>()
        where TSelf : IHasAuditEnrichmentConfig<TSelf>
    {
        var builder = new AuditEnrichmentPlanBuilder();
        TSelf.ConfigureAuditEnrichment(builder);
        return builder.Build();
    }
}
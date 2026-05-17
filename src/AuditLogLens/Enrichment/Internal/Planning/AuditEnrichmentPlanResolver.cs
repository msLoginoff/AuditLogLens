namespace AuditLogLens.Enrichment.Internal.Planning;

internal sealed class AuditEnrichmentPlanResolver
{
    private readonly IAuditDomainEnrichmentPlanProvider _domainPlanProvider;
    private readonly AuditEntityEnricherRegistry _enricherRegistry;

    public AuditEnrichmentPlanResolver(
        IAuditDomainEnrichmentPlanProvider domainPlanProvider,
        AuditEntityEnricherRegistry enricherRegistry)
    {
        _domainPlanProvider = domainPlanProvider;
        _enricherRegistry = enricherRegistry;
    }

    public AuditEnrichmentPlan GetPlan(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        var builder = new AuditEnrichmentPlanBuilder()
            .Merge(_domainPlanProvider.GetPlan(entityType));

        foreach (var enricher in _enricherRegistry.GetEnrichersFor(entityType))
        {
            enricher.Configure(builder);
        }

        return builder.Build();
    }
}
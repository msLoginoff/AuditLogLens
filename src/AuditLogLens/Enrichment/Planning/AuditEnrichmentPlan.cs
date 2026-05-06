namespace AuditLogLens.Enrichment;

public sealed class AuditEnrichmentPlan
{
    public static AuditEnrichmentPlan Empty { get; } = new(
        rules: Array.Empty<EnrichmentRule>(),
        customSteps: Array.Empty<Action<AuditEnrichmentContext>>());

    public AuditEnrichmentPlan(
        IReadOnlyCollection<EnrichmentRule> rules,
        IReadOnlyCollection<Action<AuditEnrichmentContext>> customSteps)
    {
        Rules = rules;
        CustomSteps = customSteps;
    }

    public IReadOnlyCollection<EnrichmentRule> Rules { get; }

    public IReadOnlyCollection<Action<AuditEnrichmentContext>> CustomSteps { get; }

    public bool IsEmpty => Rules.Count == 0 && CustomSteps.Count == 0;
}
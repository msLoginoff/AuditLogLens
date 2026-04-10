namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentPlan
{
    public static AuditEnrichmentPlan Empty { get; } = new(
        requiredEntityTypes: Array.Empty<Type>(),
        rules: Array.Empty<EnrichmentRule>(),
        customSteps: Array.Empty<Action<AuditEnrichmentContext>>());

    public AuditEnrichmentPlan(
        IReadOnlyCollection<Type> requiredEntityTypes,
        IReadOnlyCollection<EnrichmentRule> rules,
        IReadOnlyCollection<Action<AuditEnrichmentContext>> customSteps)
    {
        RequiredEntityTypes = requiredEntityTypes;
        Rules = rules;
        CustomSteps = customSteps;
    }

    public IReadOnlyCollection<Type> RequiredEntityTypes { get; }

    public IReadOnlyCollection<EnrichmentRule> Rules { get; }

    public IReadOnlyCollection<Action<AuditEnrichmentContext>> CustomSteps { get; }

    public bool IsEmpty =>
        RequiredEntityTypes.Count == 0
        && Rules.Count == 0
        && CustomSteps.Count == 0;
}
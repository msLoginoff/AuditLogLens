namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentPlanBuilder : IAuditEnrichmentPlanBuilder
{
    private readonly HashSet<Type> _requiredEntityTypes = new();
    private readonly List<EnrichmentRule> _rules = new();
    private readonly List<Action<AuditEnrichmentContext>> _customSteps = new();

    public void RequireEntityType(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        _requiredEntityTypes.Add(entityType);
    }

    public void AddRule(EnrichmentRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
    }

    public void AddCustomStep(Action<AuditEnrichmentContext> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _customSteps.Add(step);
    }

    public AuditEnrichmentPlan Build()
    {
        return new AuditEnrichmentPlan(
            requiredEntityTypes: _requiredEntityTypes.ToList(),
            rules: _rules.ToList(),
            customSteps: _customSteps.ToList());
    }
}
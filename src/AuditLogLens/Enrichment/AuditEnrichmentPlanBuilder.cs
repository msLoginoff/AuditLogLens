namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentPlanBuilder : IAuditEnrichmentPlanBuilder
{
    private readonly List<EnrichmentRule> _rules = new();
    private readonly List<Action<AuditEnrichmentContext>> _customSteps = new();

    public IAuditEnrichmentPlanBuilder AddRule(EnrichmentRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    public IAuditEnrichmentPlanBuilder AddCustomStep(Action<AuditEnrichmentContext> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _customSteps.Add(step);
        return this;
    }

    internal AuditEnrichmentPlanBuilder Merge(AuditEnrichmentPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        foreach (var rule in plan.Rules)
            _rules.Add(rule);

        foreach (var step in plan.CustomSteps)
            _customSteps.Add(step);

        return this;
    }

    internal AuditEnrichmentPlan Build()
    {
        return new AuditEnrichmentPlan(
            rules: _rules.ToList(),
            customSteps: _customSteps.ToList());
    }
}
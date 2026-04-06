namespace AuditLog.Enrichment;

public sealed class AuditEnrichmentPlan
{
    private readonly HashSet<Type> _requiredEntityTypes = new();

    public IReadOnlyCollection<Type> RequiredEntityTypes => _requiredEntityTypes;

    public void RequireEntityType(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        _requiredEntityTypes.Add(entityType);
    }
}
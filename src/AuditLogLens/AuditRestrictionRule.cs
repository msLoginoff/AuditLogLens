namespace AuditLog;

public class AuditRestrictionRule
{
    public required string AllowedTable { get; init; }

    public IReadOnlyCollection<string> ForbiddenProperties { get; init; }
        = [];
}
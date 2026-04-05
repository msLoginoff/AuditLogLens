using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog.Tests.TestObjects;

public sealed class TestAuditRestrictions : AuditRestrictionsBase
{
    protected override IReadOnlyCollection<AuditRestrictionRule> Rules =>
    [
        new AuditRestrictionRule
        {
            AllowedTable = nameof(AllowedEntity),
            ForbiddenProperties = [nameof(AllowedEntity.Secret)]
        },
        new AuditRestrictionRule
        {
            AllowedTable = nameof(SpecialDeleteEntity),
            ForbiddenProperties = []
        }
    ];

    public override bool IsAllowedEntry(EntityEntry entry)
    {
        if (!base.IsAllowedEntry(entry))
        {
            return false;
        }

        return entry is not { Entity: SpecialDeleteEntity, State: EntityState.Deleted };
    }
}
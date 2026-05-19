using AuditLogLens.Restrictions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLogLens.Tests.TestObjects;

public sealed class TestAuditRestrictions : AuditRestrictionsBase
{
    protected override void Configure(AuditRestrictionRules rules)
    {
        rules.For<AllowedEntity>()
            .Ignore(x => x.Secret);

        rules.For<CollectionParentEntity>();

        rules.For<PolymorphicVisitEvent>();

        rules.For<RelatedEntity>();

        rules.For<SpecialDeleteEntity>();
    }

    protected override bool ShouldAuditEntry(EntityEntry entry)
    {
        return entry is not { Entity: SpecialDeleteEntity, State: EntityState.Deleted };
    }
}
# Restrictions

Restrictions decide what AuditLogLens should audit.

They answer two questions:

- Which entity types are audited?
- Which properties should be ignored?

## Basic Restrictions

Create a class that inherits from `AuditRestrictionsBase`.

```csharp
using AuditLogLens.Restrictions;

public sealed class ApplicationAuditRestrictions : AuditRestrictionsBase
{
    protected override void Configure(AuditRestrictionRules rules)
    {
        rules.For<Patient>()
            .Ignore(x => x.InternalNote)
            .Ignore(x => x.SecurityStamp);

        rules.For<Visit>();
    }
}
```

Then register it:

```csharp
services.AddAuditRestrictions<ApplicationAuditRestrictions>();
```

This is the important part. In application code you should not register AuditLogLens internal services directly.

## Default Behavior

If no rules are configured, nothing is audited.

This is the safe default. Audit logging should be explicit, so an application does not accidentally write sensitive tables.

If at least one rule is configured, only listed entities are allowed.

```csharp
rules.For<Patient>();
rules.For<Visit>();
```

In this example, only `Patient` and `Visit` are audited.

## Ignoring Properties

Use expressions when possible:

```csharp
rules.For<Patient>()
    .Ignore(x => x.InternalNote);
```

Use strings only when you need dynamic names:

```csharp
rules.For("Patient")
    .Ignore("InternalNote");
```

## Runtime Conditions

Override `ShouldAuditEntry` when the decision depends on the EF state or the entity instance.

```csharp
protected override bool ShouldAuditEntry(EntityEntry entry)
{
    if (entry is { Entity: SystemLog, State: EntityState.Deleted })
        return false;

    return true;
}
```

`Detached` and `Unchanged` entries are always skipped by the base class.

## Common Setup

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

## Related Pages

- [Getting Started](getting-started.md)
- [Writing Audit Records](writing.md)

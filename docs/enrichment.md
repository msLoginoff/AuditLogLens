# Enrichment

Enrichment is the main feature of AuditLogLens.

It adds readable values to audit changes. For example, instead of storing only `DoctorId = 42`, you can also store `DoctorName = "Dr. Smith"`.

AuditLogLens loads enrichment data in batches. It collects load requests from all changes first, groups them by entity and key, and only then queries the database. This is designed to avoid the common N+1 shape.

## Domain Enrichment

Use domain enrichment when the rule naturally belongs to one domain entity.

```csharp
using AuditLogLens.Enrichment;

public sealed class Visit : IHasAuditEnrichmentConfig<Visit>
{
    public int DoctorId { get; set; }

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Reference<Visit, Doctor, int>(
            x => x.DoctorId,
            "DoctorName",
            doctor => doctor.FullName);
    }
}
```

This rule says:

- Source entity: `Visit`.
- Source property: `Visit.DoctorId`.
- Target entity: `Doctor`.
- Target key: `Doctor.Id` by default.
- Added readable field: `DoctorName`.

## Explicit Target Key

If the target entity does not use `Id` as the key property, pass the target key explicitly:

```csharp
builder.Reference<Visit, Doctor, Guid>(
    visit => visit.DoctorPublicId,
    doctor => doctor.PublicId,
    "DoctorName",
    doctor => doctor.FullName);
```

## Collection Enrichment

Use collection enrichment for explicit join entities, such as `PatientTag`, `VisitResource`, or `UserRole`.

```csharp
builder.Collection<Patient, PatientTag, Tag>(
    joinParentKey: join => join.PatientId,
    joinItemKey: join => join.TagId,
    fieldName: "Tags",
    itemValueSelector: tag => tag.Name);
```

This short form uses `Id` as the key property for the parent and item entities.

If the parent or item entity does not use `Id`, pass keys explicitly:

```csharp
builder.Collection<Patient, PatientTag, Tag, int, int>(
    parentKey: patient => patient.Id,
    joinParentKey: join => join.PatientId,
    joinItemKey: join => join.TagId,
    itemKey: tag => tag.Id,
    fieldName: "Tags",
    itemValueSelector: tag => tag.Name);
```

Terms:

- `TSource`: the audited parent entity, for example `Patient`.
- `TJoin`: the explicit join entity, for example `PatientTag`.
- `TItem`: the readable item entity, for example `Tag`.

Limitations in the current version:

- EF Core implicit skip-navigation many-to-many relationships without a CLR join entity are not supported yet.
- The parent audit change must already exist; collection enrichment does not yet create synthetic parent changes.
- `itemValueSelector` should return a value with meaningful equality, such as `string`, `int`, a record, or an anonymous type. Collection values are de-duplicated with `Distinct()`.

## Application Enrichers

Use an application enricher when the data does not belong to one domain entity, or when it depends on application services.

Application enrichers should inherit from `AuditEntityEnricherBase`. The base class owns the template method and flushes enrichment bags into `AuditChange`.

```csharp
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Context;

public sealed class AuditMetadataEnricher : AuditEntityEnricherBase
{
    public override bool CanHandle(Type entityType) => true;

    protected override Task ApplyCustomAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var change in context.Changes)
        {
            var bag = context.GetBagForChange(change);

            bag.SetExtraValue("TenantId", ResolveTenantId(change));
            bag.SetExtraValue("UserId", ResolveUserId());
        }

        return Task.CompletedTask;
    }
}
```

Register it with DI:

```csharp
services.AddAuditEnricher<AuditMetadataEnricher>();
```

## Old, New, and Extra Values

Enrichment can write to three places:

- `OldValues`: readable value for the old audited value.
- `NewValues`: readable value for the new audited value.
- `ExtraValues`: metadata or additional fields that are not part of old/new values.

Use `ExtraValues` for values such as tenant id, user id, patient id, correlation id, or any other application-specific data.

```csharp
bag.SetExtraValue("PatientId", patientId);
```

Read it in your mapper:

```csharp
change.TryGetExtraValue<int>("PatientId", out var patientId);
```

## When Enrichment Runs

Enrichment runs after the main `SaveChanges` succeeds and before audit records are written.

This is important for added entities: generated keys are already available when enrichment runs.

## What to Put Where

Use domain config for stable reference rules:

```csharp
Visit.DoctorId -> Doctor.FullName
Patient.PrimaryDoctorId -> Doctor.FullName
```

Use application enrichers for cross-cutting metadata:

```csharp
TenantId
SubtenantId
UserId
PatientId
RequestId
```

## Related Pages

- [Getting Started](getting-started.md)
- [Writing Audit Records](writing.md)
- [Architecture](architecture.md)

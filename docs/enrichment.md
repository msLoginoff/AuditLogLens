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

## Reference Includes

If the readable value uses a navigation from the target entity, request that navigation in the same batched load:

```csharp
builder.Reference<Visit, Doctor, int>(
    visit => visit.DoctorId,
    "DoctorClinicName",
    doctor => doctor.Clinic.Name,
    options => options.Include(doctor => doctor.Clinic));
```

Includes are merged per target entity and key group. If several rules need the same `Doctor`, AuditLogLens still loads it in one batch.

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

When only a join row changes, AuditLogLens can create a synthetic modified change for the parent entity. This allows `Patient.Tags`-style audit records without manual `DbContext` code. The parent entity type still must be allowed by restrictions.

Limitations in the current version:

- EF Core implicit skip-navigation many-to-many relationships without a CLR join entity are not supported yet.
- `itemValueSelector` should return a value with meaningful equality, such as `string`, `int`, a record, or an anonymous type. Collection values are de-duplicated with `Distinct()`.

## Application Enrichers

Use an application enricher when the data does not belong to one domain entity, or when it depends on application services.

Application enrichers should inherit from `AuditEntityEnricherBase`. The facade owns the global enrichment order. The base class gives you template-method hooks around the moment when enrichment bags are merged into `AuditChange`.

The hook order is:

```text
BeforeMergeAsync(context)
BeforeMergeChangeAsync(context, change, bag)
merge bags into AuditChange
AfterMergeChangeAsync(context, change)
AfterMergeAsync(context)
```

`BeforeMergeChangeAsync` and `AfterMergeChangeAsync` run only for changes whose entity type matches `CanHandle`.

Use per-change hooks for simple logic:

```csharp
using AuditLogLens;
using AuditLogLens.Enrichment;
using AuditLogLens.Enrichment.Context;

public sealed class AuditMetadataEnricher : AuditEntityEnricherBase
{
    public override bool CanHandle(Type entityType) => true;

    protected override Task BeforeMergeChangeAsync(
        AuditEnrichmentContext context,
        AuditChange change,
        AuditEnrichmentBag bag,
        CancellationToken cancellationToken = default)
    {
        bag.SetExtraValue("TenantId", ResolveTenantId(change));
        bag.SetExtraValue("UserId", ResolveUserId());

        return Task.CompletedTask;
    }
}
```

Use whole-context hooks when you need grouping or cross-change correlation:

```csharp
protected override Task BeforeMergeAsync(
    AuditEnrichmentContext context,
    CancellationToken cancellationToken = default)
{
    var deletedUserIds = context.Changes
        .Where(change => change.EntityType == typeof(User)
                         && change.State == nameof(EntityState.Deleted))
        .Select(change => (string)change.EntityId!)
        .ToHashSet();

    foreach (var change in context.Changes)
    {
        var bag = context.GetBagForChange(change);
        bag.SetExtraValue("DeletedByCurrentUser", deletedUserIds.Contains(GetCurrentUserId()));
    }

    return Task.CompletedTask;
}
```

Use after-merge hooks when the final old/new values are already in `AuditChange`:

```csharp
protected override Task AfterMergeChangeAsync(
    AuditEnrichmentContext context,
    AuditChange change,
    CancellationToken cancellationToken = default)
{
    if (!ShouldRedact(change))
        return Task.CompletedTask;

    change.OldValues.Clear();
    change.NewValues.Clear();
    change.NewValues[""] = "Sensitive values were redacted";

    return Task.CompletedTask;
}
```

Do not write to bags in after-merge hooks. At that point bags are already merged and cleared.

Register application enrichers with DI:

```csharp
services.AddAuditEnricher<AuditMetadataEnricher>();
```

## Enrichment Bags

Rules and before-merge hooks write intermediate values to `AuditEnrichmentBag`.

```csharp
bag.SetOld("DoctorName", "Old doctor");
bag.SetNew("DoctorName", "New doctor");
bag.SetExtraValue("TenantId", tenantId);
```

The facade performs one official merge from bags into `AuditChange`. After that merge, bags are cleared.

If a before-merge hook consumes a temporary value and does not want it merged as-is, remove it by key:

```csharp
bag.Remove("TemporaryField");
```

You can also remove only from one bucket:

```csharp
bag.RemoveOld("TemporaryField");
bag.RemoveNew("TemporaryField");
bag.RemoveExtraValue("TemporaryField");
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

`ExtraValues` are metadata. They do not create an audit record by themselves if old and new values are both empty.

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

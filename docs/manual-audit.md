# Manual Audit

Manual audit is for application events that are not naturally represented by EF Core entity changes.

Manual events use the same enrichment and writing pipeline as automatic EF audit records:

```text
application event -> AuditChange -> enrichment -> mapper -> audit table
```

## Register Services

Manual audit uses the normal AuditLogLens registration.

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

`IAuditChangeFactory` and `IAuditPipeline` are registered by `AddAuditInfrastructure()`.

## Create a Manual Change

Use `IAuditChangeFactory` when building manual changes.

```csharp
public sealed class MailAuditService
{
    private readonly AppDbContext _db;
    private readonly IAuditChangeFactory _changeFactory;
    private readonly IAuditPipeline _pipeline;

    public MailAuditService(
        AppDbContext db,
        IAuditChangeFactory changeFactory,
        IAuditPipeline pipeline)
    {
        _db = db;
        _changeFactory = changeFactory;
        _pipeline = pipeline;
    }

    public async Task LogEmailSentAsync(int patientId, string email, CancellationToken cancellationToken)
    {
        var change = _changeFactory.CreateManual(
            tableName: "Email",
            rowKey: patientId,
            state: AuditChangeState.Added,
            newValues: new Dictionary<string, object?>
            {
                ["Email"] = email
            },
            extraValues: new Dictionary<string, object?>
            {
                ["PatientId"] = patientId
            });

        await _pipeline.ProcessAsync(_db, [change], cancellationToken: cancellationToken);
    }
}
```

The factory accepts explicit dictionaries. It does not reflect over DTOs, calculate diffs, serialize payloads, or apply restrictions. Manual audit should be intentional: the caller decides which fields belong in `OldValues`, `NewValues`, and `ExtraValues`.

## Save Behavior

Manual pipeline calls default to:

```csharp
AuditSaveBehavior.AddToCurrentContext
```

This adds the mapped audit entry to the current `DbContext` and does not call `SaveChanges`.

Use this for normal service methods where the audit entry should commit with the caller's unit of work:

```csharp
await _pipeline.ProcessAsync(_db, [change], cancellationToken: cancellationToken);

await _db.SaveChangesAsync(cancellationToken);
```

If the manual audit call is the intentional save boundary, opt into immediate saving:

```csharp
await _pipeline.ProcessAsync(
    _db,
    [change],
    new AuditPipelineSettings
    {
        SaveBehavior = AuditSaveBehavior.SaveImmediately
    },
    cancellationToken);
```

`SaveImmediately` calls `SaveChanges` on the same `DbContext` under the audit save suppressor. It also saves any other pending changes currently tracked by that context, so use it only when that is the behavior you want.

## Restrictions

EF restrictions apply to automatic detection. They do not apply to manual changes.

This is deliberate. A manual event is already explicit application code, and applying EF detection restrictions to it would make event logging depend on unrelated entity allowlists.

If an application needs a manual allowlist, add it in the application service before creating or processing the manual change.

## Enrichment

Manual changes go through the normal enrichment pipeline.

Reference and lookup enrichment can read keys from `OldValues` and `NewValues`:

```csharp
var change = _changeFactory.CreateManual(
    tableName: nameof(VisitNotification),
    rowKey: notificationId,
    state: AuditChangeState.Modified,
    oldValues: new Dictionary<string, object?>
    {
        ["DoctorId"] = oldDoctorId
    },
    newValues: new Dictionary<string, object?>
    {
        ["DoctorId"] = newDoctorId
    },
    sourceType: typeof(VisitNotification));
```

Use `sourceType` when there is no source object, but you still want enrichment rules configured for a specific payload type. Use `source` when enrichers need the actual object.

Manual changes usually have no EF `EntityEntry`. Enrichers and mappers should not assume `change.Entry` is present. Collection enrichment can use `change.EntityId` as the parent key when there is no entry or synthetic key.

## ExtraValues

Use `ExtraValues` for metadata that belongs in audit table columns, not in the old/new value payload.

Application enrichers should treat existing `ExtraValues` as explicit caller input. For example, if a manual event already supplies `SomeEntityId`, a metadata enricher should not overwrite it with `null` just because there is no EF entity to inspect.

```csharp
var someEntityId = ResolveSomeEntityId(change.Entity);
if (someEntityId is not null
    && !change.ExtraValues.ContainsKey("SomeEntityId"))
{
    bag.SetExtraValue("SomeEntityId", someEntityId);
}
```

## Mapping

Manual changes are written by the same `IAuditEntryMapper<TAuditEntry>` as automatic changes.

Mapper recommendations:

- use `change.TableName ?? change.EntityType.Name` for the table/event name;
- use `change.EntityId` for the row key or event key;
- map `change.State` to the shape your audit table expects, often with `change.State.ToString()`;
- read metadata from `change.ExtraValues`;
- do not require `change.Entry`.

## Related Pages

- [Architecture](architecture.md)
- [Enrichment](enrichment.md)
- [Writing Audit Records](writing.md)

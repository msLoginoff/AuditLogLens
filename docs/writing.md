# Writing Audit Records

AuditLogLens does not force one audit table shape.

The library creates `AuditChange` objects. By default, it maps them to the built-in [`AuditLogLensEntry`](../src/AuditLogLens/AuditLogLensEntry.cs) entity.

## Default AuditLogLensEntry

The default setup does not require a custom mapper.

```csharp
services
    .AddAuditInfrastructure()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

Add the default entity to EF:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAuditLogLens();
}
```

The built-in entity is intentionally small:

```csharp
public sealed class AuditLogLensEntry
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string TableName { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}
```

The default mapper writes:

- `TableName`
- `State`
- serialized `OldValues`
- serialized `NewValues`

The mapper is [`DefaultAuditLogLensEntryMapper`](../src/AuditLogLens/Writing/Internal/DefaultAuditLogLensEntryMapper.cs).

Use this path for a fast start or for small applications.

## Custom Mapper

Use a custom mapper when your audit table has its own shape, columns, or metadata.

Create a mapper by implementing `IAuditEntryMapper<TAuditEntry>`.

```csharp
public sealed class AuditRecordMapper : IAuditEntryMapper<AuditRecord>
{
    public bool CanMap(DbContext dbContext) => dbContext is AppDbContext;

    public AuditRecord? Map(AuditChange change, DbContext dbContext)
    {
        return new AuditRecord
        {
            TableName = change.TableName,
            EntityId = change.EntityId?.ToString(),
            State = change.State,
            OldValuesJson = JsonSerializer.Serialize(change.OldValues),
            NewValuesJson = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

Returning `null` skips writing an audit record for that change.

The default EF writer:

- skips changes that still have no `OldValues` and no `NewValues`;
- maps each `AuditChange`;
- adds mapped entries to the current `DbContext`;
- calls `SaveChanges`;
- suppresses recursive audit logging for the audit save itself.

`ExtraValues` alone do not create an audit record. They are metadata for a record that already has old or new values.

Register the custom writer:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

## Using ExtraValues

Application enrichers can put additional data into `ExtraValues`.

```csharp
change.TryGetExtraValue<int>("TenantId", out var tenantId);
change.TryGetExtraValue<string>("UserId", out var userId);
```

Example:

```csharp
public AuditRecord? Map(AuditChange change, DbContext dbContext)
{
    change.TryGetExtraValue<int>("TenantId", out var tenantId);
    change.TryGetExtraValue<string>("UserId", out var userId);

    return new AuditRecord
    {
        TenantId = tenantId,
        UserId = userId,
        TableName = change.TableName,
        State = change.State
    };
}
```

For large products, a custom audit entity is often better because it can store application-specific metadata in dedicated columns.

## Related Pages

- [Enrichment](enrichment.md)
- [Transactions](transactions.md)

# Writing Audit Records

AuditLogLens does not force one audit table shape.

The library creates `AuditChange` objects. Your mapper converts them to your audit entity.

## Mapper

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

## Register the Writer

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>();
```

The default EF writer:

- maps each `AuditChange`;
- adds mapped entries to the current `DbContext`;
- calls `SaveChanges`;
- suppresses recursive audit logging for the audit save itself.

## Using ExtraValues

Application enrichers can put additional data into `ExtraValues`.

```csharp
change.TryGetExtraValue<int>("TenantId", out var tenantId);
change.TryGetExtraValue<string>("UserId", out var userId);
```

Example:

```csharp
public AuditRecord Map(AuditChange change, DbContext dbContext)
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

## Default AuditLogLensEntry

AuditLogLens includes a small default entity:

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

To configure it in EF:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.UseAuditLogLens();
}
```

For real applications, a custom audit entity is usually better.

## Related Pages

- [Enrichment](enrichment.md)
- [Transactions](transactions.md)

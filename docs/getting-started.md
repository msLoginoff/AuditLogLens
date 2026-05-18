# Getting Started

This page shows the smallest useful AuditLogLens setup.

## 1. Register Services

```csharp
services
    .AddAuditInfrastructure()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

What this does:

- `AddAuditInfrastructure()` registers the detection, enrichment, writer, and interceptor services.
- The default writer stores audit records as `AuditLogLensEntry`.
- `AddAuditRestrictions<T>()` tells AuditLogLens what should be audited.

You do not need a custom mapper or a custom audit entity for the first setup.

## 2. Add the Interceptor to Your DbContext

```csharp
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddAuditInterceptor(provider);
});
```

AuditLogLens uses an EF Core `SaveChangesInterceptor`.

No `DbContext.SaveChanges` override is required for the basic setup.

## 3. Add the Default Audit Entity to Your Model

Add the built-in [`AuditLogLensEntry`](../src/AuditLogLens/AuditLogLensEntry.cs) entity in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.UseAuditLogLens();
}
```

By default, it uses the `AuditLogLensEntries` table.

The built-in entity has this shape:

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

You can change the table name or schema:

```csharp
modelBuilder.UseAuditLogLens(tableName: "AuditEntries", schema: "audit");
```

`UseAuditLogLens()` is defined in [`AuditLogLensModelBuilderExtensions`](../src/AuditLogLens/AuditLogLensModelBuilderExtensions.cs). The default mapper is [`DefaultAuditLogLensEntryMapper`](../src/AuditLogLens/Writing/Internal/DefaultAuditLogLensEntryMapper.cs).

## 4. Optional: Use Your Own Audit Entity

If you use your own audit entity, configure it as a normal EF entity:

```csharp
public sealed class AuditRecord
{
    public int Id { get; set; }
    public string? TableName { get; set; }
    public string? EntityId { get; set; }
    public string? State { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}
```

Then create a mapper:

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

`CanMap` is useful when the same service provider contains more than one `DbContext`.

Register the custom writer:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

## 5. Create Restrictions

```csharp
using AuditLogLens.Restrictions;

public sealed class ApplicationAuditRestrictions : AuditRestrictionsBase
{
    protected override void Configure(AuditRestrictionRules rules)
    {
        rules.For<Patient>()
            .Ignore(x => x.InternalNote);

        rules.For<Visit>();
    }
}
```

Register it with:

```csharp
services.AddAuditRestrictions<ApplicationAuditRestrictions>();
```

Do not register the internal restrictions interface directly.

Important: if you do not call `AddAuditRestrictions<T>()`, nothing is audited.

## Next Steps

Most applications should configure enrichment next:

- [Enrichment](enrichment.md)
- [Restrictions](restrictions.md)
- [Writing Audit Records](writing.md)

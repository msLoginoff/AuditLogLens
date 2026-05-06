# Getting Started

This page shows the smallest useful AuditLogLens setup.

## 1. Register Services

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

What this does:

- `AddAuditInfrastructure()` registers the detection, enrichment, writer, and interceptor services.
- `AddEfAuditWriter<TAuditEntry, TMapper>()` tells AuditLogLens what EF entity should be written as the audit record.
- `AddAuditRestrictions<T>()` tells AuditLogLens what should be audited.

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

## 3. Add the Audit Entity to Your Model

If you use the default `AuditLogLensEntry`, add it in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.UseAuditLogLens();
}
```

If you use your own audit entity, configure it as a normal EF entity.

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

## 4. Create a Mapper

```csharp
public sealed class AuditRecordMapper : IAuditEntryMapper<AuditRecord>
{
    public bool CanMap(DbContext dbContext) => dbContext is AppDbContext;

    public AuditRecord Map(AuditChange change, DbContext dbContext)
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

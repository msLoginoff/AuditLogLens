# AuditLogLens

AuditLogLens is an EF Core audit pipeline for readable audit logs.

It watches `SaveChanges`, creates `AuditChange` objects, enriches them with readable data, maps them to your audit entity, and writes audit records through EF Core.

The main idea is simple:

```text
EF Core changes -> AuditChange -> enrichment -> mapper -> audit table
```

## Quick Start

### 1. Register AuditLogLens

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>()
    .AddAuditEnricher<AuditMetadataEnricher>();
```

`AddAuditRestrictions<T>()` tells AuditLogLens which entities and properties should be audited.

### 2. Add the EF Core interceptor

```csharp
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddAuditInterceptor(provider);
});
```

### 3. Map `AuditChange` to your audit entity

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
            OldValues = JsonSerializer.Serialize(change.OldValues),
            NewValues = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

### 4. Configure restrictions

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

If no rules are configured, nothing is audited. This is intentional: audit logging should be explicit, so an application does not accidentally write sensitive tables.

If at least one rule is configured, only listed entities are audited.

### 5. Add readable enrichment

Domain-level enrichment is the most common way to replace raw ids with readable values.

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

This can turn audit data like:

```text
DoctorId = 42
```

into:

```text
DoctorName = "Dr. Smith"
```

AuditLogLens batches enrichment loads across all changes first. This helps avoid the common N+1 shape when many audit records need the same related data.

## Documentation

Start here:

- [Getting Started](docs/getting-started.md)
- [Enrichment](docs/enrichment.md)
- [Restrictions](docs/restrictions.md)
- [Writing Audit Records](docs/writing.md)
- [Transactions](docs/transactions.md)
- [Testing](docs/testing.md)
- [Architecture](docs/architecture.md)

## Main Concepts

- `AuditChange` is the library-level representation of one audited change.
- `IAuditEntryMapper<TAuditEntry>` maps `AuditChange` to your audit entity.
- `AuditRestrictionsBase` controls which entities and properties are audited.
- `IHasAuditEnrichmentConfig<TSelf>` adds declarative enrichment rules near the domain entity.
- `AuditEntityEnricherBase` is the base class for application-level enrichment that does not belong to one domain entity.

## Current Status

AuditLogLens is usable as a source-based library and is being shaped into a cleaner reusable package.

The current version has:

- EF Core `SaveChangesInterceptor` integration.
- Added, modified, and deleted entity detection.
- Added-entity temporary key handling.
- Declarative enrichment with batched reference loading.
- Custom application enrichers.
- Default EF writer with recursion suppression.
- Optional transactional audit writing.

Current version: source-based working version for EF Core audit logging with enrichment, restrictions, custom mapping, EF writing, and optional transactional writes.

## License

Add license information before publishing as a public package.

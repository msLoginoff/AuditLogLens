# AuditLogLens

[![CI](https://github.com/msLoginoff/AuditLogLens/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/msLoginoff/AuditLogLens/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AuditLogLens.svg)](https://www.nuget.org/packages/AuditLogLens)
[![NuGet Downloads](https://img.shields.io/nuget/dt/AuditLogLens.svg)](https://www.nuget.org/packages/AuditLogLens)
[![License](https://img.shields.io/github/license/msLoginoff/AuditLogLens.svg)](https://github.com/msLoginoff/AuditLogLens/blob/master/LICENSE)

AuditLogLens is an EF Core audit log library for applications that need explicit audit rules and readable change records.

It watches `SaveChanges`, creates `AuditChange` objects, enriches them with readable data, maps them to an audit entity, and writes audit records through EF Core.

The main idea is simple:

```text
EF Core changes -> AuditChange -> enrichment -> mapper -> audit table
```

The current public pipeline starts from EF Core `SaveChanges`. Manual/event audit sources are not exposed as a public API yet.

## Installation

```bash
dotnet add package AuditLogLens --version 0.1.0-alpha.1
```

`0.1.0-alpha.1` is the first public alpha. The API is usable, but still being shaped before a stable `1.0` release.

## Quick Start

### 1. Register AuditLogLens

```csharp
services
    .AddAuditInfrastructure()
    .AddAuditRestrictions<ApplicationAuditRestrictions>()
    .AddAuditEnricher<AuditMetadataEnricher>();
```

This uses the built-in `AuditLogLensEntry` audit entity and the built-in mapper.

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

### 3. Add the default audit entity to your EF model

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.UseAuditLogLens();
}
```

The default entity is [`AuditLogLensEntry`](src/AuditLogLens/AuditLogLensEntry.cs). It stores:

- `Id`
- `CreatedAtUtc`
- `TableName`
- `State`
- `OldValuesJson`
- `NewValuesJson`

`UseAuditLogLens()` is defined in [`AuditLogLensModelBuilderExtensions`](src/AuditLogLens/AuditLogLensModelBuilderExtensions.cs).

The default mapper is [`DefaultAuditLogLensEntryMapper`](src/AuditLogLens/Writing/Internal/DefaultAuditLogLensEntryMapper.cs). Use `AddEfAuditWriter<TAuditEntry, TMapper>()` only when you want your own audit table shape.

### 4. Optional: map `AuditChange` to your own audit entity

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
            OldValues = JsonSerializer.Serialize(change.OldValues),
            NewValues = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

Register the custom writer instead of the default writer:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

### 5. Configure restrictions

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

### 6. Add readable enrichment

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

For complex enrichers, use `Lookup(...)` in `Configure(...)` to preload data in the same batch pipeline, then read it from `context.GetLoaded<T>(...)` inside a hook. This is useful for JSON/custom values where a simple `Reference(...)` is not enough.

Application-level enrichers can also add metadata or reshape values through `AuditEntityEnricherBase`. It uses template-method hooks around the single bag merge:

```text
BeforeMergeAsync
BeforeMergeChangeAsync
merge bags into AuditChange
AfterMergeChangeAsync
AfterMergeAsync
```

Use per-change hooks for simple one-change logic. Use whole-context hooks when you need grouping, cross-change correlation, or application services.

## Documentation

Start here:

- [Getting Started](docs/getting-started.md)
- [Documentation Index](docs/README.md)
- [Changelog](CHANGELOG.md)
- [Recipes](docs/recipes.md)
- [Enrichment](docs/enrichment.md)
- [Restrictions](docs/restrictions.md)
- [Writing Audit Records](docs/writing.md)
- [Transactions](docs/transactions.md)
- [Testing](docs/testing.md)
- [Architecture](docs/architecture.md)

## Main Concepts

- `AuditChange` is the library-level representation of one audited change.
- `AuditLogLensEntry` is the built-in audit entity for fast setup.
- `IAuditEntryMapper<TAuditEntry>` maps `AuditChange` to your custom audit entity.
- `AuditRestrictionsBase` controls which entities and properties are audited.
- `IHasAuditEnrichmentConfig<TSelf>` adds declarative enrichment rules near the domain entity.
- `AuditEntityEnricherBase` is the base class for application-level enrichment that does not belong to one domain entity.

## Project

- [Contributing](CONTRIBUTING.md)
- [Security Policy](SECURITY.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)

## Current Status

AuditLogLens is available as a public alpha NuGet package.

The current package has:

- EF Core `SaveChangesInterceptor` integration.
- Added, modified, and deleted entity detection.
- Added-entity temporary key handling.
- Declarative enrichment with batched reference loading.
- Collection enrichment for explicit join entities.
- Custom application enrichers.
- Default EF writer with recursion suppression.
- Optional transactional audit writing.

Not supported yet:

- Public manual/event audit source API. Today the built-in pipeline is designed around EF Core detection. A future version may allow an application trigger to create `AuditChange` objects and send them through the same enrichment and writing pipeline.

## License

AuditLogLens is licensed under the [MIT License](LICENSE).

# Recipes

This page contains short examples for common AuditLogLens setups.

Use the topic pages for deeper explanations:

- [Getting Started](getting-started.md)
- [Enrichment](enrichment.md)
- [Restrictions](restrictions.md)
- [Writing Audit Records](writing.md)
- [Transactions](transactions.md)

## Use the Default Audit Table

Register AuditLogLens:

```csharp
services
    .AddAuditInfrastructure()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

Add the interceptor:

```csharp
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddAuditInterceptor(provider);
});
```

Add the default audit entity:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.UseAuditLogLens();
}
```

This writes audit records as `AuditLogLensEntry`.

## Audit Only Listed Entities

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

If no rules are configured, nothing is audited.

## Use a Custom Audit Table

Create an audit entity:

```csharp
public sealed class AuditRecord
{
    public int Id { get; set; }
    public string? TableName { get; set; }
    public string? State { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}
```

Create a mapper:

```csharp
public sealed class AuditRecordMapper : IAuditEntryMapper<AuditRecord>
{
    public bool CanMap(DbContext dbContext) => dbContext is AppDbContext;

    public AuditRecord Map(AuditChange change, DbContext dbContext)
    {
        return new AuditRecord
        {
            TableName = change.TableName,
            State = change.State,
            OldValuesJson = JsonSerializer.Serialize(change.OldValues),
            NewValuesJson = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

Register the custom writer:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddAuditRestrictions<ApplicationAuditRestrictions>();
```

## Add Readable Reference Values

Put domain-level enrichment near the entity:

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

AuditLogLens batches reference loading across all changes before applying enrichment.

## Add Request or User Metadata

Use an application enricher when data does not belong to one domain entity:

```csharp
using AuditLogLens.Enrichment;

public sealed class AuditMetadataEnricher : AuditEntityEnricherBase
{
    protected override Task ApplyCustomAsync(
        AuditEnrichmentContext context,
        CancellationToken cancellationToken)
    {
        foreach (var change in context.Changes)
        {
            change.SetExtraValue("UserId", "current-user-id");
        }

        return Task.CompletedTask;
    }
}
```

Register it:

```csharp
services.AddAuditEnricher<AuditMetadataEnricher>();
```

Read `ExtraValues` in a custom mapper:

```csharp
change.TryGetExtraValue<string>("UserId", out var userId);
```

## Enable Transactional Audit Writes

```csharp
services.AddAuditInfrastructure(options =>
{
    options.WriteMode = AuditWriteMode.Transactional;
});
```

Read [Transactions](transactions.md) before using this mode with EF Core execution strategies or retry policies.

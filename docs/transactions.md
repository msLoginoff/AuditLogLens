# Transactions

AuditLogLens supports two write modes.

## NonTransactional

This is the default.

```csharp
services.AddAuditInfrastructure();
```

In this mode:

- the main `SaveChanges` runs first;
- audit changes are enriched after the main save;
- audit records are saved after that;
- recursive audit logging is suppressed for the audit save.

If the audit write fails, the main save has already succeeded.

This mode is simple and works well when audit writing should not block business data.

## Transactional

Use transactional mode when business changes and audit records must commit together.

```csharp
services.AddAuditInfrastructure(options =>
{
    options.WriteMode = AuditWriteMode.Transactional;
});
```

In this mode, AuditLogLens opens an EF transaction before the main save if there is no active transaction.

If audit writing fails, the owned transaction is rolled back.

## Dynamic Write Mode

You can choose the mode per save operation:

```csharp
services.AddAuditInfrastructure(options =>
{
    options.WriteModeSelector = (dbContext, saveContext) =>
        saveContext.PreSaveChanges.Any(change => change.State == nameof(EntityState.Deleted))
            ? AuditWriteMode.Transactional
            : AuditWriteMode.NonTransactional;
});
```

## Existing Transactions

If the `DbContext` already has an active transaction, AuditLogLens does not open another one.

This is useful when the application owns the transaction:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync();

await db.SaveChangesAsync();

await transaction.CommitAsync();
```

## Execution Strategies and Retries

EF Core retrying execution strategies do not allow arbitrary user-created transactions unless the whole operation is executed through the strategy.

If `AuditWriteMode.Transactional` would need to open a transaction while the current execution strategy retries on failure, AuditLogLens throws and asks you to own the transaction outside `SaveChanges`.

Use this pattern in applications that require retries and transactional audit writing:

```csharp
var strategy = db.Database.CreateExecutionStrategy();

await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await db.Database.BeginTransactionAsync();

    await db.SaveChangesAsync();

    await transaction.CommitAsync();
});
```

## Related Pages

- [Writing Audit Records](writing.md)
- [Architecture](architecture.md)

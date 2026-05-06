# Testing

AuditLogLens has tests for the core pipeline.

Run tests with:

```bash
dotnet run --project tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj --no-build
```

In some restricted environments `dotnet test` can fail because the VSTest runner opens a local socket. The xUnit in-process runner avoids that issue.

## Current Coverage

The test suite covers:

- change detection for added, modified, deleted, detached, and unchanged entities;
- ignored properties;
- skipped entities;
- generated keys for added entities;
- EF writer integration;
- recursive audit suppression;
- custom restrictions registration;
- domain enrichment config;
- extra values;
- transactional rollback on audit write failure;
- parallel save operations with shared service provider.

## Testing Internal Types

Some pipeline types are internal. Tests can access them through:

```csharp
[assembly: InternalsVisibleTo("AuditLogLens.Tests")]
```

This keeps the public package API smaller while still allowing focused tests.

## Good Test Targets

Prefer public API tests for:

- service registration;
- EF interceptor integration;
- restrictions;
- mapper behavior;
- domain enrichment config.

Use internal tests for small pipeline pieces when public API tests would be too heavy.

## Remaining Test Debt

Useful future tests:

- reverse reference rules;
- tracked-first enrichment lookup;
- retry execution strategy behavior;
- default `AuditLogLensEntry` JSON mapping;
- bigger end-to-end scenarios with added, modified, deleted, enrichment, and writing together.

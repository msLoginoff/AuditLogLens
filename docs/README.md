# AuditLogLens Documentation

Use this folder as the main entry point for AuditLogLens documentation.

## Start Here

- [Getting Started](getting-started.md): minimal setup with the default audit table.
- [Enrichment](enrichment.md): how to replace raw ids with readable values.
- [Restrictions](restrictions.md): how to choose which entities and properties are audited.
- [Writing Audit Records](writing.md): default audit entity, custom audit entity, and mapping.
- [Transactions](transactions.md): non-transactional and transactional write modes.
- [Testing](testing.md): what should be covered in application tests.
- [Architecture](architecture.md): how the internal pipeline works.

## Practical Scenarios

- [Recipes](recipes.md): short copy-paste examples for common setups.

## Recommended Reading Order

1. Read [Getting Started](getting-started.md).
2. Configure [Restrictions](restrictions.md).
3. Add [Enrichment](enrichment.md) if audit values should be readable.
4. Read [Writing Audit Records](writing.md) if the default `AuditLogLensEntry` is not enough.
5. Read [Transactions](transactions.md) before enabling transactional audit writes.

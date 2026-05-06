# Architecture

AuditLogLens is split into four stages.

```text
Detect -> Enrich -> Map -> Write
```

## Detect

The detector reads EF Core `ChangeTracker` entries before `SaveChanges`.

It creates `AuditChange` objects with:

- entity type;
- entity id;
- state;
- old values;
- new values;
- EF `EntityEntry`.

For added entities with generated keys, the real key is filled after the main save.

## Enrich

The enrichment stage makes audit changes readable.

It combines:

- domain-level config from `IHasAuditEnrichmentConfig<TSelf>`;
- application enrichers based on `AuditEntityEnricherBase`;
- reference rules and custom steps.

The important performance detail is global batching:

1. Build all plans.
2. Collect all load requests.
3. Group by target entity and property.
4. Load distinct keys.
5. Apply rules.

This avoids loading the same reference data separately for every audit change.

## Map

Mapping is application-owned.

AuditLogLens gives you `AuditChange`; your `IAuditEntryMapper<TAuditEntry>` creates the real audit entity.

This keeps the library independent from application-specific audit table schemas.

## Write

The default writer uses EF Core.

It:

- maps audit changes;
- adds audit entities to the current `DbContext`;
- calls `SaveChanges`;
- suppresses recursive audit logging during the audit save.

## Public API Shape

The public API is intentionally centered on the types users write directly:

- `AuditExtensions`
- `AuditOptions`
- `AuditWriteMode`
- `AuditChange`
- `IAuditEntryMapper<TAuditEntry>`
- `AuditRestrictionsBase`
- `AuditRestrictionRules`
- `IHasAuditEnrichmentConfig<TSelf>`
- `IAuditEnrichmentPlanBuilder`
- `AuditEntityEnricherBase`
- `AuditEnrichmentContext`
- `AuditEnrichmentBag`

Runtime pipeline types are internal.

## Related Pages

- [Getting Started](getting-started.md)
- [Enrichment](enrichment.md)
- [Transactions](transactions.md)

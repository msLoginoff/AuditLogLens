# Changelog

All notable changes to AuditLogLens will be documented in this file.

The format is intentionally small and follows the project version published to NuGet.

## 0.2.0-alpha.1 - Unreleased

### Added

- SourceLink metadata for GitHub source debugging.
- NuGet symbol package generation (`.snupkg`).
- Public repository files: changelog, security policy, contributing guide, and code of conduct.
- Public manual audit pipeline through `IAuditChangeFactory` and `IAuditPipeline`.
- Explicit manual audit save behavior with `AuditSaveBehavior`.
- Manual audit documentation and recipes.
- XML documentation for the main public API, manual pipeline, enrichment lifecycle, restrictions, writing abstractions, and low-level enrichment rules.
- Smoke tests for manual audit change creation and the built-in `AuditLogLensEntry` writer path.
- CI pack verification for the NuGet package.

### Changed

- Reorganized public source files by area: `Changes`, `Manual`, `Pipeline`, `Configuration`, `Writing`, `Restrictions`, and `Enrichment`.
- `AuditChange.State` now uses the `AuditChangeState` enum instead of a string.
- Manual audit changes are explicit dictionary-based events; the factory does not reflect DTOs, calculate diffs, serialize payloads, or apply restrictions.

### Fixed

- Manual changes without an EF `EntityEntry` can pass through enrichment safely.
- Collection enrichment can use `AuditChange.EntityId` as a parent key fallback for manual changes.

## 0.1.0-alpha.1 - 2026-05-22

### Added

- First public alpha of AuditLogLens.
- EF Core `SaveChangesInterceptor`-based audit pipeline.
- Explicit audit restrictions.
- Declarative enrichment rules.
- Batched readable lookup loading to avoid N+1 patterns.
- Default audit model and EF writer support.
- Application mapper support for custom audit table shapes.
- Explicit many-to-many collection support through CLR join entities.

### Known Limitations

- EF implicit skip-navigation/shared-type many-to-many relationships are not supported yet.
- Public manual/event audit source API is not available yet.

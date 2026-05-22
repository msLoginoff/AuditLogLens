# Changelog

All notable changes to AuditLogLens will be documented in this file.

The format is intentionally small and follows the project version published to NuGet.

## 0.1.0-alpha.2 - Unreleased

### Added

- SourceLink metadata for GitHub source debugging.
- NuGet symbol package generation (`.snupkg`).
- Public repository files: changelog, security policy, contributing guide, and code of conduct.

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

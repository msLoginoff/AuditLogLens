# Contributing

Thanks for considering a contribution to AuditLogLens.

This project is still in public alpha, so small, focused changes are easier to review than broad rewrites.

## Before Opening a Pull Request

- Open an issue first for larger API or behavior changes.
- Keep changes scoped to one problem.
- Add or update documentation when public behavior changes.
- Avoid introducing application-specific audit behavior into the library.

## Local Validation

Restore and build:

```bash
dotnet restore AuditLogLens.sln
dotnet build tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj --configuration Release --no-restore
```

Run tests:

```bash
dotnet tests/AuditLogLens.Tests/bin/Release/net10.0/AuditLogLens.Tests.dll
```

The test project currently uses xUnit v3. Running the built test assembly directly is the expected validation path.

## Versioning

NuGet package versions are immutable. Do not reuse an already published package version for changed contents.

When preparing a new package:

- Update the package version in `src/AuditLogLens/AuditLogLens.csproj`.
- Update `CHANGELOG.md`.
- Pack and publish manually from a known clean commit.
- Tag the release commit with the matching `vX.Y.Z` tag.

## Pull Request Checklist

- The change is focused and does not include unrelated cleanup.
- Public API changes are documented.
- Relevant tests were run.
- `CHANGELOG.md` is updated when package behavior or repository process changes.

# AuditLogLens

AuditLogLens is an EF Core audit pipeline focused on readable audit records. It captures entity changes from `SaveChanges`, enriches raw foreign-key based values into human-readable values, maps the library-level `AuditChange` model into an application-owned audit entity, and writes audit entries back through EF Core.

The library was extracted from a real application codebase, so the current version is intentionally pragmatic: the core pipeline works, tests are green, and some public contracts are still expected to evolve.

## English

### Short Overview

AuditLogLens is built around four stages:

1. **Detect** changes from EF Core `ChangeTracker` before and after `SaveChanges`.
2. **Enrich** detected changes with readable data, using declarative rules and optional application enrichers.
3. **Map** `AuditChange` into your own audit entity type.
4. **Write** mapped audit entities through EF Core while suppressing recursive audit logging.

Core files:

- `src/AuditLogLens/Interceptors/AuditSaveChangesInterceptor.cs` wires the pipeline into EF Core `SaveChanges`.
- `src/AuditLogLens/Legacy/EfAuditChangeDetector.cs` detects `Added`, `Modified`, and `Deleted` changes.
- `src/AuditLogLens/Enrichment/AuditEnrichmentFacade.cs` builds enrichment plans, globally batches entity loads, and applies rules.
- `src/AuditLogLens/Writer/EfAuditWriter.cs` writes mapped audit entities.
- `src/AuditLogLens/AuditChange.cs` is the library-level representation of one audited change.
- `src/AuditLogLens/Abstractions/IAuditEntryMapper.cs` is the application mapping contract.
- `src/AuditLogLens/AuditExtensions.cs` exposes the DI and EF Core setup methods.

### Current Status

The current version is usable as a working core pipeline:

- `SaveChangesAsync` integration works through an EF Core `SaveChangesInterceptor`.
- Change detection captures visible property changes and temporary keys for added entities.
- Enrichment rules are planned globally before loading related data, which avoids the obvious N+1 shape.
- Default writer stores audit records after the main save and suppresses recursive auditing.
- The standalone solution builds successfully.
- Existing tests pass.

Important: this is not yet a fully polished general-purpose package. See [Known Limitations](#known-limitations).

### Installation / Project Setup

The project currently targets `.NET 10` and depends on EF Core 10:

- `src/AuditLogLens/AuditLogLens.csproj`
- `tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj`

For source-based usage, reference the project from your application:

```xml
<ProjectReference Include="path/to/AuditLogLens/src/AuditLogLens/AuditLogLens.csproj" />
```

For package-based usage, the project still needs packaging metadata/versioning before publishing as NuGet.

### Minimal Application Integration

Register the audit infrastructure and a writer/mapping pair:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddSingleton<IAuditRestrictions, ApplicationAuditRestrictions>();
```

Add the interceptor to your EF Core `DbContextOptionsBuilder`:

```csharp
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddAuditInterceptor(provider);
});
```

The extension methods are defined in:

- `src/AuditLogLens/AuditExtensions.cs`

### How the Pipeline Works

#### 1. Interception

`AuditSaveChangesInterceptor` hooks into EF Core save events:

- `SavingChanges` / `SavingChangesAsync`
- `SavedChanges` / `SavedChangesAsync`
- `SaveChangesFailed` / `SaveChangesFailedAsync`

File:

- `src/AuditLogLens/Interceptors/AuditSaveChangesInterceptor.cs`

Before the main save, it calls:

```csharp
_changeDetector.DetectPreSaveChanges(dbContext)
```

After the main save succeeds, it calls:

```csharp
_changeDetector.DetectPostSaveChanges(dbContext, saveContext)
_enricher.EnrichAsync(changes, dbContext, cancellationToken)
_writer.WriteAsync(changes, dbContext, cancellationToken)
```

The interceptor stores per-`DbContext` save state in a `ConditionalWeakTable<DbContext, AuditSaveContext>`. This keeps pre-save information associated with the exact context instance and cleans it after success or failure.

#### 2. Change Detection

The default detector is `EfAuditChangeDetector`.

File:

- `src/AuditLogLens/Legacy/EfAuditChangeDetector.cs`

It uses EF Core `ChangeTracker` entries and `IAuditRestrictions` to decide what should be audited.

It captures:

- `EntityType`
- `EntityId`
- `State`
- `TableName`
- `OldValues`
- `NewValues`
- the original EF `EntityEntry`

For added entities with temporary keys, it stores the entry in `AuditSaveContext.EntriesWithTemporaryKeys`. After the main save, `DetectPostSaveChanges` updates `AuditChange.EntityId` with the real generated key and marks `IsAfterSavePhase = true`.

#### 3. Restrictions

Restrictions are controlled by `IAuditRestrictions`.

Files:

- `src/AuditLogLens/Abstractions/IAuditRestrictions.cs`
- `src/AuditLogLens/AuditRestrictionsBase.cs`
- `src/AuditLogLens/AuditRestrictionRule.cs`

A typical application implementation derives from `AuditRestrictionsBase`:

```csharp
public sealed class ApplicationAuditRestrictions : AuditRestrictionsBase
{
    protected override IReadOnlyCollection<AuditRestrictionRule> Rules =>
    [
        new AuditRestrictionRule
        {
            AllowedTable = nameof(Patient),
            ForbiddenProperties = [nameof(Patient.InternalNote)]
        }
    ];
}
```

`AllowedTable` means the entity type is auditable. `ForbiddenProperties` are excluded from `OldValues` and `NewValues`.

You can also override `IsAllowedEntry` when an entity should be skipped based on state or runtime conditions.

#### 4. Enrichment

The enrichment stage is implemented by `AuditEnrichmentFacade`.

File:

- `src/AuditLogLens/Enrichment/AuditEnrichmentFacade.cs`

The goal is to turn raw audit values into readable audit values. For example, instead of only storing `DoctorId = 42`, enrichment can add `DoctorName = "Dr. Smith"`.

The algorithm:

1. Group incoming `AuditChange` objects by `EntityType`.
2. Build a combined `AuditEnrichmentPlan` for each entity type.
3. Collect all `EntityLoadRequest` objects from all plans before querying the database.
4. Group load requests by `(EntityType, PropertyName)`.
5. Load all distinct requested keys per group in one query.
6. Store loaded entities in `AuditEnrichmentContext`.
7. Apply declarative rules after all required data is loaded.
8. Apply custom enrichers once per enrichment context.
9. Flush enrichment bags into `AuditChange.OldValues` and `AuditChange.NewValues`.

This is the important N+1 avoidance point: the facade collects all load requests globally first and only then performs batched loads.

#### 5. Domain Declarative Plans

Domain entities can provide static enrichment configuration by implementing:

- `src/AuditLogLens/Enrichment/Domain/IHasAuditEnrichmentConfig.cs`

Plans are discovered by:

- `src/AuditLogLens/Enrichment/Domain/StaticAuditDomainEnrichmentPlanProvider.cs`

Example shape:

```csharp
public sealed class Appointment : IHasAuditEnrichmentConfig<Appointment>
{
    public int DoctorId { get; set; }

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Reference<Appointment, Doctor, int>(
            x => x.DoctorId,
            "DoctorName",
            doctor => doctor.Name);
    }
}
```

The `Reference` extension methods are implemented in:

- `src/AuditLogLens/Enrichment/AuditEnrichmentPlanBuilderReferenceExtensions.cs`

#### 6. Reference Rules

`ReferenceRule` loads a target entity by a foreign key and adds a readable field.

File:

- `src/AuditLogLens/Enrichment/ReferenceRule.cs`

For modified entities, it can enrich both old and new foreign-key values. For added entities, it enriches the new value. For deleted entities, it enriches the old value.

#### 7. Reverse Reference Rules

`ReverseReferenceRule` loads related target entities by a foreign key that points back to the source entity.

File:

- `src/AuditLogLens/Enrichment/ReverseReferenceRule.cs`

This is useful when the audit record for one entity needs a summary of dependent entities.

The current API is lower-level than `ReferenceRule`: it requires selectors and a `Map` action.

#### 8. Application Enrichers

Applications can register custom enrichers implementing:

- `src/AuditLogLens/Enrichment/IAuditEntityEnricher.cs`

The base class is:

- `src/AuditLogLens/Enrichment/AuditEntityEnricherBase.cs`

A custom enricher can:

- decide which entity types it handles via `CanHandle`;
- add rules during plan building via `Configure`;
- mutate enrichment bags or changes during `ApplyAsync`.

`AuditEntityEnricherRegistry` ensures each distinct enricher is applied once per enrichment context even if it handles multiple entity types.

File:

- `src/AuditLogLens/Enrichment/AuditEntityEnricherRegistry.cs`

#### 9. Mapping

AuditLogLens does not own your audit table schema. Instead, it exposes `AuditChange` and asks your application to map it into your own audit entity.

Contract:

- `src/AuditLogLens/Abstractions/IAuditEntryMapper.cs`

```csharp
public interface IAuditEntryMapper<TAuditEntry>
    where TAuditEntry : class
{
    bool CanMap(DbContext dbContext);

    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}
```

Example:

```csharp
public sealed class AuditRecordMapper : IAuditEntryMapper<AuditRecord>
{
    public bool CanMap(DbContext dbContext) => dbContext is AppDbContext;

    public AuditRecord? Map(AuditChange change, DbContext dbContext)
    {
        return new AuditRecord
        {
            TableName = change.TableName ?? change.EntityType.Name,
            EntityId = change.EntityId?.ToString(),
            State = change.State,
            OldValues = JsonSerializer.Serialize(change.OldValues),
            NewValues = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

Returning `null` skips a specific change.

#### 10. Writing

The default writer is `EfAuditWriter<TAuditEntry>`.

File:

- `src/AuditLogLens/Writer/EfAuditWriter.cs`

It:

1. selects the first mapper whose `CanMap(dbContext)` returns `true`;
2. maps every `AuditChange` into `TAuditEntry`;
3. skips `null` results;
4. adds audit entries through `dbContext.Set<TAuditEntry>().AddRange(...)`;
5. calls `SaveChangesAsync` to persist audit entries.

The default writer is intentionally mapping-agnostic: storage strategy and mapping are separate.

#### 11. Recursion Suppression

Because `EfAuditWriter<TAuditEntry>` calls `SaveChangesAsync` internally, the audit interceptor would normally run again and audit its own audit records.

That is prevented by:

- `src/AuditLogLens/Interceptors/AuditSaveChangesSuppressor.cs`

The suppressor uses an `AsyncLocal` scope chain inspired by `Microsoft.Extensions.Logging.LoggerExternalScopeProvider`. While the writer performs its internal save, `AuditSaveChangesInterceptor` sees `IsSuppressed == true` and immediately exits.

This suppression is local to the current async flow. A parallel request in the same process is not suppressed.

### Tests

The test project is:

- `tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj`

Current focused areas:

- change detection behavior;
- generated key handling for added entities;
- global enrichment batching across source entity types;
- default EF writer path through the interceptor.

Run:

```bash
dotnet build AuditLogLens.sln
dotnet run --project tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj --no-build
```

### Known Limitations

The library is intentionally not presented as fully finished yet.

Current limitations:

- The default writer is not transactional with the main save. Main changes can be saved while audit saving fails.
- The synchronous interceptor path uses sync-over-async via `GetAwaiter().GetResult()`.
- `AuditChange` still contains application-specific metadata fields: `TenantId`, `SubtenantId`, `PatientId`, `UserId`.
- Enrichment preload currently queries with `AsNoTracking()` and does not first reuse already tracked entities.
- `ReferenceRule` and `ReverseReferenceRule` currently scan loaded entities during rule application instead of using indexed lookups.
- The enrichment DSL is still small and should grow based on real use cases.
- Some types still live under the `Legacy` namespace because the extraction is incremental.

### Development Workflow

Recommended workflow while the API is still evolving:

1. Change AuditLogLens in this repository.
2. Build and run existing tests.
3. Push changes to GitHub.
4. Update the consuming application to the required commit or package version.

Suggested integration strategy for active development: use a Git submodule or a direct source reference until the API stabilizes. Later, publish as a NuGet package.

---

## Русский

### Краткий обзор

AuditLogLens — это EF Core audit pipeline, сфокусированный на читабельных audit-записях. Библиотека перехватывает `SaveChanges`, собирает изменения сущностей, обогащает сырые значения читабельными данными, маппит библиотечную модель `AuditChange` в audit entity прикладного кода и сохраняет audit-записи через EF Core.

Pipeline состоит из четырёх стадий:

1. **Detect** — найти изменения через EF Core `ChangeTracker` до и после `SaveChanges`.
2. **Enrich** — обогатить изменения читабельными данными через декларативные rules и application enrichers.
3. **Map** — преобразовать `AuditChange` в audit entity прикладного кода.
4. **Write** — записать audit entities через EF Core и не уйти в рекурсивный audit.

Основные файлы:

- `src/AuditLogLens/Interceptors/AuditSaveChangesInterceptor.cs` подключает pipeline к EF Core `SaveChanges`.
- `src/AuditLogLens/Legacy/EfAuditChangeDetector.cs` детектит `Added`, `Modified`, `Deleted` изменения.
- `src/AuditLogLens/Enrichment/AuditEnrichmentFacade.cs` строит планы enrichment, глобально батчит загрузку связанных сущностей и применяет rules.
- `src/AuditLogLens/Writer/EfAuditWriter.cs` записывает audit entities.
- `src/AuditLogLens/AuditChange.cs` представляет одно audit-изменение на уровне библиотеки.
- `src/AuditLogLens/Abstractions/IAuditEntryMapper.cs` задаёт контракт маппинга в прикладную audit entity.
- `src/AuditLogLens/AuditExtensions.cs` содержит DI и EF Core extension methods.

### Текущий статус

Текущая версия уже работает как core pipeline:

- `SaveChangesAsync` интегрирован через EF Core `SaveChangesInterceptor`.
- Change detector собирает видимые изменения свойств и temporary keys для добавленных сущностей.
- Enrichment сначала собирает все load requests глобально, а уже потом грузит связанные данные, что закрывает очевидную N+1 проблему.
- Default writer сохраняет audit-записи после основного save и подавляет рекурсивный audit.
- Standalone solution собирается.
- Существующие тесты проходят.

Важно: это ещё не полностью отполированная general-purpose библиотека. Ограничения перечислены в разделе [Известные ограничения](#известные-ограничения).

### Подключение

Проект сейчас таргетит `.NET 10` и EF Core 10:

- `src/AuditLogLens/AuditLogLens.csproj`
- `tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj`

Для source-based подключения можно добавить project reference:

```xml
<ProjectReference Include="path/to/AuditLogLens/src/AuditLogLens/AuditLogLens.csproj" />
```

Для NuGet-публикации ещё нужно добавить package metadata и договориться о versioning.

### Минимальная интеграция в приложение

Зарегистрировать infrastructure и writer/mapping:

```csharp
services
    .AddAuditInfrastructure()
    .AddEfAuditWriter<AuditRecord, AuditRecordMapper>()
    .AddSingleton<IAuditRestrictions, ApplicationAuditRestrictions>();
```

Добавить interceptor в `DbContextOptionsBuilder`:

```csharp
services.AddDbContext<AppDbContext>((provider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddAuditInterceptor(provider);
});
```

Extension methods находятся в:

- `src/AuditLogLens/AuditExtensions.cs`

### Как работает pipeline

#### 1. Interception

`AuditSaveChangesInterceptor` подключается к EF Core save events:

- `SavingChanges` / `SavingChangesAsync`
- `SavedChanges` / `SavedChangesAsync`
- `SaveChangesFailed` / `SaveChangesFailedAsync`

Файл:

- `src/AuditLogLens/Interceptors/AuditSaveChangesInterceptor.cs`

До основного save вызывается:

```csharp
_changeDetector.DetectPreSaveChanges(dbContext)
```

После успешного основного save вызывается:

```csharp
_changeDetector.DetectPostSaveChanges(dbContext, saveContext)
_enricher.EnrichAsync(changes, dbContext, cancellationToken)
_writer.WriteAsync(changes, dbContext, cancellationToken)
```

Pre-save состояние хранится в `ConditionalWeakTable<DbContext, AuditSaveContext>`, то есть привязано к конкретному экземпляру `DbContext` и очищается после успеха или ошибки.

#### 2. Change Detection

Дефолтный detector — `EfAuditChangeDetector`.

Файл:

- `src/AuditLogLens/Legacy/EfAuditChangeDetector.cs`

Он читает EF Core `ChangeTracker` и использует `IAuditRestrictions`, чтобы решить, какие сущности и свойства надо аудировать.

Он заполняет:

- `EntityType`
- `EntityId`
- `State`
- `TableName`
- `OldValues`
- `NewValues`
- исходный EF `EntityEntry`

Для добавленных сущностей с temporary keys detector сохраняет entry в `AuditSaveContext.EntriesWithTemporaryKeys`. После основного save `DetectPostSaveChanges` заменяет temporary key на реальный generated key и ставит `IsAfterSavePhase = true`.

#### 3. Restrictions

Ограничения задаются через `IAuditRestrictions`.

Файлы:

- `src/AuditLogLens/Abstractions/IAuditRestrictions.cs`
- `src/AuditLogLens/AuditRestrictionsBase.cs`
- `src/AuditLogLens/AuditRestrictionRule.cs`

Пример:

```csharp
public sealed class ApplicationAuditRestrictions : AuditRestrictionsBase
{
    protected override IReadOnlyCollection<AuditRestrictionRule> Rules =>
    [
        new AuditRestrictionRule
        {
            AllowedTable = nameof(Patient),
            ForbiddenProperties = [nameof(Patient.InternalNote)]
        }
    ];
}
```

`AllowedTable` означает, что entity type можно аудировать. `ForbiddenProperties` исключаются из `OldValues` и `NewValues`.

Если нужно пропускать сущность по состоянию или runtime-условиям, можно переопределить `IsAllowedEntry`.

#### 4. Enrichment

Стадия enrichment реализована в `AuditEnrichmentFacade`.

Файл:

- `src/AuditLogLens/Enrichment/AuditEnrichmentFacade.cs`

Цель enrichment — превратить сырые audit-значения в читабельные. Например, вместо одного `DoctorId = 42` добавить `DoctorName = "Dr. Smith"`.

Алгоритм:

1. Сгруппировать `AuditChange` по `EntityType`.
2. Построить combined `AuditEnrichmentPlan` для каждого entity type.
3. Собрать все `EntityLoadRequest` из всех планов до похода в БД.
4. Сгруппировать load requests по `(EntityType, PropertyName)`.
5. Одним запросом на группу загрузить все distinct keys.
6. Сохранить загруженные сущности в `AuditEnrichmentContext`.
7. Применить declarative rules после загрузки всех данных.
8. Применить custom enrichers один раз на enrichment context.
9. Слить enrichment bags в `AuditChange.OldValues` и `AuditChange.NewValues`.

Важная часть: все load requests собираются глобально заранее, а не в цикле по каждой сущности. Это и закрывает основную N+1 проблему.

#### 5. Domain Declarative Plans

Доменная сущность может описать static enrichment config через:

- `src/AuditLogLens/Enrichment/Domain/IHasAuditEnrichmentConfig.cs`

Планы обнаруживаются через:

- `src/AuditLogLens/Enrichment/Domain/StaticAuditDomainEnrichmentPlanProvider.cs`

Пример:

```csharp
public sealed class Appointment : IHasAuditEnrichmentConfig<Appointment>
{
    public int DoctorId { get; set; }

    public static void ConfigureAuditEnrichment(IAuditEnrichmentPlanBuilder builder)
    {
        builder.Reference<Appointment, Doctor, int>(
            x => x.DoctorId,
            "DoctorName",
            doctor => doctor.Name);
    }
}
```

`Reference` extension methods находятся в:

- `src/AuditLogLens/Enrichment/AuditEnrichmentPlanBuilderReferenceExtensions.cs`

#### 6. Reference Rules

`ReferenceRule` загружает target entity по foreign key и добавляет читабельное поле.

Файл:

- `src/AuditLogLens/Enrichment/ReferenceRule.cs`

Для modified entities rule может обогатить и old, и new foreign-key value. Для added entities обогащается new value, для deleted entities — old value.

#### 7. Reverse Reference Rules

`ReverseReferenceRule` загружает зависимые target entities по foreign key, который указывает обратно на source entity.

Файл:

- `src/AuditLogLens/Enrichment/ReverseReferenceRule.cs`

Это полезно, когда audit-запись одной сущности должна содержать summary связанных дочерних сущностей.

Текущий API нижеуровневый: нужно передать selectors и `Map` action.

#### 8. Application Enrichers

Приложение может регистрировать custom enrichers через:

- `src/AuditLogLens/Enrichment/IAuditEntityEnricher.cs`

Базовый класс:

- `src/AuditLogLens/Enrichment/AuditEntityEnricherBase.cs`

Custom enricher может:

- выбирать entity types через `CanHandle`;
- добавлять rules через `Configure`;
- менять enrichment bags или changes внутри `ApplyAsync`.

`AuditEntityEnricherRegistry` гарантирует, что каждый distinct enricher применяется один раз на enrichment context, даже если он подходит под несколько entity types.

Файл:

- `src/AuditLogLens/Enrichment/AuditEntityEnricherRegistry.cs`

#### 9. Mapping

AuditLogLens не владеет схемой audit-таблицы приложения. Вместо этого библиотека отдаёт `AuditChange`, а приложение маппит его в свою audit entity.

Контракт:

- `src/AuditLogLens/Abstractions/IAuditEntryMapper.cs`

```csharp
public interface IAuditEntryMapper<TAuditEntry>
    where TAuditEntry : class
{
    bool CanMap(DbContext dbContext);

    TAuditEntry? Map(AuditChange change, DbContext dbContext);
}
```

Пример:

```csharp
public sealed class AuditRecordMapper : IAuditEntryMapper<AuditRecord>
{
    public bool CanMap(DbContext dbContext) => dbContext is AppDbContext;

    public AuditRecord? Map(AuditChange change, DbContext dbContext)
    {
        return new AuditRecord
        {
            TableName = change.TableName ?? change.EntityType.Name,
            EntityId = change.EntityId?.ToString(),
            State = change.State,
            OldValues = JsonSerializer.Serialize(change.OldValues),
            NewValues = JsonSerializer.Serialize(change.NewValues)
        };
    }
}
```

Если `Map` возвращает `null`, конкретное изменение не будет записано.

#### 10. Writing

Дефолтный writer — `EfAuditWriter<TAuditEntry>`.

Файл:

- `src/AuditLogLens/Writer/EfAuditWriter.cs`

Он:

1. выбирает первый mapper, у которого `CanMap(dbContext) == true`;
2. маппит каждый `AuditChange` в `TAuditEntry`;
3. пропускает `null` результаты;
4. добавляет audit entries через `dbContext.Set<TAuditEntry>().AddRange(...)`;
5. вызывает `SaveChangesAsync`, чтобы сохранить audit entries.

Writer намеренно не знает деталей mapping: storage strategy и mapping разделены.

#### 11. Suppression от рекурсии

`EfAuditWriter<TAuditEntry>` внутри себя вызывает `SaveChangesAsync`. Без защиты interceptor снова запустил бы audit pipeline и начал бы аудировать audit-записи.

Это предотвращает:

- `src/AuditLogLens/Interceptors/AuditSaveChangesSuppressor.cs`

Suppressor использует `AsyncLocal` scope chain, вдохновлённый `Microsoft.Extensions.Logging.LoggerExternalScopeProvider`. Пока writer выполняет внутренний save, `AuditSaveChangesInterceptor` видит `IsSuppressed == true` и сразу выходит.

Suppression локален для текущего async-flow. Параллельный request в том же процессе не будет подавлен.

### Тесты

Test project:

- `tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj`

Сейчас покрыты основные области:

- change detection;
- generated key handling для added entities;
- global enrichment batching across source entity types;
- default EF writer path через interceptor.

Запуск:

```bash
dotnet build AuditLogLens.sln
dotnet run --project tests/AuditLogLens.Tests/AuditLogLens.Tests.csproj --no-build
```

### Известные ограничения

Библиотека пока не позиционируется как полностью завершённая.

Текущие ограничения:

- Default writer не транзакционный относительно основного save. Основные изменения могут сохраниться, а audit-save может упасть.
- Синхронный interceptor path использует sync-over-async через `GetAwaiter().GetResult()`.
- `AuditChange` всё ещё содержит application-specific metadata fields: `TenantId`, `SubtenantId`, `PatientId`, `UserId`.
- Enrichment preload сейчас грузит данные через `AsNoTracking()` и не переиспользует сначала уже tracked entities.
- `ReferenceRule` и `ReverseReferenceRule` сейчас сканируют загруженные сущности при применении rules вместо индексированных lookup'ов.
- Enrichment DSL пока небольшой и должен расширяться под реальные кейсы.
- Некоторые типы всё ещё находятся в namespace `Legacy`, потому что extraction идёт постепенно.

### Development Workflow

Рекомендуемый процесс, пока API активно развивается:

1. Менять AuditLogLens в этом repo.
2. Собирать solution и запускать существующие тесты.
3. Пушить изменения в GitHub.
4. Обновлять consuming application до нужного commit или package version.

На этапе активной разработки удобно использовать Git submodule или source reference. После стабилизации API лучше перейти к NuGet package.

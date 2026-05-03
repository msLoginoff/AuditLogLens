using System.Runtime.CompilerServices;
using AuditLogLens.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLogLens.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditChangeDetector _changeDetector;
    private readonly IAuditEnricher _enricher;
    private readonly IAuditWriter _writer;
    private readonly AuditSaveChangesSuppressor _suppressor;

    private readonly ConditionalWeakTable<DbContext, AuditSaveContext> _saveContexts = new();

    public AuditSaveChangesInterceptor(
        IAuditChangeDetector changeDetector,
        IAuditEnricher enricher,
        IAuditWriter writer,
        AuditSaveChangesSuppressor suppressor)
    {
        _changeDetector = changeDetector;
        _enricher = enricher;
        _writer = writer;
        _suppressor = suppressor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        var dbContext = eventData.Context;
        if (_suppressor.IsSuppressed)
        {
            return result;
        }

        if (dbContext is null)
        {
            return result;
        }

        var saveContext = _changeDetector.DetectPreSaveChanges(dbContext);

        _saveContexts.Remove(dbContext);
        _saveContexts.Add(dbContext, saveContext);

        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (_suppressor.IsSuppressed)
        {
            return result;
        }

        if (dbContext is null)
        {
            return result;
        }

        var saveContext = _changeDetector.DetectPreSaveChanges(dbContext);

        _saveContexts.Remove(dbContext);
        _saveContexts.Add(dbContext, saveContext);

        return result;
    }

    public override int SavedChanges(
        SaveChangesCompletedEventData eventData,
        int result)
    {
        var dbContext = eventData.Context;
        if (_suppressor.IsSuppressed)
        {
            return result;
        }

        if (dbContext is null)
        {
            return result;
        }

        if (!_saveContexts.TryGetValue(dbContext, out var saveContext))
        {
            return result;
        }

        try
        {
            var changes = _changeDetector.DetectPostSaveChanges(dbContext, saveContext);

            _enricher.EnrichAsync(changes, dbContext).GetAwaiter().GetResult();

            _writer.WriteAsync(changes, dbContext).GetAwaiter().GetResult();
        }
        finally
        {
            _saveContexts.Remove(dbContext);
        }

        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context;
        if (_suppressor.IsSuppressed)
        {
            return result;
        }

        if (dbContext is null)
        {
            return result;
        }

        if (!_saveContexts.TryGetValue(dbContext, out var saveContext))
        {
            return result;
        }

        try
        {
            var changes = _changeDetector.DetectPostSaveChanges(dbContext, saveContext);

            await _enricher.EnrichAsync(changes, dbContext, cancellationToken).ConfigureAwait(false);

            await _writer.WriteAsync(changes, dbContext, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveContexts.Remove(dbContext);
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            _saveContexts.Remove(eventData.Context);
        }

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _saveContexts.Remove(eventData.Context);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
using System.Runtime.CompilerServices;
using System.Transactions;
using AuditLogLens.Configuration;
using AuditLogLens.Detection;
using AuditLogLens.Detection.Internal;
using AuditLogLens.Pipeline;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLogLens.Interceptors;

internal sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IAuditChangeDetector _changeDetector;
    private readonly IAuditPipeline _pipeline;
    private readonly AuditSaveChangesSuppressor _suppressor;
    private readonly AuditOptions _options;

    private readonly ConditionalWeakTable<DbContext, AuditSaveContext> _saveContexts = new();

    public AuditSaveChangesInterceptor(
        IAuditChangeDetector changeDetector,
        IAuditPipeline pipeline,
        AuditSaveChangesSuppressor suppressor,
        AuditOptions options)
    {
        _changeDetector = changeDetector;
        _pipeline = pipeline;
        _suppressor = suppressor;
        _options = options;
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
        PrepareSaveContext(dbContext, saveContext);

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
        await PrepareSaveContextAsync(dbContext, saveContext, cancellationToken).ConfigureAwait(false);

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

            _pipeline.ProcessAsync(
                    dbContext,
                    changes,
                    new AuditPipelineSettings
                    {
                        SaveBehavior = AuditSaveBehavior.SaveImmediately,
                        TrackedEntries = saveContext.TrackedEntries
                    })
                .GetAwaiter()
                .GetResult();

            CommitOwnedTransaction(saveContext);
        }
        catch
        {
            RollbackOwnedTransaction(saveContext);
            throw;
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

            await _pipeline.ProcessAsync(
                    dbContext,
                    changes,
                    new AuditPipelineSettings
                    {
                        SaveBehavior = AuditSaveBehavior.SaveImmediately,
                        TrackedEntries = saveContext.TrackedEntries
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await CommitOwnedTransactionAsync(saveContext, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RollbackOwnedTransactionAsync(saveContext, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _saveContexts.Remove(dbContext);
        }

        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null
            && _saveContexts.TryGetValue(eventData.Context, out var saveContext))
        {
            RollbackOwnedTransaction(saveContext);
            _saveContexts.Remove(eventData.Context);
        }

        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null
            && _saveContexts.TryGetValue(eventData.Context, out var saveContext))
        {
            await RollbackOwnedTransactionAsync(saveContext, cancellationToken).ConfigureAwait(false);
            _saveContexts.Remove(eventData.Context);
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken).ConfigureAwait(false);
    }

    private void PrepareSaveContext(DbContext dbContext, AuditSaveContext saveContext)
    {
        saveContext.WriteMode = _options.ResolveWriteMode(dbContext, saveContext);

        if (!ShouldOpenOwnedTransaction(dbContext, saveContext))
        {
            return;
        }

        saveContext.Transaction = dbContext.Database.BeginTransaction();
        saveContext.OwnsTransaction = true;
    }

    private async Task PrepareSaveContextAsync(
        DbContext dbContext,
        AuditSaveContext saveContext,
        CancellationToken cancellationToken)
    {
        saveContext.WriteMode = _options.ResolveWriteMode(dbContext, saveContext);

        if (!ShouldOpenOwnedTransaction(dbContext, saveContext))
        {
            return;
        }

        saveContext.Transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        saveContext.OwnsTransaction = true;
    }

    private static bool ShouldOpenOwnedTransaction(DbContext dbContext, AuditSaveContext saveContext)
    {
        if (saveContext.WriteMode != AuditWriteMode.Transactional
            || saveContext.PreSaveChanges.Count == 0
            || dbContext.Database.CurrentTransaction is not null
            || Transaction.Current is not null)
        {
            return false;
        }

        if (dbContext.Database.CreateExecutionStrategy().RetriesOnFailure)
        {
            throw new InvalidOperationException(
                "Transactional audit writing opens an EF Core transaction internally, but the current execution strategy retries on failure. " +
                "Open an explicit transaction outside SaveChanges and execute the whole operation through DbContext.Database.CreateExecutionStrategy(), " +
                "or use AuditWriteMode.NonTransactional.");
        }

        return true;
    }

    private static void CommitOwnedTransaction(AuditSaveContext saveContext)
    {
        if (!saveContext.OwnsTransaction || saveContext.Transaction is null)
        {
            return;
        }

        try
        {
            saveContext.Transaction.Commit();
        }
        finally
        {
            saveContext.Transaction.Dispose();
            saveContext.Transaction = null;
            saveContext.OwnsTransaction = false;
        }
    }

    private static async Task CommitOwnedTransactionAsync(
        AuditSaveContext saveContext,
        CancellationToken cancellationToken)
    {
        if (!saveContext.OwnsTransaction || saveContext.Transaction is null)
        {
            return;
        }

        try
        {
            await saveContext.Transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await saveContext.Transaction.DisposeAsync().ConfigureAwait(false);
            saveContext.Transaction = null;
            saveContext.OwnsTransaction = false;
        }
    }

    private static void RollbackOwnedTransaction(AuditSaveContext saveContext)
    {
        if (!saveContext.OwnsTransaction || saveContext.Transaction is null)
        {
            return;
        }

        try
        {
            saveContext.Transaction.Rollback();
        }
        finally
        {
            saveContext.Transaction.Dispose();
            saveContext.Transaction = null;
            saveContext.OwnsTransaction = false;
        }
    }

    private static async Task RollbackOwnedTransactionAsync(
        AuditSaveContext saveContext,
        CancellationToken cancellationToken)
    {
        if (!saveContext.OwnsTransaction || saveContext.Transaction is null)
        {
            return;
        }

        try
        {
            await saveContext.Transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await saveContext.Transaction.DisposeAsync().ConfigureAwait(false);
            saveContext.Transaction = null;
            saveContext.OwnsTransaction = false;
        }
    }
}

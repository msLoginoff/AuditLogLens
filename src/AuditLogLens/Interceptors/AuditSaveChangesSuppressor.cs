namespace AuditLogLens.Interceptors;

public sealed class AuditSaveChangesSuppressor
{
    // Inspired by Microsoft.Extensions.Logging.LoggerExternalScopeProvider:
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/LoggerExternalScopeProvider.cs
    private readonly AsyncLocal<SuppressionScope?> _currentScope = new();

    public bool IsSuppressed => _currentScope.Value is not null;

    public IDisposable Suppress()
    {
        var parent = _currentScope.Value;
        var scope = new SuppressionScope(this, parent);
        _currentScope.Value = scope;

        return scope;
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly AuditSaveChangesSuppressor _suppressor;
        private bool _disposed;

        public SuppressionScope(
            AuditSaveChangesSuppressor suppressor,
            SuppressionScope? parent)
        {
            _suppressor = suppressor;
            Parent = parent;
        }

        public SuppressionScope? Parent { get; }

        public void Dispose()
        {
            if (_disposed)
                return;

            _suppressor._currentScope.Value = Parent;
            _disposed = true;
        }
    }
}
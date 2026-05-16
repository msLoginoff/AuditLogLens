using System.Collections.Concurrent;
using System.Reflection;

namespace AuditLogLens.Enrichment.Internal;

internal static class EntityPropertyReader
{
    private static readonly ConcurrentDictionary<(Type EntityType, string PropertyName), PropertyInfo> _propertyCache =
        new();

    public static object? GetValue(object entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return GetRequiredProperty(entity.GetType(), propertyName).GetValue(entity);
    }

    private static PropertyInfo GetRequiredProperty(Type entityType, string propertyName)
    {
        return _propertyCache.GetOrAdd(
            (entityType, propertyName),
            static key => key.EntityType.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new InvalidOperationException(
                              $"Property '{key.PropertyName}' was not found on type {key.EntityType.FullName}."));
    }
}
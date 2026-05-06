namespace AuditLogLens.Enrichment;

internal static class LoadedEntityLookup
{
    public static Dictionary<object, object> OneByKey(
        IEnumerable<object> entities,
        Func<object, object?> keySelector)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(keySelector);

        var result = new Dictionary<object, object>();

        foreach (var entity in entities)
        {
            var key = keySelector(entity);
            if (key is null)
                continue;

            result.TryAdd(key, entity);
        }

        return result;
    }

    public static Dictionary<object, List<object>> ManyByKey(
        IEnumerable<object> entities,
        Func<object, object?> keySelector)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(keySelector);

        var result = new Dictionary<object, List<object>>();

        foreach (var entity in entities)
        {
            var key = keySelector(entity);
            if (key is null)
                continue;

            if (!result.TryGetValue(key, out var groupedEntities))
            {
                groupedEntities = [];
                result[key] = groupedEntities;
            }

            groupedEntities.Add(entity);
        }

        return result;
    }
}
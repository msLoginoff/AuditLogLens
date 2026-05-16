using Microsoft.EntityFrameworkCore;

namespace AuditLogLens.Tests.TestObjects;

public sealed class TestAuditEntryMapper : IAuditEntryMapper<TestAuditEntry>
{
    public bool CanMap(DbContext dbContext) => dbContext is AuditTestDbContext;

    public TestAuditEntry Map(AuditChange change, DbContext dbContext)
    {
        return new TestAuditEntry
        {
            TableName = change.TableName,
            EntityId = change.EntityId?.ToString(),
            State = change.State,
            NewName = change.NewValues.TryGetValue(nameof(AllowedEntity.Name), out var name)
                ? name?.ToString()
                : null,
            NewTags = change.NewValues.TryGetValue("Tags", out var tags)
                ? FormatCollection(tags)
                : null
        };
    }

    private static string? FormatCollection(object? value)
    {
        return value is IEnumerable<object?> values
            ? string.Join(",", values)
            : value?.ToString();
    }
}
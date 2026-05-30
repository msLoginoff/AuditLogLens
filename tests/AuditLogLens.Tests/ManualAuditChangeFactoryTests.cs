using AuditLogLens.Changes;
using AuditLogLens.Manual;
using AuditLogLens.Tests.TestObjects;
using Xunit;

namespace AuditLogLens.Tests;

public class ManualAuditChangeFactoryTests
{
    [Fact]
    public void CreateManual_UsesExplicitValuesAndSourceType()
    {
        var source = new ManualAuditPayload
        {
            Name = "payload"
        };
        var oldValues = new Dictionary<string, object?>
        {
            ["Name"] = "Old"
        };
        var newValues = new Dictionary<string, object?>
        {
            ["Name"] = "New"
        };
        var extraValues = new Dictionary<string, object?>
        {
            ["PatientId"] = 42
        };

        var change = new AuditChangeFactory().CreateManual(
            tableName: "ManualEvent",
            rowKey: "manual-1",
            state: AuditChangeState.Modified,
            newValues: newValues,
            oldValues: oldValues,
            source: source,
            sourceType: typeof(AllowedEntity),
            extraValues: extraValues);

        Assert.Equal("ManualEvent", change.TableName);
        Assert.Equal("manual-1", change.EntityId);
        Assert.Equal(AuditChangeState.Modified, change.State);
        Assert.Equal(typeof(AllowedEntity), change.EntityType);
        Assert.Same(source, change.Entity);
        Assert.Equal("Old", change.OldValues["Name"]);
        Assert.Equal("New", change.NewValues["Name"]);
        Assert.Equal(42, change.ExtraValues["PatientId"]);

        oldValues["Name"] = "Changed old";
        newValues["Name"] = "Changed new";
        extraValues["PatientId"] = 100;

        Assert.Equal("Old", change.OldValues["Name"]);
        Assert.Equal("New", change.NewValues["Name"]);
        Assert.Equal(42, change.ExtraValues["PatientId"]);
    }

    private sealed class ManualAuditPayload
    {
        public string? Name { get; init; }
    }
}

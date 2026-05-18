using AuditLogLens.Enrichment.Context;
using AuditLogLens.Tests.TestObjects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuditLogLens.Tests;

public class AuditExtraValuesTests
{
    [Fact]
    public void TryGetExtraValue_ReturnsTypedValueOnlyWhenStoredValueHasRequestedType()
    {
        var change = new AuditChange
        {
            EntityType = typeof(AllowedEntity),
            State = nameof(EntityState.Modified)
        };
        change.SetExtraValue("PatientId", 42);

        Assert.True(change.TryGetExtraValue<int>("PatientId", out var patientId));
        Assert.Equal(42, patientId);

        Assert.False(change.TryGetExtraValue<string>("PatientId", out var textValue));
        Assert.Null(textValue);

        Assert.False(change.TryGetExtraValue<int>("Missing", out var missingValue));
        Assert.Equal(default, missingValue);
    }

    [Fact]
    public void MergeBagsToChanges_CopiesOldNewAndExtraValuesIntoAuditChange()
    {
        var change = new AuditChange
        {
            EntityType = typeof(AllowedEntity),
            State = nameof(EntityState.Modified)
        };
        using var db = new AuditTestDbContext(
            new DbContextOptionsBuilder<AuditTestDbContext>()
                .Options);
        var context = new AuditEnrichmentContext([change], db, []);

        var bag = context.GetBagForChange(change);
        bag.SetOld("Name", "Old");
        bag.SetNew("Name", "New");
        bag.SetExtraValue("PatientId", 42);

        context.MergeBagsToChanges();

        Assert.Equal("Old", change.OldValues["Name"]);
        Assert.Equal("New", change.NewValues["Name"]);
        Assert.Equal(42, change.ExtraValues["PatientId"]);
        Assert.False(bag.HasAnyValues());
    }

    [Fact]
    public void BagRemove_RemovesValuesByKeyBeforeMerge()
    {
        var change = new AuditChange
        {
            EntityType = typeof(AllowedEntity),
            State = nameof(EntityState.Modified)
        };
        using var db = new AuditTestDbContext(
            new DbContextOptionsBuilder<AuditTestDbContext>()
                .Options);
        var context = new AuditEnrichmentContext([change], db, []);

        var bag = context.GetBagForChange(change);
        bag.SetOld("Raw", "Old");
        bag.SetNew("Raw", "New");
        bag.SetExtraValue("Raw", 42);
        bag.SetNew("Keep", "Kept");

        Assert.True(bag.Remove("Raw"));
        Assert.False(bag.Remove("Missing"));

        context.MergeBagsToChanges();

        Assert.False(change.OldValues.ContainsKey("Raw"));
        Assert.False(change.NewValues.ContainsKey("Raw"));
        Assert.False(change.ExtraValues.ContainsKey("Raw"));
        Assert.Equal("Kept", change.NewValues["Keep"]);
    }
}
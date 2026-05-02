using System.Text.Json.Nodes;
using TransactionalAuditLog.Services;

namespace TransactionalAuditLog.Tests;

public sealed class DiffEngineTests
{
    private readonly DiffEngine _engine = new();

    [Fact]
    public void Compute_ChangedField_IncludesOldAndNewValues()
    {
        var before = Parse("""{"name":"John","phone":"555-1234"}""");
        var after  = Parse("""{"name":"John","phone":"555-5678"}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.True(diff.ContainsKey("phone"));
        Assert.Equal("555-1234", diff["phone"]!["old"]!.GetValue<string>());
        Assert.Equal("555-5678", diff["phone"]!["new"]!.GetValue<string>());
    }

    [Fact]
    public void Compute_UnchangedField_ExcludedFromDiff()
    {
        var before = Parse("""{"name":"John","phone":"555-1234"}""");
        var after  = Parse("""{"name":"John","phone":"555-5678"}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.False(diff.ContainsKey("name"));
    }

    [Fact]
    public void Compute_NullToValue_IncludesFieldWithNullOld()
    {
        var before = Parse("""{"name":"John"}""");
        var after  = Parse("""{"name":"John","phone":"555-5678"}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.True(diff.ContainsKey("phone"));
        Assert.Null(diff["phone"]!["old"]);
        Assert.Equal("555-5678", diff["phone"]!["new"]!.GetValue<string>());
    }

    [Fact]
    public void Compute_ValueToNull_IncludesFieldWithNullNew()
    {
        var before = Parse("""{"name":"John","phone":"555-1234"}""");
        var after  = Parse("""{"name":"John"}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.True(diff.ContainsKey("phone"));
        Assert.Equal("555-1234", diff["phone"]!["old"]!.GetValue<string>());
        Assert.Null(diff["phone"]!["new"]);
    }

    [Fact]
    public void Compute_NoChanges_ReturnsEmptyDiff()
    {
        var before = Parse("""{"name":"John","phone":"555-1234"}""");
        var after  = Parse("""{"name":"John","phone":"555-1234"}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.Empty(diff);
    }

    [Fact]
    public void Compute_MultipleChanges_IncludesAllChangedFields()
    {
        var before = Parse("""{"name":"John","phone":"555-1234","notes":"old note"}""");
        var after  = Parse("""{"name":"John","phone":"555-5678","notes":null}""");

        var diff = Parse(_engine.Compute(before, after));

        Assert.Equal(2, diff.Count);
        Assert.True(diff.ContainsKey("phone"));
        Assert.True(diff.ContainsKey("notes"));
    }

    [Fact]
    public void Compute_NullBefore_ThrowsArgumentNullException()
    {
        var after = Parse("""{"name":"John"}""");
        Assert.Throws<ArgumentNullException>(() => _engine.Compute(null!, after));
    }

    [Fact]
    public void Compute_NullAfter_ThrowsArgumentNullException()
    {
        var before = Parse("""{"name":"John"}""");
        Assert.Throws<ArgumentNullException>(() => _engine.Compute(before, null!));
    }

    private static JsonObject Parse(string json) =>
        JsonNode.Parse(json)!.AsObject();
}

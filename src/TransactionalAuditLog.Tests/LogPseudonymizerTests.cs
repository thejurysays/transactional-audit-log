using Microsoft.Extensions.Configuration;
using TransactionalAuditLog.Services;

namespace TransactionalAuditLog.Tests;

public sealed class LogPseudonymizerTests
{
    private static LogPseudonymizer Build(string key = "test-key") =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:PseudonymKey"] = key })
            .Build());

    [Fact]
    public void Pseudonymize_SameInput_ReturnsSamePseudonym()
    {
        var pseudonymizer = Build();

        var first  = pseudonymizer.Pseudonymize("actor-123");
        var second = pseudonymizer.Pseudonymize("actor-123");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Pseudonymize_DifferentInputs_ReturnDifferentPseudonyms()
    {
        var pseudonymizer = Build();

        var a = pseudonymizer.Pseudonymize("actor-123");
        var b = pseudonymizer.Pseudonymize("actor-456");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Pseudonymize_SameInputDifferentKey_ReturnsDifferentPseudonym()
    {
        var p1 = Build("key-one");
        var p2 = Build("key-two");

        Assert.NotEqual(p1.Pseudonymize("actor-123"), p2.Pseudonymize("actor-123"));
    }

    [Fact]
    public void Pseudonymize_ReturnsExactly16HexChars()
    {
        var result = Build().Pseudonymize("actor-123");

        Assert.Equal(16, result.Length);
        Assert.Matches("^[0-9a-f]{16}$", result);
    }

    [Fact]
    public void Constructor_MissingKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build(); // no PseudonymKey

        Assert.Throws<InvalidOperationException>(() => new LogPseudonymizer(config));
    }

    [Fact]
    public void Constructor_WhitespaceKey_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:PseudonymKey"] = "   " })
            .Build();

        Assert.Throws<InvalidOperationException>(() => new LogPseudonymizer(config));
    }
}

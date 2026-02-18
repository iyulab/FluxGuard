using System.Text.RegularExpressions;
using FluentAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Patterns;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

public class PatternRegistryTests
{
    private static PatternDefinition CreatePattern(string id, string name = "test", string regex = "test",
        Severity severity = Severity.Medium, bool enabled = true) => new()
    {
        Id = id,
        Name = name,
        Regex = new Regex(regex, RegexOptions.None, TimeSpan.FromMilliseconds(100)),
        Severity = severity,
        IsEnabled = enabled
    };

    #region Register

    [Fact]
    public void Register_SinglePattern_CanRetrieve()
    {
        var registry = new PatternRegistry();
        var pattern = CreatePattern("p1", "Pattern 1", @"\btest\b");

        registry.Register("injection", pattern);

        registry.HasCategory("injection").Should().BeTrue();
        var patterns = registry.GetPatterns("injection");
        patterns.Should().HaveCount(1);
        patterns[0].Id.Should().Be("p1");
    }

    [Fact]
    public void Register_MultiplePatterns_SameCategory_AllRetrieved()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));
        registry.Register("injection", CreatePattern("p2"));
        registry.Register("injection", CreatePattern("p3"));

        registry.GetPatterns("injection").Should().HaveCount(3);
    }

    [Fact]
    public void Register_DuplicateId_NotAdded()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1", name: "first"));
        registry.Register("injection", CreatePattern("p1", name: "second"));

        var patterns = registry.GetPatterns("injection");
        patterns.Should().HaveCount(1);
        patterns[0].Name.Should().Be("first");
    }

    [Fact]
    public void Register_DifferentCategories_Separate()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));
        registry.Register("pii", CreatePattern("p2"));

        registry.GetPatterns("injection").Should().HaveCount(1);
        registry.GetPatterns("pii").Should().HaveCount(1);
    }

    #endregion

    #region RegisterMany

    [Fact]
    public void RegisterMany_MultiplePatterns_AllRegistered()
    {
        var registry = new PatternRegistry();
        var patterns = new[]
        {
            CreatePattern("p1"),
            CreatePattern("p2"),
            CreatePattern("p3")
        };

        registry.RegisterMany("injection", patterns);

        registry.GetPatterns("injection").Should().HaveCount(3);
    }

    #endregion

    #region GetPatterns

    [Fact]
    public void GetPatterns_NonExistentCategory_ReturnsEmpty()
    {
        var registry = new PatternRegistry();

        registry.GetPatterns("nonexistent").Should().BeEmpty();
    }

    [Fact]
    public void GetPatterns_DisabledPatterns_Excluded()
    {
        var registry = new PatternRegistry();
        registry.Register("cat", CreatePattern("p1", enabled: true));
        registry.Register("cat", CreatePattern("p2", enabled: false));

        registry.GetPatterns("cat").Should().HaveCount(1);
        registry.GetPatterns("cat")[0].Id.Should().Be("p1");
    }

    #endregion

    #region GetAllPatterns

    [Fact]
    public void GetAllPatterns_MultipleCategories_ReturnsAll()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));
        registry.Register("pii", CreatePattern("p2"));
        registry.Register("pii", CreatePattern("p3"));

        var all = registry.GetAllPatterns();

        all.Should().HaveCount(2);
        all["injection"].Should().HaveCount(1);
        all["pii"].Should().HaveCount(2);
    }

    [Fact]
    public void GetAllPatterns_Empty_ReturnsEmpty()
    {
        var registry = new PatternRegistry();

        registry.GetAllPatterns().Should().BeEmpty();
    }

    #endregion

    #region HasCategory

    [Fact]
    public void HasCategory_Existing_ReturnsTrue()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));

        registry.HasCategory("injection").Should().BeTrue();
    }

    [Fact]
    public void HasCategory_NonExisting_ReturnsFalse()
    {
        var registry = new PatternRegistry();

        registry.HasCategory("nonexistent").Should().BeFalse();
    }

    #endregion

    #region DisablePattern

    [Fact]
    public void DisablePattern_ExistingPattern_DisablesIt()
    {
        var registry = new PatternRegistry();
        registry.Register("cat", CreatePattern("p1"));
        registry.Register("cat", CreatePattern("p2"));

        registry.DisablePattern("cat", "p1");

        var patterns = registry.GetPatterns("cat");
        patterns.Should().HaveCount(1);
        patterns[0].Id.Should().Be("p2");
    }

    [Fact]
    public void DisablePattern_NonExistentCategory_NoError()
    {
        var registry = new PatternRegistry();

        // Should not throw
        registry.DisablePattern("nonexistent", "p1");
    }

    [Fact]
    public void DisablePattern_NonExistentPattern_NoError()
    {
        var registry = new PatternRegistry();
        registry.Register("cat", CreatePattern("p1"));

        // Should not throw
        registry.DisablePattern("cat", "nonexistent");
    }

    #endregion

    #region RemoveCategory

    [Fact]
    public void RemoveCategory_Existing_RemovesIt()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));

        registry.RemoveCategory("injection");

        registry.HasCategory("injection").Should().BeFalse();
    }

    [Fact]
    public void RemoveCategory_NonExistent_NoError()
    {
        var registry = new PatternRegistry();

        // Should not throw
        registry.RemoveCategory("nonexistent");
    }

    #endregion

    #region Clear

    [Fact]
    public void Clear_RemovesAllPatterns()
    {
        var registry = new PatternRegistry();
        registry.Register("injection", CreatePattern("p1"));
        registry.Register("pii", CreatePattern("p2"));

        registry.Clear();

        registry.GetAllPatterns().Should().BeEmpty();
        registry.HasCategory("injection").Should().BeFalse();
        registry.HasCategory("pii").Should().BeFalse();
    }

    #endregion
}

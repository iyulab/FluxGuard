using FluxGuard.L1.Patterns.PII;
using AwesomeAssertions;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

/// <summary>
/// The Korean unique identifiers (PIPA Art. 24) every Korean PII detector in the ecosystem must find —
/// <c>pipa-unique-identifiers.tsv</c>, kept byte for byte in FluxCurator's tests too. A sample a pattern misses means the gate
/// lets through a number the masker would hide (or the other way round).
/// </summary>
public class PipaUniqueIdentifierCorpusTests
{
    private static readonly Dictionary<string, string> PatternByKind = new(StringComparer.Ordinal)
    {
        ["RRN"] = "ResidentRegistrationNumber",
        ["Passport"] = "KoreanPassport",
        ["DriversLicense"] = "KoreanDriverLicense",
    };

    public static TheoryData<string, string> Samples()
    {
        var data = new TheoryData<string, string>();
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "L1", "Patterns", "pipa-unique-identifiers.tsv")))
        {
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var parts = line.Split('\t', 2);
            data.Add(parts[0], parts[1]);
        }

        return data;
    }

    [Fact]
    public void The_corpus_names_only_kinds_this_library_maps_and_has_samples_of_each()
    {
        var kinds = Samples().Select(row => row.Data.Item1).Distinct().ToList();

        kinds.Should().BeEquivalentTo(PatternByKind.Keys);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_sample_is_found_by_the_pattern_for_its_kind(string kind, string text)
    {
        var pattern = KoreanPIIPatterns.GetPatterns().Single(p => p.Name == PatternByKind[kind]);

        var found = pattern.Regex.Matches(text).Any(m => pattern.Validator is null || pattern.Validator(m.Value));

        found.Should().BeTrue($"{pattern.Id} ({pattern.Name}) should find the {kind} in «{text}»");
    }
}

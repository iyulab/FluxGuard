using AwesomeAssertions;
using FluxGuard.Abstractions;
using FluxGuard.Core;
using FluxGuard.L1.Guards.Input;
using FluxGuard.L1.Patterns;
using FluxGuard.L1.Patterns.Generated;
using FluxGuard.L1.Patterns.PII;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

/// <summary>
/// Credentials labelled in Korean (PII_KO008) and the language-independent <c>pw</c> shorthand on the
/// English Password pattern (PII009). The English pattern alone flagged none of the labels a Korean
/// operations document actually uses, so a consumer that enabled <c>ko</c> still only caught
/// <c>password:</c>. Pattern-level facts isolate each regex from the other registered patterns (a bank
/// account regex would happily match the digits in a secret).
/// </summary>
public class KoreanCredentialPatternTests
{
    private static PatternDefinition Korean() => KoreanPIIPatterns.GetPatterns().Single(p => p.Id == "PII_KO008");
    private static PatternDefinition English() => PIIPatterns.GetPatterns().Single(p => p.Id == "PII009");

    [Theory]
    [InlineData("비밀번호 : abcd1234")]
    [InlineData("패스워드: abcd1234")]
    [InlineData("비번=abcd1234")]
    [InlineData("암호 = abcd1234")]
    [InlineData("접속 정보 - 비밀번호: S3cr3t!!")]
    public void KoreanPassword_MatchesALabelledSecret(string text)
    {
        Korean().Regex.IsMatch(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("암호화 방식: AES-256", "'암호화' is encryption, not a credential label")]
    [InlineData("비밀번호: 관리자에게 문의", "a Korean phrase after the label is an instruction, not a secret")]
    [InlineData("비번 abcd1234", "no separator - deliberately out of scope for false-positive cost")]
    [InlineData("비밀번호: abc", "shorter than four characters is not a secret")]
    public void KoreanPassword_LeavesTheFalsePositiveShapesAlone(string text, string because)
    {
        Korean().Regex.IsMatch(text).Should().BeFalse(because);
    }

    [Fact]
    public void KoreanPassword_RanksBelowTheEnglishPattern()
    {
        Korean().Confidence.Should().BeLessThan(English().Confidence);
        Korean().Severity.Should().Be(English().Severity);
    }

    [Theory]
    [InlineData("PW : abcd1234")]
    [InlineData("pw=abcd1234")]
    public void EnglishPassword_MatchesThePwShorthand(string text)
    {
        English().Regex.IsMatch(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("swpw=abcd1234", "'pw' inside a longer word is not a label")]
    [InlineData("password: hunter2", "the historical labels still match")]
    public void EnglishPassword_PwShorthandTakesAWordBoundary(string text, string because)
    {
        var expected = text.StartsWith("password", StringComparison.Ordinal);
        English().Regex.IsMatch(text).Should().Be(expected, because);
    }

    [Fact]
    public async Task ExposureGuard_WithKoreanEnabled_FlagsAKoreanLabelledSecret()
    {
        var guard = new L1PIIExposureGuard(new PatternRegistry(), enabledLanguages: ["ko"]);

        var result = await guard.CheckAsync(new GuardContext { OriginalInput = "계정 admin / 비밀번호 : Zx9!qwer" });

        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void KoreanPatterns_StillHaveUniqueIds()
    {
        KoreanPIIPatterns.GetPatterns().Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }
}

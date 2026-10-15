using FluxGuard.Core;
using FluxGuard.Presets;
using AwesomeAssertions;
using Xunit;

namespace FluxGuard.Tests.L1.Patterns;

/// <summary>
/// Numbers that are not personal data — order and invoice numbers, build numbers, timestamps, serials — must not be read
/// as national ID, passport or account numbers. A number-shaped PII pattern is kept only in a form the number cannot take
/// by accident: a verified check digit, the written form with separators, or a label naming the kind of number.
/// </summary>
public class BenignNumberProbeTests
{
    private static readonly IFluxGuard Guard = FluxGuardBuilder.Create(b => b.ApplyStandardPreset()).Build();

    [Theory]
    [InlineData("주문번호 123456789 배송 상태 알려줘")]
    [InlineData("Invoice 482915736 is overdue, draft a reminder")]
    [InlineData("빌드 202610161234 에서 실패한 테스트 목록")]
    [InlineData("Ticket 4821-5539-1170 was closed yesterday")]
    [InlineData("시리얼 번호 1234 5678 9012 장비의 펌웨어 버전")]
    [InlineData("타임스탬프 1792104021927 를 날짜로 바꿔줘")]
    [InlineData("Order 987654321012 shipped")]
    [InlineData("Shipment 4821553911701234 is delayed")]
    [InlineData("주문번호 202610160001 상태를 알려줘")]          // passes the My Number check digit, has no label
    public async Task Input_BenignNumber_IsNotBlocked(string input)
    {
        var result = await Guard.CheckInputAsync(input, TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse(result.BlockReason);
    }

    [Theory]
    [InlineData("The order number is 123456789.")]
    [InlineData("주문번호는 482915736 입니다.")]
    [InlineData("Build 20261016 passed; artifact 98765432 is ready.")]
    [InlineData("The device serial is 1234-5678-9012.")]
    [InlineData("Ticket 1234567 was merged into 7654321.")]
    public async Task Output_BenignNumber_IsNotBlocked(string output)
    {
        var result = await Guard.CheckOutputAsync("question", output, TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeFalse(result.BlockReason);
    }

    [Theory]
    [InlineData("My card is 4111 1111 1111 1111", "CreditCard")]          // grouped, Luhn-valid Visa test number
    [InlineData("My card is 4111111111111111", "CreditCard")]
    [InlineData("My SSN is 123-45-6789", "SSN")]                           // the written form
    [InlineData("SSN: 123456789", "SSN")]                                  // bare digits with the label
    [InlineData("social security number 123 45 6789", "SSN")]
    [InlineData("My ITIN is 912-70-1234", "USITIN")]
    [InlineData("マイナンバーは 123456789018 です", "MyNumber")]           // valid check digit
    [InlineData("마이넘버 1234 5678 9018", "MyNumber")]
    [InlineData("주민등록번호 900101-1234567", "ResidentRegistrationNumber")]
    public async Task Input_RealNumberShapedPii_StillBlocks(string input, string pattern)
    {
        var result = await Guard.CheckInputAsync(input, TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain(pattern);
    }

    [Theory]
    [InlineData("The IBAN is GB29 NWBK 6016 1331 9268 19", "IBAN")]         // printed groups, valid check digits
    [InlineData("The passport number is 123456789.", "USPassport")]
    [InlineData("EIN 12-3456789 is on the form.", "USEIN")]
    [InlineData("Employer identification number 123456789.", "USEIN")]
    [InlineData("입금 계좌 110-123-456789 로 보내주세요.", "KoreanBankAccount")]
    [InlineData("운전면허 번호는 11-22-333333-44 입니다.", "KoreanDriverLicense")]
    [InlineData("口座番号 1234567 にお振込みください", "JapaneseBankAccount")]
    [InlineData("運転免許証番号 123456789012", "JapaneseDriverLicense")]
    public async Task Output_LabelledNumberShapedPii_StillBlocks(string output, string pattern)
    {
        var result = await Guard.CheckOutputAsync("question", output, TestContext.Current.CancellationToken);

        result.IsBlocked.Should().BeTrue();
        result.BlockReason.Should().Contain(pattern);
    }

    [Theory]
    [InlineData("사업자등록번호 220-81-62517", true)]   // valid check digit
    [InlineData("사업자등록번호 123-45-67890", false)]
    public async Task Input_KoreanBusinessNumber_NeedsItsCheckDigit(string input, bool detected)
    {
        var result = await Guard.CheckInputAsync(input, TestContext.Current.CancellationToken);

        result.TriggeredGuards.Any(t => t.Pattern == "KoreanBusinessNumber").Should().Be(detected);
    }
}

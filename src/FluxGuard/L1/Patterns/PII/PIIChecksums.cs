using FluxGuard.Abstractions;

namespace FluxGuard.L1.Patterns.PII;

/// <summary>
/// Check-digit and checksum tests for number-shaped PII formats that carry one (<see cref="PatternDefinition.Validator"/>).
/// A number of the right length that fails its check is not that kind of number — an order or ticket number, a serial —
/// and must not be reported as PII.
/// </summary>
internal static class PIIChecksums
{
    /// <summary>
    /// A payment card number: 13–19 digits (separators ignored), an issuer prefix of a major scheme, and a valid Luhn
    /// check digit.
    /// </summary>
    public static bool IsPaymentCardNumber(string text)
    {
        var digits = DigitsOf(text);
        if (digits.Length is < 13 or > 19 || !HasCardIssuerPrefix(digits))
            return false;

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }

            sum += d;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// A Japanese Individual Number (マイナンバー): 12 digits whose last digit is the check digit over the first eleven
    /// (weights 2–7 then 2–6 from the right, modulo 11).
    /// </summary>
    public static bool IsMyNumber(string text)
    {
        var digits = DigitsOf(text);
        if (digits.Length != 12)
            return false;

        var sum = 0;
        for (var n = 1; n <= 11; n++)
        {
            var p = digits[11 - n] - '0';
            var q = n <= 6 ? n + 1 : n - 5;
            sum += p * q;
        }

        var remainder = sum % 11;
        var check = remainder <= 1 ? 0 : 11 - remainder;
        return check == digits[11] - '0';
    }

    /// <summary>
    /// A Korean business registration number (사업자등록번호): 10 digits with the National Tax Service check digit.
    /// </summary>
    public static bool IsKoreanBusinessNumber(string text)
    {
        var digits = DigitsOf(text);
        if (digits.Length != 10)
            return false;

        int[] weights = [1, 3, 7, 1, 3, 7, 1, 3, 5];
        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (digits[i] - '0') * weights[i];
        sum += (digits[8] - '0') * 5 / 10;

        return (10 - sum % 10) % 10 == digits[9] - '0';
    }

    /// <summary>An IBAN whose check digits verify (ISO 13616, modulo 97 = 1).</summary>
    public static bool IsIban(string text)
    {
        var iban = text.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (iban.Length is < 15 or > 34)
            return false;

        var rearranged = string.Concat(iban.AsSpan(4), iban.AsSpan(0, 4));
        var remainder = 0;
        foreach (var c in rearranged)
        {
            int value;
            if (c is >= '0' and <= '9') value = c - '0';
            else if (c is >= 'A' and <= 'Z') value = c - 'A' + 10;
            else return false;

            remainder = value >= 10 ? (remainder * 100 + value) % 97 : (remainder * 10 + value) % 97;
        }

        return remainder == 1;
    }

    private static bool HasCardIssuerPrefix(string digits)
    {
        var two = int.Parse(digits.AsSpan(0, 2), System.Globalization.CultureInfo.InvariantCulture);
        var four = int.Parse(digits.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);

        return digits[0] == '4'                       // Visa
            || two is >= 51 and <= 55                 // Mastercard
            || four is >= 2221 and <= 2720            // Mastercard 2-series
            || two is 34 or 37                        // American Express
            || four == 6011 || two == 65              // Discover
            || four is >= 3528 and <= 3589            // JCB
            || two is 36 or 38 || (digits[0] == '3' && digits[1] == '0')  // Diners Club
            || two == 62;                             // UnionPay
    }

    private static string DigitsOf(string text) => new(text.Where(char.IsAsciiDigit).ToArray());
}

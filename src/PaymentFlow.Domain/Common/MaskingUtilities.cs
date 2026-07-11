namespace PaymentFlow.Domain.Common;

public static class MaskingUtilities
{
    /// <summary>
    /// Masks all but the last four characters of an account/IBAN number.
    /// Centralized so no DTO mapping can accidentally leak the full value.
    /// </summary>
    public static string MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return string.Empty;

        var trimmed = accountNumber.Trim();
        if (trimmed.Length <= 4)
            return new string('*', trimmed.Length);

        return string.Concat(new string('*', trimmed.Length - 4), trimmed[^4..]);
    }
}

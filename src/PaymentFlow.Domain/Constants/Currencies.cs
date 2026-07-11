namespace PaymentFlow.Domain.Constants;

public static class Currencies
{
    // Fictional platform supports a small ISO 4217 allow-list.
    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        "USD", "EUR", "GBP", "JOD", "AED", "SAR", "JPY", "CHF", "CAD", "AUD"
    };

    public static bool IsSupported(string? code) =>
        !string.IsNullOrWhiteSpace(code) && Supported.Contains(code);
}

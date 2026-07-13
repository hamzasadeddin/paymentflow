namespace PaymentFlow.Application.Abstractions;

/// <summary>A single line on the external bank statement being reconciled against.</summary>
public sealed record StatementLine(string Reference, decimal Amount, string Currency, DateTime ValueDateUtc);

/// <summary>
/// Supplies the external bank statement to reconcile settled payments against.
/// Behind an interface so the source is swappable: the demo implementation in
/// Infrastructure derives lines from completed payments with deterministic drift,
/// but a real bank-feed integration would drop in here unchanged from the caller's
/// point of view.
/// </summary>
public interface IExternalStatementProvider
{
    /// <summary>The statement as of <paramref name="asOfUtc"/> (typically "now").</summary>
    Task<IReadOnlyList<StatementLine>> GetStatementAsync(DateTime asOfUtc, CancellationToken cancellationToken);
}

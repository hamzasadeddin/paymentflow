using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Reconciliation;

/// <summary>
/// <see cref="IExternalStatementProvider"/> backed by the admin-editable rule store
/// with the <c>appsettings</c>-bound <see cref="ReconciliationOptions"/> as the
/// fallback (Phase 07). The statement mirrors the completed payments (the ledger is
/// the source of truth), then — when <see cref="ReconciliationOptions.IntroduceSyntheticBreaks"/>
/// is on — applies a <b>deterministic</b> drift so every run reproducibly yields one
/// of each break type: a dropped line (missing-from-statement), a bumped amount
/// (amount-mismatch), and a phantom line (missing-from-ledger). With the drift off,
/// the statement matches the ledger exactly (a clean, zero-break run). The
/// reconciliation engine is unchanged.
/// </summary>
public sealed class SimulatedStatementProvider(
    IApplicationDbContext db,
    IRuleSettingsProvider rules,
    Microsoft.Extensions.Options.IOptions<ReconciliationOptions> configFallback)
    : IExternalStatementProvider
{
    private readonly ReconciliationOptions _configFallback = configFallback.Value;

    public async Task<IReadOnlyList<StatementLine>> GetStatementAsync(
        DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var options = rules.GetEffective(ReconciliationOptions.SectionName, _configFallback);

        // Mirror the ledger: one line per completed payment, ordered by reference
        // so the drift picks are stable across runs.
        var completed = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed)
            .OrderBy(p => p.PaymentReference)
            .Select(p => new { p.PaymentReference, p.Amount, p.Currency, p.UpdatedAtUtc, p.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var drift = options.IntroduceSyntheticBreaks;
        var driftAmount = options.AmountDriftMinorUnits / 100m;
        var bumpApplied = false;

        var lines = new List<StatementLine>(completed.Count + 1);

        foreach (var p in completed)
        {
            // Missing-from-statement: drop the payment whose reference ends in the configured digit.
            if (drift && !string.IsNullOrEmpty(options.DropReferenceEndingIn)
                && p.PaymentReference.EndsWith(options.DropReferenceEndingIn, StringComparison.Ordinal))
                continue;

            var amount = p.Amount;

            // Amount-mismatch: bump exactly one surviving line by the configured drift.
            if (drift && !bumpApplied && driftAmount > 0)
            {
                amount += driftAmount;
                bumpApplied = true;
            }

            lines.Add(new StatementLine(
                p.PaymentReference, amount, p.Currency, p.UpdatedAtUtc ?? p.CreatedAtUtc));
        }

        // Missing-from-ledger: a phantom line with no matching payment.
        if (drift && options.PhantomAmount > 0)
        {
            lines.Add(new StatementLine(
                $"PHANTOM-{asOfUtc:yyyyMMdd}", options.PhantomAmount, "USD", asOfUtc));
        }

        return lines;
    }
}

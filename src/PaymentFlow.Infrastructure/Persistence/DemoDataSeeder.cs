using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentFlow.Domain.Entities;
using PaymentFlow.Application.Common;
using PaymentFlow.Infrastructure.Approvals;
using PaymentFlow.Infrastructure.Identity;

namespace PaymentFlow.Infrastructure.Persistence;

/// <summary>
/// Seeds fictional customers, accounts, beneficiaries, and payments so the UI is
/// demoable on first run. Idempotent: does nothing if customers already exist.
///
/// Seeded records carry a maker (the analyst demo user) and are approved by
/// <b>different</b> users (approver / admin), so separation of duties is visibly
/// satisfied. One partially-approved dual-control payment is seeded so the
/// Approvals queue is populated on first run.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PaymentFlowDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        if (await db.Customers.AnyAsync())
            return;

        // Resolve demo user ids so seeded records have a real maker/checker split.
        // When demo users are absent (no demo password configured), fall back to
        // a neutral "seed" marker and null maker.
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var analystId = (await userManager.FindByEmailAsync("analyst@paymentflow.local"))?.Id.ToString();
        var approverId = (await userManager.FindByEmailAsync("approver@paymentflow.local"))?.Id.ToString();
        var adminId = (await userManager.FindByEmailAsync("admin@paymentflow.local"))?.Id.ToString();

        // Distinct approvers for the checker side (never the maker/analyst).
        var approvers = new[] { approverId, adminId }.Where(x => x is not null).Select(x => x!).ToList();
        if (approvers.Count == 0)
            approvers.Add("seed");

        var customers = new List<Customer>
        {
            NewIndividual("Layla Haddad", "layla.haddad@example.com", "JO", 1),
            NewIndividual("Marcus Bennett", "marcus.bennett@example.com", "GB", 2),
            NewBusiness("Cedar Trading LLC", "ap@cedartrading.example", "AE", 3),
            NewBusiness("Northwind Logistics", "finance@northwind.example", "US", 4),
            NewIndividual("Sofia Rossi", "sofia.rossi@example.com", "CH", 5)
        };

        // Give each customer one or two fictional accounts.
        var laylaJod = NewAccount("112233445566", "JOD", 15250.75m, 5000m);
        var cedarAed = NewAccount("445566778899", "AED", 92000.00m, 25000m);
        var northwindUsd = NewAccount("556677889900", "USD", 47310.20m, 20000m);

        customers[0].AddAccount(laylaJod);
        customers[1].AddAccount(NewAccount("223344556677", "GBP", 8400.00m, 3000m));
        customers[1].AddAccount(NewAccount("334455667788", "EUR", 1200.50m, 2000m));
        customers[2].AddAccount(cedarAed);
        customers[3].AddAccount(northwindUsd);
        customers[4].AddAccount(NewAccount("667788990011", "CHF", 5600.00m, 4000m));

        db.Customers.AddRange(customers);

        var ammanUtilities = NewBeneficiary(customers[0].Id, "Amman Utilities Co", "998877665544", "Arab Bank", "JOD", analystId);
        var gulfFreight = NewBeneficiary(customers[2].Id, "Gulf Freight Services", "887766554433", "Emirates NBD", "AED", analystId);
        var pacificSuppliers = NewBeneficiary(customers[3].Id, "Pacific Suppliers Inc", "776655443322", "Chase", "USD", analystId);
        db.Beneficiaries.AddRange(ammanUtilities, gulfFreight, pacificSuppliers);

        // A spread of payments across the lifecycle. Approved/Completed payments
        // apply their reservation/settlement so the seeded balances stay honest,
        // and every decision is recorded as an ApprovalDecision for the audit trail.
        var now = DateTime.UtcNow;
        var decisions = new List<ApprovalDecision>();

        // Approved to Gulf Freight — carries a compliance hold below, so it is
        // visibly held from settlement (the worker skips it) until cleared.
        var approvedFreight = NewPayment(cedarAed, gulfFreight, 5000.00m, "PAY-2026-000003",
            PaymentStatus.Approved, now, "Freight settlement", analystId, approvers, decisions);

        db.Payments.AddRange(
            NewPayment(laylaJod, ammanUtilities, 250.00m, "PAY-2026-000001", PaymentStatus.Draft, now, "Monthly utilities", analystId, approvers, decisions),
            NewPayment(northwindUsd, pacificSuppliers, 1200.00m, "PAY-2026-000002", PaymentStatus.PendingApproval, now, "Supplier invoice #4471", analystId, approvers, decisions),
            approvedFreight,
            NewPayment(northwindUsd, pacificSuppliers, 800.00m, "PAY-2026-000004", PaymentStatus.Completed, now, "Parts order", analystId, approvers, decisions),
            NewPayment(laylaJod, ammanUtilities, 1500.00m, "PAY-2026-000005", PaymentStatus.Rejected, now, "Duplicate request", analystId, approvers, decisions),
            // A second Completed payment (ref not ending in 4) so a first-run
            // reconciliation shows all three break types, not just two. Kept
            // contiguous with the sequence below — CreatePaymentCommand derives the
            // next reference from the payment COUNT, so gaps would collide.
            NewPayment(northwindUsd, pacificSuppliers, 1750.00m, "PAY-2026-000006", PaymentStatus.Completed, now, "Warehouse fittings", analystId, approvers, decisions));

        // A dual-control payment awaiting its second approver (1 of 2) — populates
        // the Approvals queue with a partially-approved item on first run.
        var dualPending = NewPayment(cedarAed, gulfFreight, 6000.00m, "PAY-2026-000007",
            PaymentStatus.PendingApproval, now, "Equipment purchase (dual approval)", analystId, approvers, decisions);
        db.Payments.Add(dualPending);
        decisions.Add(Decision(dualPending.Id, approvers[0], ApprovalOutcome.Approved, "First approval recorded", now));

        db.ApprovalDecisions.AddRange(decisions);

        // A seeded OPEN compliance hold on the approved Gulf Freight payment, so
        // the Compliance queue is populated on first run and that payment cannot
        // settle until an officer clears it. Curated directly (like the seeded
        // payment states) rather than run through the screening service.
        db.ComplianceCases.Add(new ComplianceCase
        {
            PaymentId = approvedFreight.Id,
            PaymentReference = approvedFreight.PaymentReference,
            Category = ComplianceCategory.Sanctions,
            Reason = "Beneficiary \"Gulf Freight Services\" matched the watchlist term \"Gulf Freight\".",
            RaisedByUserId = null,
            CreatedAtUtc = now
        });

        await db.SaveChangesAsync();
        logger.LogInformation(
            "Seeded {Customers} demo customers with accounts, beneficiaries, and payments", customers.Count);
    }

    private static Customer NewIndividual(string name, string email, string country, int seq) => new()
    {
        Type = CustomerType.Individual,
        Name = name,
        Email = email,
        CountryCode = country,
        CustomerReference = $"CUST-2026-{seq:D6}",
        Status = CustomerStatus.Active
    };

    private static Customer NewBusiness(string name, string email, string country, int seq) => new()
    {
        Type = CustomerType.Business,
        Name = name,
        Email = email,
        CountryCode = country,
        CustomerReference = $"CUST-2026-{seq:D6}",
        Status = CustomerStatus.Active
    };

    private static PaymentAccount NewAccount(string number, string currency, decimal balance, decimal limit) => new()
    {
        AccountNumber = number,
        Currency = currency,
        AvailableBalance = balance,
        LedgerBalance = balance,
        DailyLimit = limit,
        Status = AccountStatus.Active
    };

    private static Beneficiary NewBeneficiary(
        Guid customerId, string name, string number, string bank, string currency, string? makerId)
    {
        var b = new Beneficiary
        {
            CustomerId = customerId,
            Name = name,
            AccountNumber = number,
            BankName = bank,
            Currency = currency,
            CreatedByUserId = makerId
        };
        b.SubmitForApproval(DateTime.UtcNow);
        b.Approve("seed", "Pre-approved demo beneficiary", DateTime.UtcNow);
        return b;
    }

    /// <summary>
    /// Builds a payment and walks it to <paramref name="target"/> via the domain
    /// state machine, applying the same reservation/settlement effects to the
    /// source account that the real handlers would, and recording an
    /// <see cref="ApprovalDecision"/> for each approve/reject so the demo data is
    /// coherent with the approval engine.
    /// </summary>
    private static Payment NewPayment(
        PaymentAccount account, Beneficiary beneficiary, decimal amount, string reference,
        PaymentStatus target, DateTime now, string? description,
        string? makerId, IReadOnlyList<string> approvers, List<ApprovalDecision> decisions)
    {
        var required = ApprovalPolicyProvider.RequiredApprovalsFor(amount, ApprovalPolicyOptions.Defaults);

        var payment = new Payment
        {
            PaymentReference = reference,
            SourceAccountId = account.Id,
            BeneficiaryId = beneficiary.Id,
            Amount = amount,
            Currency = account.Currency,
            Description = description,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedByUserId = makerId,
            CreatedAtUtc = now
        };

        if (target == PaymentStatus.Draft)
            return payment;

        payment.SubmitForApproval(required, now);
        if (target == PaymentStatus.PendingApproval)
            return payment;

        if (target == PaymentStatus.Rejected)
        {
            var reviewer = approvers[0];
            payment.Reject(reviewer, "Demo rejected payment", now);
            decisions.Add(Decision(payment.Id, reviewer, ApprovalOutcome.Rejected, "Demo rejected payment", now));
            return payment;
        }

        if (target == PaymentStatus.Cancelled)
        {
            payment.Cancel(now);
            return payment;
        }

        // Approve reserves funds from the account, and records the decision(s).
        account.Reserve(amount);
        if (required == 0)
        {
            payment.Approve(ApprovalDecision.AutoApprover, "Auto-approved (below approval threshold).", now);
            decisions.Add(Decision(payment.Id, ApprovalDecision.AutoApprover,
                ApprovalOutcome.Approved, "Auto-approved (below approval threshold).", now));
        }
        else
        {
            var used = approvers.Distinct(StringComparer.Ordinal).Take(required).ToList();
            foreach (var a in used)
                decisions.Add(Decision(payment.Id, a, ApprovalOutcome.Approved, "Pre-approved demo payment", now));
            payment.Approve(used[^1], "Pre-approved demo payment", now);
        }

        if (target == PaymentStatus.Approved)
            return payment;

        payment.MarkProcessing(now);
        if (target == PaymentStatus.Processing)
            return payment;

        if (target == PaymentStatus.Completed)
        {
            account.Settle(amount);
            payment.Complete(now);
        }
        else if (target == PaymentStatus.Failed)
        {
            account.ReleaseReservation(amount);
            payment.Fail("Simulated processing failure", now);
        }

        return payment;
    }

    private static ApprovalDecision Decision(
        Guid subjectId, string approver, ApprovalOutcome outcome, string? notes, DateTime now) => new()
    {
        SubjectType = ApprovalSubjectType.Payment,
        SubjectId = subjectId,
        ApproverUserId = approver,
        Decision = outcome,
        Notes = notes,
        DecidedAtUtc = now
    };
}

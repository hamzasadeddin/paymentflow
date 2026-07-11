using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Persistence;

/// <summary>
/// Seeds fictional customers, accounts, and beneficiaries so the UI is
/// demoable on first run. Idempotent: does nothing if customers already exist.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<PaymentFlowDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DemoDataSeeder");

        if (await db.Customers.AnyAsync())
            return;

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

        var ammanUtilities = NewBeneficiary(customers[0].Id, "Amman Utilities Co", "998877665544", "Arab Bank", "JOD");
        var gulfFreight = NewBeneficiary(customers[2].Id, "Gulf Freight Services", "887766554433", "Emirates NBD", "AED");
        var pacificSuppliers = NewBeneficiary(customers[3].Id, "Pacific Suppliers Inc", "776655443322", "Chase", "USD");
        db.Beneficiaries.AddRange(ammanUtilities, gulfFreight, pacificSuppliers);

        // A spread of payments across the lifecycle. Approved/Completed payments
        // apply their reservation/settlement so the seeded balances stay honest.
        var now = DateTime.UtcNow;
        db.Payments.AddRange(
            NewPayment(laylaJod, ammanUtilities, 250.00m, "PAY-2026-000001", PaymentStatus.Draft, now, "Monthly utilities"),
            NewPayment(northwindUsd, pacificSuppliers, 1200.00m, "PAY-2026-000002", PaymentStatus.PendingApproval, now, "Supplier invoice #4471"),
            NewPayment(cedarAed, gulfFreight, 5000.00m, "PAY-2026-000003", PaymentStatus.Approved, now, "Freight settlement"),
            NewPayment(northwindUsd, pacificSuppliers, 800.00m, "PAY-2026-000004", PaymentStatus.Completed, now, "Parts order"),
            NewPayment(laylaJod, ammanUtilities, 999.00m, "PAY-2026-000005", PaymentStatus.Rejected, now, "Duplicate request"));

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

    private static Beneficiary NewBeneficiary(Guid customerId, string name, string number, string bank, string currency)
    {
        var b = new Beneficiary
        {
            CustomerId = customerId,
            Name = name,
            AccountNumber = number,
            BankName = bank,
            Currency = currency
        };
        b.SubmitForApproval(DateTime.UtcNow);
        b.Approve("seed", "Pre-approved demo beneficiary", DateTime.UtcNow);
        return b;
    }

    /// <summary>
    /// Builds a payment and walks it to <paramref name="target"/> via the domain
    /// state machine, applying the same reservation/settlement effects to the
    /// source account that the real handlers would, so the demo data is coherent.
    /// </summary>
    private static Payment NewPayment(
        PaymentAccount account, Beneficiary beneficiary, decimal amount, string reference,
        PaymentStatus target, DateTime now, string? description)
    {
        var payment = new Payment
        {
            PaymentReference = reference,
            SourceAccountId = account.Id,
            BeneficiaryId = beneficiary.Id,
            Amount = amount,
            Currency = account.Currency,
            Description = description,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now
        };

        if (target == PaymentStatus.Draft)
            return payment;

        payment.SubmitForApproval(now);
        if (target == PaymentStatus.PendingApproval)
            return payment;

        if (target == PaymentStatus.Rejected)
        {
            payment.Reject("seed", "Demo rejected payment", now);
            return payment;
        }

        if (target == PaymentStatus.Cancelled)
        {
            payment.Cancel(now);
            return payment;
        }

        // Approve reserves funds from the account.
        account.Reserve(amount);
        payment.Approve("seed", "Pre-approved demo payment", now);
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
}

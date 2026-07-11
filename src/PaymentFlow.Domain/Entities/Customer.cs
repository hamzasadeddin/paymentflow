using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public enum CustomerType { Individual = 1, Business = 2 }
public enum CustomerStatus { Active = 1, Inactive = 2, Suspended = 3 }

public class Customer : AuditableEntity
{
    public CustomerType Type { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    /// <summary>Full legal/display name (person name or business name).</summary>
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }

    /// <summary>Externally shareable identifier, e.g. CUST-2026-000123.</summary>
    public string CustomerReference { get; set; } = string.Empty;

    public string? CountryCode { get; set; }

    private readonly List<PaymentAccount> _accounts = [];
    public IReadOnlyCollection<PaymentAccount> Accounts => _accounts.AsReadOnly();

    public void AddAccount(PaymentAccount account) => _accounts.Add(account);
}

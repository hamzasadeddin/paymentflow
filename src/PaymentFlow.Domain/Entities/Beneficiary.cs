using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

public enum BeneficiaryStatus { Draft = 1, PendingApproval = 2, Approved = 3, Rejected = 4 }

public class Beneficiary : AuditableEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string? BankName { get; set; }
    public string? BankIdentifierCode { get; set; }
    public string Currency { get; set; } = "USD";
    public string? CountryCode { get; set; }

    /// <summary>The maker: the user who created this beneficiary. Used to block self-approval.</summary>
    public string? CreatedByUserId { get; set; }

    public BeneficiaryStatus Status { get; private set; } = BeneficiaryStatus.Draft;
    public string? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    public string MaskedNumber => MaskingUtilities.MaskAccountNumber(AccountNumber);

    // Explicit transition rules keep the lifecycle valid regardless of caller.
    public bool CanEdit => Status is BeneficiaryStatus.Draft or BeneficiaryStatus.Rejected;

    public void SubmitForApproval(DateTime utcNow)
    {
        if (Status is not (BeneficiaryStatus.Draft or BeneficiaryStatus.Rejected))
            throw new InvalidOperationException($"Cannot submit a beneficiary in status {Status}.");

        Status = BeneficiaryStatus.PendingApproval;
        ReviewedByUserId = null;
        ReviewedAtUtc = null;
        ReviewNotes = null;
        Touch(utcNow);
    }

    public void Approve(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != BeneficiaryStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve a beneficiary in status {Status}.");

        Status = BeneficiaryStatus.Approved;
        Stamp(reviewerUserId, notes, utcNow);
    }

    public void Reject(string reviewerUserId, string? notes, DateTime utcNow)
    {
        if (Status != BeneficiaryStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot reject a beneficiary in status {Status}.");

        Status = BeneficiaryStatus.Rejected;
        Stamp(reviewerUserId, notes, utcNow);
    }

    private void Stamp(string reviewerUserId, string? notes, DateTime utcNow)
    {
        ReviewedByUserId = reviewerUserId;
        ReviewNotes = notes;
        ReviewedAtUtc = utcNow;
        Touch(utcNow);
    }
}

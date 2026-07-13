using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

// ---------- Submit ----------
public record SubmitPaymentCommand(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class SubmitPaymentCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, IApprovalPolicyProvider policy,
    IComplianceScreeningService screening, IOptions<ScreeningOptions> screeningOptions)
    : IRequestHandler<SubmitPaymentCommand, Result<PaymentDto>>
{
    private readonly ScreeningOptions _screening = screeningOptions.Value;

    public async Task<Result<PaymentDto>> Handle(
        SubmitPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        if (payment.Beneficiary is { Status: not BeneficiaryStatus.Approved })
            return Result.Failure<PaymentDto>(Error.Conflict("payment.beneficiaryNotApproved",
                "The beneficiary must be approved before this payment can be submitted."));

        // Fail-fast funds/limit check (authoritative reservation happens on final approval).
        var fundsError = await PaymentGuards.CheckDebitableAsync(
            db, payment.SourceAccount!, payment, clock.UtcNow, cancellationToken);
        if (fundsError is not null)
            return Result.Failure<PaymentDto>(fundsError);

        // Resolve and stamp how many approvers this payment needs, from the policy.
        var requirement = policy.Resolve(payment.Amount);

        try
        {
            payment.SubmitForApproval(requirement.RequiredApprovals, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        // Auto-approve band: below the threshold, the payment clears on submit —
        // funds are reserved immediately and it is stamped as an auto decision.
        if (requirement.AutoApproves)
        {
            var account = payment.SourceAccount!;
            try
            {
                account.Reserve(payment.Amount);
                account.Touch(clock.UtcNow);
                payment.Approve(ApprovalDecision.AutoApprover,
                    "Auto-approved (below approval threshold).", clock.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
            }

            db.ApprovalDecisions.Add(new ApprovalDecision
            {
                SubjectType = ApprovalSubjectType.Payment,
                SubjectId = payment.Id,
                ApproverUserId = ApprovalDecision.AutoApprover,
                Decision = ApprovalOutcome.Approved,
                Notes = "Auto-approved (below approval threshold).",
                DecidedAtUtc = clock.UtcNow
            });
            db.SecurityAuditEvents.Add(new SecurityAuditEvent
            {
                EventType = SecurityEventTypes.PaymentApproved,
                Succeeded = true,
                Details = $"{payment.PaymentReference} auto-approved (amount below approval threshold).",
                OccurredAtUtc = clock.UtcNow
            });
        }

        // Compliance screening: a flag raises an OPEN hold that gates settlement.
        // Screening is independent of approval — the payment still flows through
        // maker-checker; the hold only bites when the payment tries to process.
        if (_screening.AutoScreenOnSubmit && payment.Beneficiary is not null)
        {
            var screen = screening.Screen(payment, payment.Beneficiary);
            if (screen.Flagged)
            {
                db.ComplianceCases.Add(new ComplianceCase
                {
                    PaymentId = payment.Id,
                    PaymentReference = payment.PaymentReference,
                    Category = screen.Category,
                    Reason = screen.Reason,
                    RaisedByUserId = null, // automatic screen
                    CreatedAtUtc = clock.UtcNow
                });
                db.SecurityAuditEvents.Add(new SecurityAuditEvent
                {
                    EventType = SecurityEventTypes.ComplianceHoldPlaced,
                    Succeeded = true,
                    Details = $"{payment.PaymentReference} held for compliance review: {screen.Reason}",
                    OccurredAtUtc = clock.UtcNow
                });
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.concurrencyConflict",
                "The payment or account was modified by someone else. Reload and try again."));
        }

        return Result.Success(payment.ToDto());
    }
}

// ---------- Cancel ----------
public record CancelPaymentCommand(Guid PaymentId) : IRequest<Result<PaymentDto>>;

public sealed class CancelPaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CancelPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        try
        {
            payment.Cancel(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(payment.ToDto());
    }
}

// ---------- Approve / Reject (maker-checker approval engine) ----------
public enum PaymentReviewAction { Approve, Reject }

public record TransitionPaymentCommand(
    Guid PaymentId, PaymentReviewAction Action,
    string? ReviewerUserId, string? ReviewerEmail, string? Notes)
    : IRequest<Result<PaymentDto>>;

public sealed class TransitionPaymentCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<TransitionPaymentCommand, Result<PaymentDto>>
{
    public async Task<Result<PaymentDto>> Handle(
        TransitionPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments
            .Include(p => p.SourceAccount)
            .Include(p => p.Beneficiary)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentDto>(Error.NotFound("payment.notFound", "Payment not found."));

        // The approver's identity must be known for separation of duties to hold.
        if (string.IsNullOrWhiteSpace(request.ReviewerUserId))
            return Result.Failure<PaymentDto>(Error.Forbidden("payment.unknownReviewer",
                "The approver identity could not be determined."));

        var reviewer = request.ReviewerUserId;

        // Separation of duties: the maker can never be a checker.
        if (!string.IsNullOrEmpty(payment.CreatedByUserId)
            && string.Equals(payment.CreatedByUserId, reviewer, StringComparison.Ordinal))
            return Result.Failure<PaymentDto>(Error.Forbidden("payment.selfApprovalNotAllowed",
                "You cannot approve or reject a payment you created (separation of duties)."));

        if (request.Action == PaymentReviewAction.Reject)
        {
            try
            {
                payment.Reject(reviewer, request.Notes, clock.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
            }

            RecordDecision(payment.Id, reviewer, request.ReviewerEmail, ApprovalOutcome.Rejected, request.Notes);
            AuditDecision(payment, reviewer, request.ReviewerEmail, approved: false);
        }
        else
        {
            if (payment.Status != PaymentStatus.PendingApproval)
                return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition",
                    $"Cannot approve a payment in status {payment.Status}."));

            if (payment.Beneficiary is { Status: not BeneficiaryStatus.Approved })
                return Result.Failure<PaymentDto>(Error.Conflict("payment.beneficiaryNotApproved",
                    "The beneficiary must be approved before this payment can be approved."));

            // No approver may count twice toward dual control.
            var priorApprovers = await db.ApprovalDecisions
                .Where(d => d.SubjectType == ApprovalSubjectType.Payment
                            && d.SubjectId == payment.Id
                            && d.Decision == ApprovalOutcome.Approved)
                .Select(d => d.ApproverUserId)
                .ToListAsync(cancellationToken);

            if (priorApprovers.Contains(reviewer, StringComparer.Ordinal))
                return Result.Failure<PaymentDto>(Error.Conflict("payment.alreadyApprovedByUser",
                    "You have already approved this payment."));

            RecordDecision(payment.Id, reviewer, request.ReviewerEmail, ApprovalOutcome.Approved, request.Notes);
            AuditDecision(payment, reviewer, request.ReviewerEmail, approved: true);

            var distinctApprovals = priorApprovers.Distinct(StringComparer.Ordinal).Count() + 1;
            var required = Math.Max(payment.RequiredApprovals, 1);

            if (distinctApprovals >= required)
            {
                // Final approval: re-check funds, then reserve and flip to Approved.
                var account = payment.SourceAccount!;
                var fundsError = await PaymentGuards.CheckDebitableAsync(
                    db, account, payment, clock.UtcNow, cancellationToken);
                if (fundsError is not null)
                    return Result.Failure<PaymentDto>(fundsError);

                try
                {
                    account.Reserve(payment.Amount);
                    account.Touch(clock.UtcNow);
                    payment.Approve(reviewer, request.Notes, clock.UtcNow);
                }
                catch (InvalidOperationException ex)
                {
                    return Result.Failure<PaymentDto>(Error.Conflict("payment.invalidTransition", ex.Message));
                }
            }
            else
            {
                // Partial approval: stays PendingApproval until a distinct approver finalizes.
                payment.Touch(clock.UtcNow);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PaymentDto>(Error.Conflict("payment.concurrencyConflict",
                "The payment or account was modified by someone else. Reload and try again."));
        }

        return Result.Success(payment.ToDto());
    }

    private void RecordDecision(
        Guid paymentId, string approverUserId, string? approverEmail,
        ApprovalOutcome outcome, string? notes)
        => db.ApprovalDecisions.Add(new ApprovalDecision
        {
            SubjectType = ApprovalSubjectType.Payment,
            SubjectId = paymentId,
            ApproverUserId = approverUserId,
            ApproverEmail = approverEmail,
            Decision = outcome,
            Notes = notes,
            DecidedAtUtc = clock.UtcNow
        });

    private void AuditDecision(Payment payment, string approverUserId, string? approverEmail, bool approved)
        => db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = Guid.TryParse(approverUserId, out var id) ? id : null,
            Email = approverEmail,
            EventType = approved ? SecurityEventTypes.PaymentApproved : SecurityEventTypes.PaymentRejected,
            Succeeded = true,
            Details = $"{payment.PaymentReference} {(approved ? "approved" : "rejected")}.",
            OccurredAtUtc = clock.UtcNow
        });
}

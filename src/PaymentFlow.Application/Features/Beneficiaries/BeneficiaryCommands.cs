using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Constants;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Beneficiaries;

// ---------- Create ----------
public record CreateBeneficiaryCommand(
    Guid CustomerId, string Name, string AccountNumber, string? BankName,
    string? BankIdentifierCode, string Currency, string? CountryCode,
    string? CreatedByUserId)
    : IRequest<Result<BeneficiaryDto>>;

public sealed class CreateBeneficiaryCommandValidator : AbstractValidator<CreateBeneficiaryCommand>
{
    public CreateBeneficiaryCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(x => x.BankIdentifierCode).MaximumLength(11);
        RuleFor(x => x.Currency).NotEmpty().Length(3)
            .Must(Currencies.IsSupported).WithMessage("Unsupported currency code.");
        RuleFor(x => x.CountryCode).Length(2).When(x => !string.IsNullOrWhiteSpace(x.CountryCode));
    }
}

public sealed class CreateBeneficiaryCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<CreateBeneficiaryCommand, Result<BeneficiaryDto>>
{
    public async Task<Result<BeneficiaryDto>> Handle(
        CreateBeneficiaryCommand request, CancellationToken cancellationToken)
    {
        var customerExists = await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            return Result.Failure<BeneficiaryDto>(Error.NotFound("customer.notFound", "Customer not found."));

        var duplicate = await db.Beneficiaries.AnyAsync(
            b => b.CustomerId == request.CustomerId && b.AccountNumber == request.AccountNumber.Trim(),
            cancellationToken);
        if (duplicate)
            return Result.Failure<BeneficiaryDto>(
                Error.Conflict("beneficiary.duplicate", "This beneficiary account already exists for the customer."));

        var beneficiary = new Beneficiary
        {
            CustomerId = request.CustomerId,
            Name = request.Name.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            BankName = request.BankName?.Trim(),
            BankIdentifierCode = request.BankIdentifierCode?.Trim().ToUpperInvariant(),
            Currency = request.Currency.ToUpperInvariant(),
            CountryCode = request.CountryCode?.ToUpperInvariant(),
            CreatedByUserId = string.IsNullOrWhiteSpace(request.CreatedByUserId) ? null : request.CreatedByUserId,
            CreatedAtUtc = clock.UtcNow
        };

        db.Beneficiaries.Add(beneficiary);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(beneficiary.ToDto());
    }
}

// ---------- Update ----------
public record UpdateBeneficiaryCommand(
    Guid BeneficiaryId, string Name, string AccountNumber, string? BankName,
    string? BankIdentifierCode, string Currency, string? CountryCode, string RowVersion)
    : IRequest<Result<BeneficiaryDto>>;

public sealed class UpdateBeneficiaryCommandValidator : AbstractValidator<UpdateBeneficiaryCommand>
{
    public UpdateBeneficiaryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(34);
        RuleFor(x => x.Currency).NotEmpty().Length(3).Must(Currencies.IsSupported);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class UpdateBeneficiaryCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<UpdateBeneficiaryCommand, Result<BeneficiaryDto>>
{
    public async Task<Result<BeneficiaryDto>> Handle(
        UpdateBeneficiaryCommand request, CancellationToken cancellationToken)
    {
        var beneficiary = await db.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == request.BeneficiaryId, cancellationToken);

        if (beneficiary is null)
            return Result.Failure<BeneficiaryDto>(Error.NotFound("beneficiary.notFound", "Beneficiary not found."));

        if (!beneficiary.CanEdit)
            return Result.Failure<BeneficiaryDto>(
                Error.Conflict("beneficiary.notEditable",
                    $"A beneficiary in status {beneficiary.Status} cannot be edited."));

        db.Beneficiaries.Entry(beneficiary).Property(b => b.RowVersion).OriginalValue =
            Convert.FromBase64String(request.RowVersion);

        beneficiary.Name = request.Name.Trim();
        beneficiary.AccountNumber = request.AccountNumber.Trim();
        beneficiary.BankName = request.BankName?.Trim();
        beneficiary.BankIdentifierCode = request.BankIdentifierCode?.Trim().ToUpperInvariant();
        beneficiary.Currency = request.Currency.ToUpperInvariant();
        beneficiary.CountryCode = request.CountryCode?.ToUpperInvariant();
        beneficiary.Touch(clock.UtcNow);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<BeneficiaryDto>(
                Error.Conflict("beneficiary.concurrencyConflict",
                    "The beneficiary was modified by someone else. Reload and try again."));
        }

        return Result.Success(beneficiary.ToDto());
    }
}

// ---------- Lifecycle transitions ----------
public enum BeneficiaryTransition { Submit, Approve, Reject }

public record TransitionBeneficiaryCommand(
    Guid BeneficiaryId, BeneficiaryTransition Transition,
    string? ReviewerUserId, string? ReviewerEmail, string? Notes)
    : IRequest<Result<BeneficiaryDto>>;

public sealed class TransitionBeneficiaryCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<TransitionBeneficiaryCommand, Result<BeneficiaryDto>>
{
    public async Task<Result<BeneficiaryDto>> Handle(
        TransitionBeneficiaryCommand request, CancellationToken cancellationToken)
    {
        var beneficiary = await db.Beneficiaries
            .FirstOrDefaultAsync(b => b.Id == request.BeneficiaryId, cancellationToken);

        if (beneficiary is null)
            return Result.Failure<BeneficiaryDto>(Error.NotFound("beneficiary.notFound", "Beneficiary not found."));

        var isReview = request.Transition is BeneficiaryTransition.Approve or BeneficiaryTransition.Reject;

        // Separation of duties on approve/reject: the maker can never be the checker.
        if (isReview)
        {
            if (string.IsNullOrWhiteSpace(request.ReviewerUserId))
                return Result.Failure<BeneficiaryDto>(Error.Forbidden("beneficiary.unknownReviewer",
                    "The approver identity could not be determined."));

            if (!string.IsNullOrEmpty(beneficiary.CreatedByUserId)
                && string.Equals(beneficiary.CreatedByUserId, request.ReviewerUserId, StringComparison.Ordinal))
                return Result.Failure<BeneficiaryDto>(Error.Forbidden("beneficiary.selfApprovalNotAllowed",
                    "You cannot approve or reject a beneficiary you created (separation of duties)."));
        }

        try
        {
            switch (request.Transition)
            {
                case BeneficiaryTransition.Submit:
                    beneficiary.SubmitForApproval(clock.UtcNow);
                    break;
                case BeneficiaryTransition.Approve:
                    beneficiary.Approve(request.ReviewerUserId!, request.Notes, clock.UtcNow);
                    break;
                case BeneficiaryTransition.Reject:
                    beneficiary.Reject(request.ReviewerUserId!, request.Notes, clock.UtcNow);
                    break;
            }
        }
        catch (InvalidOperationException ex)
        {
            // Domain guards rejected the transition -> 409, not a 500.
            return Result.Failure<BeneficiaryDto>(Error.Conflict("beneficiary.invalidTransition", ex.Message));
        }

        // Record the decision + a security audit event for approve/reject.
        if (isReview)
        {
            var approved = request.Transition == BeneficiaryTransition.Approve;
            db.ApprovalDecisions.Add(new ApprovalDecision
            {
                SubjectType = ApprovalSubjectType.Beneficiary,
                SubjectId = beneficiary.Id,
                ApproverUserId = request.ReviewerUserId!,
                ApproverEmail = request.ReviewerEmail,
                Decision = approved ? ApprovalOutcome.Approved : ApprovalOutcome.Rejected,
                Notes = request.Notes,
                DecidedAtUtc = clock.UtcNow
            });
            db.SecurityAuditEvents.Add(new SecurityAuditEvent
            {
                UserId = Guid.TryParse(request.ReviewerUserId, out var uid) ? uid : null,
                Email = request.ReviewerEmail,
                EventType = approved ? SecurityEventTypes.BeneficiaryApproved : SecurityEventTypes.BeneficiaryRejected,
                Succeeded = true,
                Details = $"Beneficiary {beneficiary.Name} {(approved ? "approved" : "rejected")}.",
                OccurredAtUtc = clock.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(beneficiary.ToDto());
    }
}

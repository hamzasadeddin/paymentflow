using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Admin;

// ---------- Shared upsert writer ----------

/// <summary>
/// Upserts one rule section's override and audits the change. Creating a first
/// override needs no row version; a subsequent edit passes the row version so a
/// concurrent change surfaces as 409 (mirroring the rest of the API).
/// </summary>
internal static class RuleSettingWriter
{
    public static async Task<Result<RuleSetDto<T>>> UpsertAsync<T>(
        IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser,
        string section, T values, string? rowVersion, CancellationToken cancellationToken) where T : class
    {
        var json = JsonSerializer.Serialize(values, RuleSettingsJson.Options);

        var existing = await db.RuleSettings
            .FirstOrDefaultAsync(r => r.Section == section, cancellationToken);

        if (existing is null)
        {
            existing = new RuleSetting { Section = section };
            existing.Apply(json, currentUser.UserId?.ToString(), clock.UtcNow);
            db.RuleSettings.Add(existing);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(rowVersion))
                db.RuleSettings.Entry(existing).Property(r => r.RowVersion).OriginalValue =
                    Convert.FromBase64String(rowVersion);
            existing.Apply(json, currentUser.UserId?.ToString(), clock.UtcNow);
        }

        db.SecurityAuditEvents.Add(new SecurityAuditEvent
        {
            UserId = currentUser.UserId,
            Email = currentUser.Email,
            EventType = SecurityEventTypes.RuleSetUpdated,
            Succeeded = true,
            Details = $"{section} rules updated.",
            OccurredAtUtc = clock.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<RuleSetDto<T>>(Error.Conflict("rules.concurrencyConflict",
                "This rule section was modified by someone else. Reload and try again."));
        }

        return Result.Success(new RuleSetDto<T>(values, IsOverridden: true,
            existing.UpdatedByUserId, existing.UpdatedAtUtc, Convert.ToBase64String(existing.RowVersion)));
    }
}

// ---------- Approval ----------

public record UpdateApprovalRulesCommand(
    decimal AutoApproveBelow, decimal DualApprovalAtOrAbove, string? RowVersion)
    : IRequest<Result<RuleSetDto<ApprovalPolicyOptions>>>;

public sealed class UpdateApprovalRulesCommandValidator : AbstractValidator<UpdateApprovalRulesCommand>
{
    public UpdateApprovalRulesCommandValidator()
    {
        RuleFor(x => x.AutoApproveBelow).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DualApprovalAtOrAbove)
            .GreaterThanOrEqualTo(x => x.AutoApproveBelow)
            .WithMessage("Dual-approval threshold must be at or above the auto-approve threshold.");
    }
}

public sealed class UpdateApprovalRulesCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    : IRequestHandler<UpdateApprovalRulesCommand, Result<RuleSetDto<ApprovalPolicyOptions>>>
{
    public Task<Result<RuleSetDto<ApprovalPolicyOptions>>> Handle(
        UpdateApprovalRulesCommand request, CancellationToken cancellationToken)
    {
        var values = new ApprovalPolicyOptions
        {
            AutoApproveBelow = request.AutoApproveBelow,
            DualApprovalAtOrAbove = request.DualApprovalAtOrAbove
        };
        return RuleSettingWriter.UpsertAsync(db, clock, currentUser,
            ApprovalPolicyOptions.SectionName, values, request.RowVersion, cancellationToken);
    }
}

// ---------- Screening (Compliance) ----------

public record UpdateScreeningRulesCommand(
    bool AutoScreenOnSubmit, string[] WatchlistBeneficiaryNames, string[] WatchlistCountryCodes,
    decimal SinglePaymentReviewLimit, string? RowVersion)
    : IRequest<Result<RuleSetDto<ScreeningOptions>>>;

public sealed class UpdateScreeningRulesCommandValidator : AbstractValidator<UpdateScreeningRulesCommand>
{
    public UpdateScreeningRulesCommandValidator()
    {
        RuleFor(x => x.SinglePaymentReviewLimit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WatchlistBeneficiaryNames).NotNull();
        RuleFor(x => x.WatchlistCountryCodes).NotNull();
        RuleForEach(x => x.WatchlistBeneficiaryNames).NotEmpty().MaximumLength(200);
        RuleForEach(x => x.WatchlistCountryCodes).Length(2)
            .WithMessage("Country codes must be 2-letter ISO-3166 alpha-2 codes.");
    }
}

public sealed class UpdateScreeningRulesCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    : IRequestHandler<UpdateScreeningRulesCommand, Result<RuleSetDto<ScreeningOptions>>>
{
    public Task<Result<RuleSetDto<ScreeningOptions>>> Handle(
        UpdateScreeningRulesCommand request, CancellationToken cancellationToken)
    {
        var values = new ScreeningOptions
        {
            AutoScreenOnSubmit = request.AutoScreenOnSubmit,
            WatchlistBeneficiaryNames = request.WatchlistBeneficiaryNames
                .Select(n => n.Trim()).Where(n => n.Length > 0).ToArray(),
            WatchlistCountryCodes = request.WatchlistCountryCodes
                .Select(c => c.Trim().ToUpperInvariant()).Where(c => c.Length > 0).ToArray(),
            SinglePaymentReviewLimit = request.SinglePaymentReviewLimit
        };
        return RuleSettingWriter.UpsertAsync(db, clock, currentUser,
            ScreeningOptions.SectionName, values, request.RowVersion, cancellationToken);
    }
}

// ---------- Reconciliation ----------

public record UpdateReconciliationRulesCommand(
    bool IntroduceSyntheticBreaks, string DropReferenceEndingIn, decimal PhantomAmount,
    int AmountDriftMinorUnits, string? RowVersion)
    : IRequest<Result<RuleSetDto<ReconciliationOptions>>>;

public sealed class UpdateReconciliationRulesCommandValidator : AbstractValidator<UpdateReconciliationRulesCommand>
{
    public UpdateReconciliationRulesCommandValidator()
    {
        RuleFor(x => x.PhantomAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AmountDriftMinorUnits).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DropReferenceEndingIn)
            .Must(s => string.IsNullOrEmpty(s) || (s.Length == 1 && char.IsDigit(s[0])))
            .WithMessage("Drop-reference digit must be a single digit or empty.");
    }
}

public sealed class UpdateReconciliationRulesCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    : IRequestHandler<UpdateReconciliationRulesCommand, Result<RuleSetDto<ReconciliationOptions>>>
{
    public Task<Result<RuleSetDto<ReconciliationOptions>>> Handle(
        UpdateReconciliationRulesCommand request, CancellationToken cancellationToken)
    {
        var values = new ReconciliationOptions
        {
            IntroduceSyntheticBreaks = request.IntroduceSyntheticBreaks,
            DropReferenceEndingIn = request.DropReferenceEndingIn ?? string.Empty,
            PhantomAmount = request.PhantomAmount,
            AmountDriftMinorUnits = request.AmountDriftMinorUnits
        };
        return RuleSettingWriter.UpsertAsync(db, clock, currentUser,
            ReconciliationOptions.SectionName, values, request.RowVersion, cancellationToken);
    }
}

// ---------- Processing ----------

public record UpdateProcessingRulesCommand(
    bool AutoProcessEnabled, int PollingIntervalSeconds, int BatchSize,
    int SimulatedLatencyMsMin, int SimulatedLatencyMsMax, int FailOnCents, string? RowVersion)
    : IRequest<Result<RuleSetDto<ProcessingOptions>>>;

public sealed class UpdateProcessingRulesCommandValidator : AbstractValidator<UpdateProcessingRulesCommand>
{
    public UpdateProcessingRulesCommandValidator()
    {
        RuleFor(x => x.PollingIntervalSeconds).GreaterThanOrEqualTo(1);
        RuleFor(x => x.BatchSize).GreaterThanOrEqualTo(1);
        RuleFor(x => x.SimulatedLatencyMsMin).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SimulatedLatencyMsMax).GreaterThanOrEqualTo(x => x.SimulatedLatencyMsMin)
            .WithMessage("Maximum latency must be at or above the minimum.");
        RuleFor(x => x.FailOnCents).InclusiveBetween(0, 99);
    }
}

public sealed class UpdateProcessingRulesCommandHandler(
    IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService currentUser)
    : IRequestHandler<UpdateProcessingRulesCommand, Result<RuleSetDto<ProcessingOptions>>>
{
    public Task<Result<RuleSetDto<ProcessingOptions>>> Handle(
        UpdateProcessingRulesCommand request, CancellationToken cancellationToken)
    {
        var values = new ProcessingOptions
        {
            AutoProcessEnabled = request.AutoProcessEnabled,
            PollingIntervalSeconds = request.PollingIntervalSeconds,
            BatchSize = request.BatchSize,
            SimulatedLatencyMsMin = request.SimulatedLatencyMsMin,
            SimulatedLatencyMsMax = request.SimulatedLatencyMsMax,
            FailOnCents = request.FailOnCents
        };
        return RuleSettingWriter.UpsertAsync(db, clock, currentUser,
            ProcessingOptions.SectionName, values, request.RowVersion, cancellationToken);
    }
}

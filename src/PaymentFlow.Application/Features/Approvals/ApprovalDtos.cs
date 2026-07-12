using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Approvals;

/// <summary>A single item awaiting approval, for the unified Approvals queue.</summary>
public record ApprovalQueueItemDto(
    ApprovalSubjectType SubjectType,
    Guid SubjectId,
    string Reference,
    string Title,
    decimal? Amount,
    string Currency,
    string? MakerUserId,
    string? MakerEmail,
    int RequiredApprovals,
    int ApprovalsReceived,
    DateTime CreatedAtUtc);

/// <summary>The Approvals queue: pending payments and pending beneficiaries.</summary>
public record ApprovalQueueDto(
    IReadOnlyList<ApprovalQueueItemDto> Payments,
    IReadOnlyList<ApprovalQueueItemDto> Beneficiaries);

/// <summary>A recorded approve/reject decision, for a subject's decision history.</summary>
public record ApprovalDecisionDto(
    Guid Id,
    string ApproverUserId,
    string? ApproverEmail,
    ApprovalOutcome Decision,
    string? Notes,
    DateTime DecidedAtUtc);

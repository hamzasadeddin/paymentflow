using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Payments;

public record PaymentStatusCountDto(PaymentStatus Status, int Count);

public record PaymentStatusSummaryDto(int Total, IReadOnlyList<PaymentStatusCountDto> Counts);

/// <summary>
/// Counts payments by status for the dashboard overview. Returns every status
/// (zero-filled) so the card can render a stable, complete picture.
/// </summary>
public record GetPaymentStatusSummaryQuery : IRequest<Result<PaymentStatusSummaryDto>>;

public sealed class GetPaymentStatusSummaryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPaymentStatusSummaryQuery, Result<PaymentStatusSummaryDto>>
{
    public async Task<Result<PaymentStatusSummaryDto>> Handle(
        GetPaymentStatusSummaryQuery request, CancellationToken cancellationToken)
    {
        var grouped = await db.Payments.AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = Enum.GetValues<PaymentStatus>()
            .Select(status => new PaymentStatusCountDto(
                status, grouped.FirstOrDefault(g => g.Status == status)?.Count ?? 0))
            .ToList();

        return Result.Success(new PaymentStatusSummaryDto(counts.Sum(c => c.Count), counts));
    }
}

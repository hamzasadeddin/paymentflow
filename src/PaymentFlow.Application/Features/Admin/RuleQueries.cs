using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Application.Features.Admin;

/// <summary>Returns every config-backed rule section resolved to its effective values.</summary>
public record GetRulesQuery : IRequest<Result<RulesDto>>;

public sealed class GetRulesQueryHandler(
    IApplicationDbContext db,
    IOptions<ApprovalPolicyOptions> approval,
    IOptions<ScreeningOptions> screening,
    IOptions<ReconciliationOptions> reconciliation,
    IOptions<ProcessingOptions> processing)
    : IRequestHandler<GetRulesQuery, Result<RulesDto>>
{
    public async Task<Result<RulesDto>> Handle(GetRulesQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.RuleSettings.AsNoTracking().ToListAsync(cancellationToken);

        var dto = new RulesDto(
            Resolve(rows, ApprovalPolicyOptions.SectionName, approval.Value),
            Resolve(rows, ScreeningOptions.SectionName, screening.Value),
            Resolve(rows, ReconciliationOptions.SectionName, reconciliation.Value),
            Resolve(rows, ProcessingOptions.SectionName, processing.Value));

        return Result.Success(dto);
    }

    private static RuleSetDto<T> Resolve<T>(List<RuleSetting> rows, string section, T fallback) where T : class
    {
        var row = rows.FirstOrDefault(r => r.Section == section);
        if (row is null)
            return new RuleSetDto<T>(fallback, IsOverridden: false, null, null, null);

        T values;
        try
        {
            values = JsonSerializer.Deserialize<T>(row.ValueJson, RuleSettingsJson.Options) ?? fallback;
        }
        catch (JsonException)
        {
            // A malformed stored value should never break the admin screen; fall back.
            values = fallback;
        }

        return new RuleSetDto<T>(values, IsOverridden: true,
            row.UpdatedByUserId, row.UpdatedAtUtc, Convert.ToBase64String(row.RowVersion));
    }
}

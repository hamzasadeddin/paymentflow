namespace PaymentFlow.Application.Common.Paging;

/// <summary>Shared query contract for all list endpoints.</summary>
public record PagedRequest
{
    private const int MaxPageSize = 100;
    private int _pageSize = 20;

    public int Page { get; init; } = 1;

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value is < 1 or > MaxPageSize ? 20 : value;
    }

    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; }

    public int Skip => (Math.Max(Page, 1) - 1) * PageSize;
}

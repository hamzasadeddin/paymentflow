namespace PaymentFlow.Application.Abstractions;

/// <summary>
/// Resolves user ids to display emails for read models (e.g. showing who raised
/// an item in the approvals queue). Kept as an abstraction so the Application
/// layer stays free of the Identity dependency.
/// </summary>
public interface IUserLookupService
{
    Task<IReadOnlyDictionary<string, string>> GetEmailsByIdsAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken);
}

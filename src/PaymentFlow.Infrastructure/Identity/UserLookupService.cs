using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;

namespace PaymentFlow.Infrastructure.Identity;

public sealed class UserLookupService(UserManager<ApplicationUser> userManager) : IUserLookupService
{
    public async Task<IReadOnlyDictionary<string, string>> GetEmailsByIdsAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        // Maker ids are stored as the string form of the user's Guid key.
        var guids = userIds
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .Distinct()
            .ToList();

        if (guids.Count == 0)
            return new Dictionary<string, string>();

        var users = await userManager.Users
            .Where(u => guids.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        return users
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .ToDictionary(u => u.Id.ToString(), u => u.Email!);
    }
}
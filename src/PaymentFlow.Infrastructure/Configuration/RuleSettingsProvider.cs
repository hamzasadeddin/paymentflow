using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;

namespace PaymentFlow.Infrastructure.Configuration;

/// <summary>
/// Store-backed <see cref="IRuleSettingsProvider"/>. Reads the single override row
/// for a section (if any) and deserializes it into the section's options type;
/// otherwise returns the caller's <c>appsettings</c> fallback. A missing or
/// malformed stored value never throws — it falls back — so the engines keep
/// working even if the store is empty or a bad value slipped in.
///
/// Scoped, so it shares the request's <see cref="IApplicationDbContext"/>. The
/// lookup is a single indexed read against a tiny table; no caching layer is used
/// (a memory cache with write invalidation would be an easy later optimization).
/// </summary>
public sealed class RuleSettingsProvider(IApplicationDbContext db) : IRuleSettingsProvider
{
    public TOptions GetEffective<TOptions>(string section, TOptions configFallback) where TOptions : class
    {
        var row = db.RuleSettings.AsNoTracking()
            .FirstOrDefault(r => r.Section == section);

        if (row is null || string.IsNullOrWhiteSpace(row.ValueJson))
            return configFallback;

        try
        {
            return JsonSerializer.Deserialize<TOptions>(row.ValueJson, RuleSettingsJson.Options)
                ?? configFallback;
        }
        catch (JsonException)
        {
            return configFallback;
        }
    }
}

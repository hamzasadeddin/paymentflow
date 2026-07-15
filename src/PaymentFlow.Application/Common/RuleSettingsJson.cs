using System.Text.Json;

namespace PaymentFlow.Application.Common;

/// <summary>
/// The single JSON contract for serializing rule-section option objects into the
/// rule-settings store and reading them back out. Shared by the admin write side
/// (Application) and the effective-options provider (Infrastructure) so a stored
/// override always round-trips back into the same options type.
/// </summary>
public static class RuleSettingsJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

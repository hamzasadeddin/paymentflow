using PaymentFlow.Domain.Common;

namespace PaymentFlow.Domain.Entities;

/// <summary>
/// An admin-editable override for one config-backed rule section (e.g.
/// <c>ApprovalPolicy</c>, <c>Compliance</c>). The value is the serialized options
/// object for that section; when no <see cref="RuleSetting"/> row exists for a
/// section the engines fall back to the <c>appsettings</c>-bound defaults, so a
/// fresh database behaves exactly as before any admin edit.
///
/// It is an <see cref="AuditableEntity"/>, so editing a section is an
/// optimistic-concurrency operation (two admins racing on the same section →
/// 409), consistent with the rest of the platform. One row per section is
/// enforced by a unique index on <see cref="Section"/>.
/// </summary>
public class RuleSetting : AuditableEntity
{
    /// <summary>The configuration section name this override targets (unique), e.g. <c>ApprovalPolicy</c>.</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>The serialized options object for the section (JSON).</summary>
    public string ValueJson { get; set; } = string.Empty;

    /// <summary>Who last changed this override (a user id string), or <c>null</c> if unknown.</summary>
    public string? UpdatedByUserId { get; private set; }

    /// <summary>Replace the stored value and stamp the editor/time.</summary>
    public void Apply(string valueJson, string? updatedByUserId, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
            throw new InvalidOperationException("Rule setting value cannot be empty.");

        ValueJson = valueJson;
        UpdatedByUserId = updatedByUserId;
        Touch(utcNow);
    }
}

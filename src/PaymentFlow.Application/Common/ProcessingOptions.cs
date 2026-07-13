namespace PaymentFlow.Application.Common;

/// <summary>
/// Settlement-processing settings, bound from the <c>Processing</c> configuration
/// section. Controls the background worker cadence, the simulated settlement
/// latency, and the deterministic failure sentinel. Phase 05 keeps these in
/// configuration; a later phase could move them into an admin-editable store.
/// </summary>
public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    /// <summary>Master switch for the background worker. The manual endpoint is unaffected.</summary>
    public bool AutoProcessEnabled { get; init; } = true;

    /// <summary>Delay between worker ticks, in seconds.</summary>
    public int PollingIntervalSeconds { get; init; } = 5;

    /// <summary>Maximum number of Approved payments claimed per worker tick.</summary>
    public int BatchSize { get; init; } = 10;

    /// <summary>Lower bound of the simulated per-payment settlement delay, in milliseconds.</summary>
    public int SimulatedLatencyMsMin { get; init; } = 750;

    /// <summary>Upper bound of the simulated per-payment settlement delay, in milliseconds. 0 disables the delay.</summary>
    public int SimulatedLatencyMsMax { get; init; } = 2500;

    /// <summary>
    /// A payment fails settlement iff the cents component of its amount equals
    /// this value (default 13). Deterministic and reproducible for demos.
    /// </summary>
    public int FailOnCents { get; init; } = 13;

    /// <summary>The built-in defaults.</summary>
    public static ProcessingOptions Defaults => new();
}

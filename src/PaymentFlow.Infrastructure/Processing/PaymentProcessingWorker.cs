using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentFlow.Application.Abstractions;
using PaymentFlow.Application.Common;
using PaymentFlow.Application.Features.Payments;
using PaymentFlow.Domain.Entities;

namespace PaymentFlow.Infrastructure.Processing;

/// <summary>
/// Continuously drains <see cref="PaymentStatus.Approved"/> payments by handing
/// each to <see cref="ProcessPaymentCommand"/> — the same settlement routine the
/// manual endpoint uses. Runs as a singleton hosted service, so each tick opens
/// its own scope for the scoped <c>DbContext</c> and MediatR sender. Enable/disable
/// and cadence come from <see cref="ProcessingOptions"/>.
/// </summary>
public sealed class PaymentProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessingOptions> options,
    ILogger<PaymentProcessingWorker> logger) : BackgroundService
{
    private readonly ProcessingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.AutoProcessEnabled)
        {
            logger.LogInformation("Automatic payment processing is disabled; worker will not poll.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        logger.LogInformation(
            "Payment processing worker started (interval {Interval}s, batch {Batch}).",
            interval.TotalSeconds, _options.BatchSize);

        do
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A tick failure must never kill the worker; log and wait for the next tick.
                logger.LogError(ex, "Payment processing tick failed; will retry next interval.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var batchSize = Math.Max(1, _options.BatchSize);

        // Skip payments held for compliance (open or rejected case): the manual
        // ProcessPaymentCommand would reject them anyway, so don't churn on them.
        var blockedPaymentIds = db.ComplianceCases
            .Where(c => c.Status == ComplianceCaseStatus.Open || c.Status == ComplianceCaseStatus.Rejected)
            .Select(c => c.PaymentId);

        var approvedIds = await db.Payments
            .Where(p => p.Status == PaymentStatus.Approved && !blockedPaymentIds.Contains(p.Id))
            .OrderBy(p => p.CreatedAtUtc)
            .Select(p => p.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var id in approvedIds)
        {
            var result = await sender.Send(new ProcessPaymentCommand(id), cancellationToken);
            if (result.IsFailure)
            {
                // Losing the claim race (already processing) is expected and benign.
                logger.LogDebug("Payment {PaymentId} not settled this tick: {Code}",
                    id, result.Error!.Code);
            }
        }
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

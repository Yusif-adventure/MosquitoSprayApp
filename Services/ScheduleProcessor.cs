using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Services;

/// <summary>Turns due schedules into spray commands. Kept separate from the hosted service so it is testable.</summary>
public class ScheduleProcessor
{
    /// <summary>A schedule more than this late is marked Missed instead of firing at a surprising time.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan NotifyMissedWithin = TimeSpan.FromDays(1);

    private readonly ApplicationDbContext _db;
    private readonly DeviceCommandService _commands;
    private readonly ILogger<ScheduleProcessor> _logger;

    public ScheduleProcessor(ApplicationDbContext db, DeviceCommandService commands, ILogger<ScheduleProcessor> logger)
    {
        _db = db;
        _commands = commands;
        _logger = logger;
    }

    /// <returns>How many schedules reached a final state (Executed or Missed).</returns>
    public async Task<int> ProcessDueAsync(DateTime utcNow, CancellationToken ct = default)
    {
        var due = await _db.Schedules
            .Where(s => s.IsEnabled && s.Status == ScheduleStatus.Pending && s.ScheduledFor <= utcNow)
            .OrderBy(s => s.ScheduledFor)
            .Take(100)
            .ToListAsync(ct);

        var finished = 0;
        foreach (var schedule in due)
        {
            if (utcNow - schedule.ScheduledFor > GracePeriod)
            {
                MarkMissed(schedule, utcNow, "it could not run within 15 minutes of its scheduled time");
                finished++;
                continue;
            }

            var result = await _commands.StageSprayAsync(schedule.UserId, "Schedule", ct);
            switch (result.Outcome)
            {
                case QueueSprayOutcome.Queued:
                    schedule.Status = ScheduleStatus.Executed;
                    schedule.ExecutedAt = utcNow;
                    finished++;
                    break;

                case QueueSprayOutcome.NoDevice:
                    MarkMissed(schedule, utcNow, "no sprayer is linked to your account");
                    finished++;
                    break;

                case QueueSprayOutcome.Busy:
                    // The sprayer is mid-cycle; try again on the next tick (until the grace period runs out).
                    break;
            }
        }

        if (due.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Schedule run: {Due} due, {Finished} finished", due.Count, finished);
        }

        return finished;
    }

    private void MarkMissed(ScheduleItem schedule, DateTime utcNow, string reason)
    {
        schedule.Status = ScheduleStatus.Missed;
        schedule.ExecutedAt = utcNow;

        if (utcNow - schedule.ScheduledFor < NotifyMissedWithin)
        {
            _db.Notifications.Add(new NotificationItem
            {
                UserId = schedule.UserId,
                Title = "Scheduled Spray Missed",
                Message = $"\"{schedule.Name}\" did not run because {reason}.",
                CreatedAt = utcNow,
                Severity = NotificationSeverity.Warning
            });
        }
    }
}

/// <summary>Wakes up every 30 seconds and runs any schedules that have come due.</summary>
public sealed class ScheduleRunnerService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ScheduleRunnerService> _logger;

    public ScheduleRunnerService(IServiceScopeFactory scopes, ILogger<ScheduleRunnerService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ScheduleProcessor>();
            await processor.ProcessDueAsync(DateTime.UtcNow, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schedule run failed; will retry on the next tick");
        }
    }
}

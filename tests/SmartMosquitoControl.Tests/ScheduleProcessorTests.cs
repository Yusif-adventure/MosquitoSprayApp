using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Tests;

public class ScheduleProcessorTests
{
    private static ScheduleItem Schedule(string userId, TimeSpan fromNow, bool enabled = true, string name = "Evening") =>
        new() { UserId = userId, Name = name, ScheduledFor = DateTime.UtcNow + fromNow, IsEnabled = enabled };

    private static async Task<ScheduleItem> ReloadAsync(TestDb t, int id)
    {
        t.ForgetTrackedEntities();
        return await t.Db.Schedules.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    [Fact]
    public async Task DueSchedule_QueuesASpray_AndIsMarkedExecuted()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await t.DataFor("u1").AddLinkedDeviceAsync("SPR-0001", "A");
        var schedule = Schedule("u1", TimeSpan.FromMinutes(-1));
        t.Db.Schedules.Add(schedule);
        await t.Db.SaveChangesAsync();

        var finished = await t.Processor().ProcessDueAsync(DateTime.UtcNow);

        Assert.Equal(1, finished);
        var reloaded = await ReloadAsync(t, schedule.Id);
        Assert.Equal(ScheduleStatus.Executed, reloaded.Status);
        Assert.NotNull(reloaded.ExecutedAt);
        var command = await t.Db.DeviceCommands.SingleAsync();
        Assert.Equal("Schedule", command.Source);
        Assert.Equal(DeviceCommandStatus.Pending, command.Status);
        Assert.Equal("Scheduled spray", (await t.Db.SprayHistory.SingleAsync()).Type);
    }

    [Fact]
    public async Task DueSchedule_WithoutASprayer_IsMissed_AndTheUserIsTold()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var schedule = Schedule("u1", TimeSpan.FromMinutes(-1));
        t.Db.Schedules.Add(schedule);
        await t.Db.SaveChangesAsync();

        await t.Processor().ProcessDueAsync(DateTime.UtcNow);

        Assert.Equal(ScheduleStatus.Missed, (await ReloadAsync(t, schedule.Id)).Status);
        Assert.Contains(await t.Db.Notifications.ToListAsync(), n => n.Title == "Scheduled Spray Missed");
        Assert.Empty(t.Db.DeviceCommands);
    }

    [Fact]
    public async Task ScheduleThatIsTooLate_IsMissed_NotFiredAtASurprisingTime()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await t.DataFor("u1").AddLinkedDeviceAsync("SPR-0001", "A");
        var schedule = Schedule("u1", -(ScheduleProcessor.GracePeriod + TimeSpan.FromMinutes(5)));
        t.Db.Schedules.Add(schedule);
        await t.Db.SaveChangesAsync();

        await t.Processor().ProcessDueAsync(DateTime.UtcNow);

        Assert.Equal(ScheduleStatus.Missed, (await ReloadAsync(t, schedule.Id)).Status);
        Assert.Empty(t.Db.DeviceCommands);
    }

    [Fact]
    public async Task FutureAndDisabledSchedules_AreLeftAlone()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await t.DataFor("u1").AddLinkedDeviceAsync("SPR-0001", "A");
        var future = Schedule("u1", TimeSpan.FromHours(1), name: "future");
        var disabled = Schedule("u1", TimeSpan.FromMinutes(-1), enabled: false, name: "disabled");
        t.Db.Schedules.AddRange(future, disabled);
        await t.Db.SaveChangesAsync();

        var finished = await t.Processor().ProcessDueAsync(DateTime.UtcNow);

        Assert.Equal(0, finished);
        Assert.Equal(ScheduleStatus.Pending, (await ReloadAsync(t, future.Id)).Status);
        Assert.Equal(ScheduleStatus.Pending, (await ReloadAsync(t, disabled.Id)).Status);
        Assert.Empty(t.Db.DeviceCommands);
    }

    [Fact]
    public async Task TwoSchedulesDueTogether_AreSprayedOneAtATime()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await t.DataFor("u1").AddLinkedDeviceAsync("SPR-0001", "A");
        var first = Schedule("u1", TimeSpan.FromMinutes(-2), name: "first");
        var second = Schedule("u1", TimeSpan.FromMinutes(-1), name: "second");
        t.Db.Schedules.AddRange(first, second);
        await t.Db.SaveChangesAsync();

        var finished = await t.Processor(simulate: false).ProcessDueAsync(DateTime.UtcNow);

        Assert.Equal(1, finished);
        Assert.Equal(ScheduleStatus.Executed, (await ReloadAsync(t, first.Id)).Status);
        Assert.Equal(ScheduleStatus.Pending, (await ReloadAsync(t, second.Id)).Status); // retried on a later tick
        Assert.Single(t.Db.DeviceCommands);
    }
}

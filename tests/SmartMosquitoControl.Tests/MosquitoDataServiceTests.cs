using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Tests;

public class MosquitoDataServiceTests
{
    [Fact]
    public async Task FirstLinkedDevice_BecomesPrimary_AndGetsStateAndKey()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");

        var result = await data.AddLinkedDeviceAsync("spr-0001", "Kitchen");

        Assert.Equal(LinkDeviceOutcome.Linked, result.Outcome);
        Assert.NotNull(result.ApiKey);
        Assert.NotEmpty(result.ApiKey!);
        Assert.True(result.Device!.IsCurrent);
        Assert.Equal("SPR-0001", result.Device.DeviceId);
        // Only the hash is stored, never the key itself.
        Assert.NotEqual(result.ApiKey, result.Device.ApiKeyHash);
        Assert.True(ApiKeyHasher.Verify(result.ApiKey, result.Device.ApiKeyHash));

        var state = await data.GetDeviceAsync();
        Assert.NotNull(state);
        Assert.Equal("SPR-0001", state!.DeviceId);
        Assert.False(state.IsOnline); // has never checked in
    }

    [Fact]
    public async Task SecondDevice_IsNotPrimary_UntilSwitched()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");
        await data.AddLinkedDeviceAsync("SPR-0001", "A");
        var second = await data.AddLinkedDeviceAsync("SPR-0002", "B");

        Assert.False(second.Device!.IsCurrent);
        Assert.Equal("SPR-0001", (await data.GetDeviceAsync())!.DeviceId);

        Assert.True(await data.SetCurrentDeviceAsync(second.Device.Id));

        Assert.Equal("SPR-0002", (await data.GetDeviceAsync())!.DeviceId);
        var all = await data.GetLinkedDevicesAsync();
        Assert.Single(all, d => d.IsCurrent);
    }

    [Fact]
    public async Task SameSprayerId_CannotBeLinkedByTwoAccounts()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        t.AddUser("u2");

        var first = await t.DataFor("u1").AddLinkedDeviceAsync("SPR-0001", "Mine");
        var second = await t.DataFor("u2").AddLinkedDeviceAsync("spr-0001", "Stolen?");

        Assert.Equal(LinkDeviceOutcome.Linked, first.Outcome);
        Assert.Equal(LinkDeviceOutcome.AlreadyLinked, second.Outcome);
        Assert.Empty(await t.DataFor("u2").GetLinkedDevicesAsync());
    }

    [Fact]
    public async Task UnlinkingPrimary_PromotesAnotherAndRemovesState()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");
        var first = await data.AddLinkedDeviceAsync("SPR-0001", "A");
        await data.AddLinkedDeviceAsync("SPR-0002", "B");

        Assert.True(await data.RemoveLinkedDeviceAsync(first.Device!.Id));

        t.ForgetTrackedEntities();
        var remaining = await data.GetLinkedDevicesAsync();
        Assert.Single(remaining);
        Assert.True(remaining[0].IsCurrent);
        Assert.False(await t.Db.Devices.AnyAsync(d => d.DeviceId == "SPR-0001"));
    }

    [Fact]
    public async Task Schedules_AreScopedToTheirOwner()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        t.AddUser("u2");
        var owner = t.DataFor("u1");
        var other = t.DataFor("u2");

        await owner.AddScheduleAsync(new ScheduleItem { Name = "Evening", ScheduledFor = DateTime.UtcNow.AddHours(2) });
        var id = (await owner.GetUpcomingSchedulesAsync()).Single().Id;

        Assert.False(await other.RemoveScheduleAsync(id));
        Assert.False(await other.SetScheduleEnabledAsync(id, false));
        Assert.Empty(await other.GetUpcomingSchedulesAsync());
        Assert.Single(await owner.GetUpcomingSchedulesAsync());
    }

    [Fact]
    public async Task UpcomingSchedules_TakeIsAppliedInOrder_AndTimesComeBackAsUtc()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");
        foreach (var hours in new[] { 5, 1, 3, 2 })
        {
            await data.AddScheduleAsync(new ScheduleItem { Name = $"in {hours}h", ScheduledFor = DateTime.UtcNow.AddHours(hours) });
        }

        t.ForgetTrackedEntities();
        var firstTwo = await data.GetUpcomingSchedulesAsync(take: 2);

        Assert.Equal(new[] { "in 1h", "in 2h" }, firstTwo.Select(s => s.Name).ToArray());
        Assert.All(firstTwo, s => Assert.Equal(DateTimeKind.Utc, s.ScheduledFor.Kind));
    }

    [Fact]
    public async Task NextSchedule_IgnoresDisabledOnes()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");
        await data.AddScheduleAsync(new ScheduleItem { Name = "soon but off", ScheduledFor = DateTime.UtcNow.AddHours(1), IsEnabled = false });
        await data.AddScheduleAsync(new ScheduleItem { Name = "later", ScheduledFor = DateTime.UtcNow.AddHours(4) });

        Assert.Equal("later", (await data.GetNextScheduleAsync())!.Name);
    }

    [Fact]
    public async Task MarkAllNotificationsRead_ClearsOnlyTheCurrentUsersUnreadCount()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        t.AddUser("u2");
        t.Db.Notifications.Add(new NotificationItem { UserId = "u1", Title = "a", Message = "a" });
        t.Db.Notifications.Add(new NotificationItem { UserId = "u2", Title = "b", Message = "b" });
        await t.Db.SaveChangesAsync();

        await t.DataFor("u1").MarkAllNotificationsReadAsync();

        Assert.Equal(0, await t.DataFor("u1").CountUnreadNotificationsAsync());
        Assert.Equal(1, await t.DataFor("u2").CountUnreadNotificationsAsync());
    }

    [Fact]
    public async Task Settings_AreClampedToSaneRanges()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var data = t.DataFor("u1");

        await data.UpdateSettingsAsync(sprayDurationSeconds: 9999, lowInsecticideThreshold: 0,
            lowInsecticideAlertsEnabled: false, sprayNotificationsEnabled: true);

        var user = await data.GetCurrentUserAsync();
        Assert.Equal(120, user!.SprayDurationSeconds);
        Assert.Equal(5, user.LowInsecticideThreshold);
        Assert.False(user.LowInsecticideAlertsEnabled);
    }
}

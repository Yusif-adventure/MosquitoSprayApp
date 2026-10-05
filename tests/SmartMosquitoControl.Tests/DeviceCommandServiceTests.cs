using Microsoft.EntityFrameworkCore;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Tests;

public class DeviceCommandServiceTests
{
    private static async Task<string> LinkAsync(TestDb t, string userId, string deviceId)
    {
        var result = await t.DataFor(userId).AddLinkedDeviceAsync(deviceId, "Sprayer");
        return result.ApiKey!;
    }

    [Fact]
    public async Task Queue_WithoutADevice_ReportsNoDevice()
    {
        using var t = new TestDb();
        t.AddUser("u1");

        var result = await t.Commands().QueueSprayAsync("u1", "Manual");

        Assert.Equal(QueueSprayOutcome.NoDevice, result.Outcome);
        Assert.Empty(t.Db.SprayHistory);
    }

    [Fact]
    public async Task Queue_WithRealHardware_QueuesAPendingCommand_AndLeavesTheLevelAlone()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await LinkAsync(t, "u1", "SPR-0001");

        var result = await t.Commands(simulate: false).QueueSprayAsync("u1", "Manual");

        Assert.Equal(QueueSprayOutcome.Queued, result.Outcome);
        Assert.Equal(DeviceCommandStatus.Pending, result.Command!.Status);
        Assert.Equal(30, result.Command.DurationSeconds); // the user's configured duration
        Assert.Single(t.Db.SprayHistory);
        Assert.Equal(100, (await t.Db.Devices.SingleAsync()).InsecticideLevel); // only the device can report usage
    }

    [Fact]
    public async Task Queue_WhileACommandIsInFlight_IsBusy()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await LinkAsync(t, "u1", "SPR-0001");
        var commands = t.Commands();

        Assert.Equal(QueueSprayOutcome.Queued, (await commands.QueueSprayAsync("u1", "Manual")).Outcome);
        Assert.Equal(QueueSprayOutcome.Busy, (await commands.QueueSprayAsync("u1", "Manual")).Outcome);
        Assert.Single(t.Db.DeviceCommands);
    }

    [Fact]
    public async Task Queue_ExpiresStaleCommands_InsteadOfBlockingForever()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await LinkAsync(t, "u1", "SPR-0001");
        t.Db.DeviceCommands.Add(new DeviceCommand
        {
            UserId = "u1",
            DeviceId = "SPR-0001",
            Status = DeviceCommandStatus.Pending,
            CreatedAt = DateTime.UtcNow - DeviceCommandService.CommandTimeout - TimeSpan.FromMinutes(1)
        });
        await t.Db.SaveChangesAsync();

        var result = await t.Commands().QueueSprayAsync("u1", "Manual");

        Assert.Equal(QueueSprayOutcome.Queued, result.Outcome);
        t.ForgetTrackedEntities();
        Assert.Equal(1, await t.Db.DeviceCommands.CountAsync(c => c.Status == DeviceCommandStatus.Expired));
    }

    [Fact]
    public async Task Queue_InSimulationMode_CompletesImmediately_AndWarnsWhenCrossingTheLowThreshold()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        await LinkAsync(t, "u1", "SPR-0001");
        (await t.Db.Devices.SingleAsync()).InsecticideLevel = 24; // threshold defaults to 20; one spray uses 5
        await t.Db.SaveChangesAsync();

        var result = await t.Commands(simulate: true).QueueSprayAsync("u1", "Manual");

        Assert.Equal(DeviceCommandStatus.Completed, result.Command!.Status);
        t.ForgetTrackedEntities();
        var device = await t.Db.Devices.SingleAsync();
        Assert.Equal(19, device.InsecticideLevel);
        Assert.True(device.IsOnline);
        Assert.Contains(await t.Db.Notifications.ToListAsync(),
            n => n.Title == "Low Insecticide Level" && n.Severity == NotificationSeverity.Warning);
    }

    [Fact]
    public async Task DeviceFlow_Authenticate_Claim_Complete()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var key = await LinkAsync(t, "u1", "SPR-0001");
        var commands = t.Commands();
        await commands.QueueSprayAsync("u1", "Manual");

        Assert.Null(await commands.AuthenticateDeviceAsync("SPR-0001", "wrong-key"));
        Assert.Null(await commands.AuthenticateDeviceAsync("SPR-9999", key));
        var device = await commands.AuthenticateDeviceAsync("spr-0001", key);
        Assert.NotNull(device);

        var claimed = await commands.ClaimNextCommandAsync(device!);
        Assert.NotNull(claimed);
        Assert.Equal(DeviceCommandStatus.Sent, claimed!.Status);
        Assert.Null(await commands.ClaimNextCommandAsync(device!)); // already handed out

        Assert.True(await commands.CompleteCommandAsync(device!, claimed.Id, success: true, insecticideLevel: 61));
        Assert.False(await commands.CompleteCommandAsync(device!, claimed.Id, success: true, insecticideLevel: 61)); // not twice

        t.ForgetTrackedEntities();
        var state = await t.Db.Devices.SingleAsync();
        Assert.Equal(61, state.InsecticideLevel);
        Assert.NotNull(state.LastSprayAt);
        Assert.True(state.IsOnline);
        Assert.Equal(DeviceCommandStatus.Completed, (await t.Db.DeviceCommands.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeviceCannotCompleteAnotherDevicesCommand()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        t.AddUser("u2");
        await LinkAsync(t, "u1", "SPR-0001");
        var otherKey = await LinkAsync(t, "u2", "SPR-0002");
        var commands = t.Commands();
        var queued = await commands.QueueSprayAsync("u1", "Manual");
        var victim = await commands.AuthenticateDeviceAsync("SPR-0001", (await t.Db.LinkedDevices.AsNoTracking().FirstAsync(d => d.DeviceId == "SPR-0001")).ApiKeyHash);
        Assert.Null(victim); // the stored hash is not a usable key

        var attacker = await commands.AuthenticateDeviceAsync("SPR-0002", otherKey);
        Assert.False(await commands.CompleteCommandAsync(attacker!, queued.Command!.Id, success: true, insecticideLevel: 0));
    }

    [Fact]
    public async Task FailedSpray_RaisesAWarning()
    {
        using var t = new TestDb();
        t.AddUser("u1");
        var key = await LinkAsync(t, "u1", "SPR-0001");
        var commands = t.Commands();
        await commands.QueueSprayAsync("u1", "Manual");
        var device = (await commands.AuthenticateDeviceAsync("SPR-0001", key))!;
        var claimed = (await commands.ClaimNextCommandAsync(device))!;

        await commands.CompleteCommandAsync(device, claimed.Id, success: false, insecticideLevel: null);

        t.ForgetTrackedEntities();
        Assert.Contains(await t.Db.Notifications.ToListAsync(), n => n.Title == "Spray Failed");
        Assert.Equal(DeviceCommandStatus.Failed, (await t.Db.DeviceCommands.SingleAsync()).Status);
    }

    [Fact]
    public void ApiKeys_AreRandom_AndOnlyTheirOwnHashVerifies()
    {
        var a = ApiKeyHasher.Generate();
        var b = ApiKeyHasher.Generate();

        Assert.NotEqual(a, b);
        Assert.True(ApiKeyHasher.Verify(a, ApiKeyHasher.Hash(a)));
        Assert.False(ApiKeyHasher.Verify(a, ApiKeyHasher.Hash(b)));
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartMosquitoControl.Data;
using SmartMosquitoControl.Models;
using SmartMosquitoControl.Services;

namespace SmartMosquitoControl.Tests;

/// <summary>A throwaway in-memory SQLite database built from the real EF model (so unique indexes etc. are enforced).</summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public ApplicationDbContext Db { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
    }

    public ApplicationUser AddUser(string id)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = $"{id}@example.com",
            NormalizedUserName = $"{id}@example.com".ToUpperInvariant(),
            Email = $"{id}@example.com",
            NormalizedEmail = $"{id}@example.com".ToUpperInvariant()
        };
        Db.Users.Add(user);
        Db.SaveChanges();
        return user;
    }

    public DeviceCommandService Commands(bool simulate = false) =>
        new(Db, Options.Create(new DeviceOptions { SimulateHardware = simulate }), NullLogger<DeviceCommandService>.Instance);

    /// <summary>A data service that behaves as if <paramref name="userId"/> were signed in.</summary>
    public MosquitoDataService DataFor(string userId, bool simulate = false)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "test"))
        };
        return new MosquitoDataService(Db, new HttpContextAccessor { HttpContext = context }, Commands(simulate));
    }

    public ScheduleProcessor Processor(bool simulate = false) =>
        new(Db, Commands(simulate), NullLogger<ScheduleProcessor>.Instance);

    /// <summary>Drops tracked entities so the next query shows what was actually persisted.</summary>
    public void ForgetTrackedEntities() => Db.ChangeTracker.Clear();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartMosquitoControl.Models;

namespace SmartMosquitoControl.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DeviceState> Devices { get; set; } = default!;
    public DbSet<LinkedDevice> LinkedDevices { get; set; } = default!;
    public DbSet<ScheduleItem> Schedules { get; set; } = default!;
    public DbSet<SprayHistoryItem> SprayHistory { get; set; } = default!;
    public DbSet<NotificationItem> Notifications { get; set; } = default!;
    public DbSet<DeviceCommand> DeviceCommands { get; set; } = default!;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // SQLite stores DateTime as text and hands it back with Kind = Unspecified.
        // Everything in this app is UTC, so make that explicit on the way out.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Device state: one row per sprayer; a physical sprayer belongs to exactly one account.
        modelBuilder.Entity<DeviceState>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeviceState>()
            .HasIndex(d => d.DeviceId)
            .IsUnique();

        modelBuilder.Entity<LinkedDevice>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LinkedDevice>()
            .HasIndex(d => d.DeviceId)
            .IsUnique();

        modelBuilder.Entity<ScheduleItem>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SprayHistoryItem>()
            .HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationItem>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeviceCommand>()
            .HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for the main queries
        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(s => new { s.UserId, s.ScheduledFor });

        modelBuilder.Entity<SprayHistoryItem>()
            .HasIndex(h => new { h.UserId, h.Time });

        modelBuilder.Entity<NotificationItem>()
            .HasIndex(n => new { n.UserId, n.CreatedAt });

        modelBuilder.Entity<DeviceCommand>()
            .HasIndex(c => new { c.DeviceId, c.Status });
    }
}

internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

internal sealed class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v.Value.ToUniversalTime())
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}

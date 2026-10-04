using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure DeviceState relationships
        modelBuilder.Entity<DeviceState>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeviceState>()
            .HasIndex(d => new { d.UserId, d.DeviceId })
            .IsUnique();

        // Configure LinkedDevice relationships
        modelBuilder.Entity<LinkedDevice>()
            .HasOne(d => d.User)
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ScheduleItem relationships
        modelBuilder.Entity<ScheduleItem>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure SprayHistoryItem relationships
        modelBuilder.Entity<SprayHistoryItem>()
            .HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure NotificationItem relationships
        modelBuilder.Entity<NotificationItem>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add indexes for performance
        modelBuilder.Entity<ScheduleItem>()
            .HasIndex(s => new { s.UserId, s.ScheduledFor });

        modelBuilder.Entity<SprayHistoryItem>()
            .HasIndex(h => new { h.UserId, h.Time });

        modelBuilder.Entity<NotificationItem>()
            .HasIndex(n => new { n.UserId, n.CreatedAt });
    }
}

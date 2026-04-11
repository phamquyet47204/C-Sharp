using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;

namespace VinhKhanh.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Poi> Pois { get; set; }
    public DbSet<PoiLocalization> PoiLocalizations { get; set; }
    public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<FreeTrialRecord> FreeTrialRecords { get; set; }
<<<<<<< HEAD
=======
    public DbSet<DeviceTrial> DeviceTrials { get; set; }
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Poi>()
            .HasMany(p => p.Localizations)
            .WithOne(l => l.Poi)
            .HasForeignKey(l => l.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        // Poi.OwnerId → ApplicationUser (no cascade, nullable)
        modelBuilder.Entity<Poi>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

<<<<<<< HEAD
=======
        // Payment.Amount precision (tránh truncation warning)
        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
        // Payment.TransactionId unique index
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionId)
            .IsUnique();

        // Payment.UserId → ApplicationUser
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FreeTrialRecord unique indexes (filtered to handle nulls)
        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.UserId, f.PoiId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.DeviceId, f.PoiId })
            .IsUnique()
            .HasFilter("[DeviceId] IS NOT NULL");
<<<<<<< HEAD
=======

        modelBuilder.Entity<DeviceTrial>()
            .HasIndex(d => d.DeviceId)
            .IsUnique();
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    }
}

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
    public DbSet<DeviceTrial> DeviceTrials { get; set; }
    public DbSet<PoiRating> PoiRatings { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Poi>()
            .HasMany(p => p.Localizations)
            .WithOne(l => l.Poi)
            .HasForeignKey(l => l.PoiId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Poi>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionId)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.UserId, f.PoiId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.DeviceId, f.PoiId })
            .IsUnique()
            .HasFilter("[DeviceId] IS NOT NULL");

        modelBuilder.Entity<PoiRating>()
            .HasOne(r => r.Poi)
            .WithMany()
            .HasForeignKey(r => r.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PoiRating>()
            .HasIndex(r => new { r.DeviceId, r.PoiId })
            .IsUnique();

        modelBuilder.Entity<PoiRating>()
            .ToTable(t => t.HasCheckConstraint("CK_PoiRatings_Stars", "[Stars] >= 1 AND [Stars] <= 5"));

        modelBuilder.Entity<SystemSetting>()
            .HasKey(s => s.Key);
    }
}

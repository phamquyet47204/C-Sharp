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
<<<<<<< Updated upstream
=======
    public DbSet<DeviceTrial> DeviceTrials { get; set; }
    public DbSet<PoiRating> PoiRatings { get; set; }
    public DbSet<SystemSetting> SystemSettings { get; set; }
>>>>>>> Stashed changes

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Poi>()
            .HasMany(p => p.Localizations)
            .WithOne(l => l.Poi)
            .HasForeignKey(l => l.PoiId)
            .OnDelete(DeleteBehavior.Cascade);

        // Poi.OwnerId → ApplicationUser (không sử dụng cascade, cho phép null)
        modelBuilder.Entity<Poi>()
            .HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // Cấu hình Unique Index cho Payment.TransactionId
        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.TransactionId)
            .IsUnique();

        // Khóa ngoại Payment.UserId → ApplicationUser
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cấu hình Unique Index cho FreeTrialRecord (lọc để xử lý các giá trị null)
        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.UserId, f.PoiId })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        modelBuilder.Entity<FreeTrialRecord>()
            .HasIndex(f => new { f.DeviceId, f.PoiId })
            .IsUnique()
            .HasFilter("[DeviceId] IS NOT NULL");
<<<<<<< Updated upstream
=======

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
>>>>>>> Stashed changes
    }
}

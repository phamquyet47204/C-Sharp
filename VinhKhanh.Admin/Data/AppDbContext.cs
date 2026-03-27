using Microsoft.EntityFrameworkCore;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Admin.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Poi> Pois => Set<Poi>();
    public DbSet<NarrationEvent> NarrationEvents => Set<NarrationEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Poi>().HasIndex(p => p.UpdatedAt);
        b.Entity<NarrationEvent>().HasIndex(e => e.TriggeredAt);
    }
}

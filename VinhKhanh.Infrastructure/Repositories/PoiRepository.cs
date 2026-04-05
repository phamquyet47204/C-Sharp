using Microsoft.EntityFrameworkCore;
using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Infrastructure.Repositories;

public class PoiRepository(AppDbContext context) : IPoiRepository
{
    public async Task<IEnumerable<Poi>> GetSyncPoisAsync(DateTime lastSyncTimestamp, CancellationToken cancellationToken = default)
    {
        return await context.Pois
            .Include(p => p.Localizations)
            .Where(p => p.UpdatedAt > lastSyncTimestamp && p.Status == PoiStatus.Approved)
            .ToListAsync(cancellationToken);
    }

    public async Task<Poi?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await context.Pois.FindAsync([id], cancellationToken);
    }

    public async Task<bool> ApprovePoiAsync(int id, CancellationToken cancellationToken = default)
    {
        var poi = await GetByIdAsync(id, cancellationToken);
        if (poi == null) return false;

        poi.IsApproved = true;
        poi.UpdatedAt = DateTime.UtcNow;
        return await context.SaveChangesAsync(cancellationToken) > 0;
    }
}

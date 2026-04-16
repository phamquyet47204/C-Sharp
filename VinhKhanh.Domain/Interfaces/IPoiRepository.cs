using VinhKhanh.Domain.Entities;

namespace VinhKhanh.Domain.Interfaces;

public interface IPoiRepository
{
    Task<IEnumerable<Poi>> GetSyncPoisAsync(DateTime lastSyncTimestamp, CancellationToken cancellationToken = default);
    Task<Poi?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> ApprovePoiAsync(int id, CancellationToken cancellationToken = default);
}

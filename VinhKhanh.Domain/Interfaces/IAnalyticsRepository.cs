using VinhKhanh.Domain.Entities;

namespace VinhKhanh.Domain.Interfaces;

public interface IAnalyticsRepository
{
    Task AddVisitEventAsync(AnalyticsEvent evt, CancellationToken cancellationToken = default);
}

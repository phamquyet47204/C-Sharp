using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;
using VinhKhanh.Infrastructure.Data;

namespace VinhKhanh.Infrastructure.Repositories;

public class AnalyticsRepository(AppDbContext context) : IAnalyticsRepository
{
    public async Task AddVisitEventAsync(AnalyticsEvent evt, CancellationToken cancellationToken = default)
    {
        context.AnalyticsEvents.Add(evt);
        await context.SaveChangesAsync(cancellationToken);
    }
}

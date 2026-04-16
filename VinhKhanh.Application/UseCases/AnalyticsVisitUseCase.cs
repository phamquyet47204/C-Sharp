using VinhKhanh.Domain.Entities;
using VinhKhanh.Domain.Interfaces;

namespace VinhKhanh.Application.UseCases;

public class AnalyticsVisitCommand
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int? PoiId { get; set; }
    public string? EventType { get; set; } // "visit" | "narration"
}

public class AnalyticsVisitUseCase(IAnalyticsRepository repository)
{
    public async Task ExecuteAsync(AnalyticsVisitCommand command, CancellationToken cancellationToken = default)
    {
        var evt = new AnalyticsEvent
        {
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            DeviceId = string.IsNullOrWhiteSpace(command.DeviceId) ? "anonymous" : command.DeviceId,
            Timestamp = DateTime.UtcNow,
            PoiId = command.PoiId,
            EventType = command.EventType ?? "visit"
        };
        await repository.AddVisitEventAsync(evt, cancellationToken);
    }
}

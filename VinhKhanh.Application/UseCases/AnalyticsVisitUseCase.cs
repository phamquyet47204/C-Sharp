using System.Security.Cryptography;
using System.Text;
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
        command.DeviceId = BuildAnonymousDeviceId(command.DeviceId);

        var evt = new AnalyticsEvent
        {
            Latitude = command.Latitude,
            Longitude = command.Longitude,
            DeviceId = command.DeviceId,
            Timestamp = DateTime.UtcNow,
            PoiId = command.PoiId,
            EventType = command.EventType ?? "visit"
        };
        await repository.AddVisitEventAsync(evt, cancellationToken);
    }

    private static string BuildAnonymousDeviceId(string? rawDeviceId)
    {
        if (string.IsNullOrWhiteSpace(rawDeviceId))
        {
            return "anonymous";
        }

        var normalized = rawDeviceId.Trim().ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hash = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"anon-{hash[..24]}";
    }
}

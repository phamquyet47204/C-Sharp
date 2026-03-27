using VinhKhanh.Shared;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Services;

/// <summary>
/// Runs as a Foreground Service on Android / Background Task on iOS.
/// Polls GPS, checks geofences, fires narration events.
/// </summary>
public class GeofenceService(LocalDatabase db, NarrationEngine narration)
{
    private CancellationTokenSource? _cts;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => LoopAsync(_cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var location = await Geolocation.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(4)), ct);

                if (location is not null)
                    await CheckGeofencesAsync(location.Latitude, location.Longitude);
            }
            catch (FeatureNotEnabledException) { /* GPS off */ }
            catch (PermissionException) { break; }

            await Task.Delay(_pollInterval, ct);
        }
    }

    private async Task CheckGeofencesAsync(double lat, double lon)
    {
        var pois = await db.GetActivePoisAsync();

        foreach (var poi in pois) // already sorted by Priority desc
        {
            var dist = Haversine.Distance(lat, lon, poi.Latitude, poi.Longitude);
            if (dist <= poi.Radius)
                await narration.EnqueueAsync(poi);
        }
    }
}

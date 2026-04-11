using System.Net.Http.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Networking;

namespace VinhKhanh.Mobile.Services;

/// <summary>
/// Gửi analytics event (visit, narration, app_open) lên Admin API.
/// Nếu offline → lưu vào hàng chờ và gửi lại khi có mạng.
/// </summary>
public class AnalyticsService
{
    private readonly HttpClient _http;
    private readonly Queue<AnalyticsPayload> _offlineQueue = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private const string Endpoint = "api/analytics/visit";

    public AnalyticsService(HttpClient http)
    {
        _http = http;
        // Tự động flush khi mạng khôi phục
        Connectivity.ConnectivityChanged += async (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
                await FlushOfflineQueueAsync();
        };
    }

    /// <summary>Track lượt mở app.</summary>
    public Task TrackAppOpenAsync() =>
        SendAsync(new AnalyticsPayload
        {
            EventType = "app_open",
            Latitude = 10.7580,
            Longitude = 106.7020
        });

    /// <summary>Track khi người dùng bắt đầu nghe TTS/audio của một POI.</summary>
    public Task TrackNarrationAsync(int poiId, double lat, double lng) =>
        SendAsync(new AnalyticsPayload
        {
            EventType = "narration",
            PoiId = poiId,
            Latitude = lat,
            Longitude = lng
        });

    /// <summary>Track khi người dùng bước vào vùng geofence của POI.</summary>
    public Task TrackPoiVisitAsync(int poiId, double lat, double lng) =>
        SendAsync(new AnalyticsPayload
        {
            EventType = "visit",
            PoiId = poiId,
            Latitude = lat,
            Longitude = lng
        });

    private async Task SendAsync(AnalyticsPayload payload)
    {
        payload.DeviceId = GetDeviceId();

        if (Connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            _offlineQueue.Enqueue(payload);
            return;
        }

        try
        {
            await _http.PostAsJsonAsync(Endpoint, payload);
        }
        catch
        {
            // Lưu vào hàng chờ nếu gửi thất bại
            _offlineQueue.Enqueue(payload);
        }
    }

    /// <summary>Gửi lại tất cả event đang chờ trong queue.</summary>
    private async Task FlushOfflineQueueAsync()
    {
        if (_offlineQueue.Count == 0) return;

        await _flushLock.WaitAsync();
        try
        {
            while (_offlineQueue.Count > 0)
            {
                var payload = _offlineQueue.Peek();
                try
                {
                    await _http.PostAsJsonAsync(Endpoint, payload);
                    _offlineQueue.Dequeue(); // Chỉ xóa khi gửi thành công
                }
                catch
                {
                    break; // Dừng nếu vẫn lỗi, thử lại lần sau
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", string.Empty);
        if (!string.IsNullOrEmpty(id)) return id;

        // Tạo device ID lần đầu và lưu lại
        id = DeviceInfo.Current.Platform.ToString() + "_" + Guid.NewGuid().ToString("N")[..12];
        Preferences.Set("device_id", id);
        return id;
    }
}

/// <summary>Payload gửi lên API analytics.</summary>
public class AnalyticsPayload
{
    public string EventType { get; set; } = "visit";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int? PoiId { get; set; }
}

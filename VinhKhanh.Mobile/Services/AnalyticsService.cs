using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VinhKhanhFoodStreet.Configuration;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// AnalyticsService: Dịch vụ gửi dữ liệu phân tích và trạng thái hoạt động về Backend.
/// 
/// Chức năng chính:
/// - Gửi tọa độ thời gian thực để hiển thị trên bản đồ Heatmap của Admin.
/// - Gửi "Heartbeat" để Admin biết thiết bị đang Online.
/// - Gửi sự kiện khi người dùng bắt đầu nghe thuyết minh (Narration).
/// </summary>
public class AnalyticsService
{
    private readonly HttpClient _httpClient;
    private readonly AccessService _accessService;

    public AnalyticsService(AccessService accessService)
    {
        _accessService = accessService;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Gửi tọa độ và trạng thái hoạt động hiện tại lên Server.
    /// </summary>
    /// <param name="lat">Vĩ độ</param>
    /// <param name="lng">Kinh độ</param>
    /// <param name="eventType">Loại sự kiện: "heartbeat", "visit", "narration"</param>
    /// <param name="poiId">Mã POI (nếu có)</param>
    public async Task TrackActivityAsync(double lat, double lng, string eventType = "heartbeat", int? poiId = null)
    {
        try
        {
            var command = new
            {
                Latitude = lat,
                Longitude = lng,
                DeviceId = _accessService.DeviceId,
                PoiId = poiId,
                EventType = eventType
            };

            var response = await _httpClient.PostAsJsonAsync("api/analytics/visit", command);
            
            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[AnalyticsService] Sync thành công: {eventType} ({lat}, {lng})");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[AnalyticsService] Sync thất bại: {response.StatusCode} - {error}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AnalyticsService] Lỗi kết nối gửi analytics: {ex.Message}");
        }
    }
}

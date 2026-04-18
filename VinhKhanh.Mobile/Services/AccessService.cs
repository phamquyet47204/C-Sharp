using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using VinhKhanhFoodStreet.Configuration;

#if ANDROID
using Android.App;
using Android.Provider;
#endif

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// AccessService: Dịch vụ quản lý quyền truy cập và gói dùng thử (Free Trial).
/// 
/// Chức năng chính:
/// - Định danh thiết bị (Device Identification) để quản lý dùng thử không cần đăng nhập.
/// - Kiểm tra trạng thái gói dùng thử (7 ngày) hoặc gói VIP từ Server.
/// - Đồng bộ thời hạn truy cập giữa Local và Server.
/// - Tự động kích hoạt gói dùng thử cho người dùng mới lần đầu mở app.
/// </summary>
public class AccessService
{
    // Key lưu trữ ngày hết hạn truy cập trong bộ nhớ Preferences của thiết bị
    private const string AccessPassExpiryKey = "access_pass_expiry";
    
    private readonly HttpClient _httpClient;
    private readonly string _deviceId;

    public AccessService()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        // Tạo định danh duy nhất cho thiết bị
        _deviceId = GetPersistentDeviceId();
    }

    /// <summary>
    /// ID định danh của thiết bị hiện tại.
    /// </summary>
    public string DeviceId => _deviceId;

    /// <summary>
    /// Thuật toán lấy Device ID bền vững:
    /// 1. Với Android: Ưu tiên lấy ANDROID_ID (duy nhất theo phần cứng/người dùng).
    /// 2. Với các nền tảng khác hoặc khi lỗi: Sinh một GUID ngẫu nhiên và lưu vào Preferences để dùng lại.
    /// </summary>
    private string GetPersistentDeviceId()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var id = Android.Provider.Settings.Secure.GetString(context.ContentResolver, Android.Provider.Settings.Secure.AndroidId);
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AccessService] Error getting AndroidId: {ex.Message}");
        }
#endif
        // Fallback: Tìm trong bộ nhớ app xem đã từng sinh ID chưa
        var prefId = Preferences.Get("device_id_guid", string.Empty);
        if (string.IsNullOrWhiteSpace(prefId))
        {
            // Lần đầu mở app: sinh ID mới
            prefId = Guid.NewGuid().ToString();
            Preferences.Set("device_id_guid", prefId);
        }
        return prefId;
    }

    /// <summary>
    /// Kiểm tra xem người dùng hiện có quyền truy cập thuyết minh hay không.
    /// So sánh ngày hết hạn lưu ở Local với thời gian thực của hệ thống.
    /// </summary>
    public bool HasActivePass()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate > DateTime.UtcNow;
        }

        return false;
    }

    /// <summary>
    /// Lấy ngày hết hạn quyền truy cập.
    /// </summary>
    public DateTime? GetExpiryDate()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate;
        }
        return null;
    }

    /// <summary>
    /// Tính số ngày sử dụng còn lại.
    /// </summary>
    public int GetRemainingDays()
    {
        var expiry = GetExpiryDate();
        if (expiry == null) return 0;
        
        var remaining = (expiry.Value - DateTime.UtcNow).TotalDays;
        return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
    }

    /// <summary>
    /// Đồng bộ trạng thái dùng thử/gói cước từ Server.
    /// - Gửi DeviceId lên Server để truy vấn hạn dùng.
    /// - Nếu Server trả về hạn dùng mới (ví dụ: vừa mua gói nâng cấp), cập nhật vào Local.
    /// - Nếu là người dùng hoàn toàn mới (chưa dùng pass, chưa dùng trial), tiến hành StartTrial tự động.
    /// </summary>
    public async Task SyncTrialStatusAsync()
    {
        try
        {
            // 1. Kiểm tra trạng thái hiện tại từ backend
            var checkUrl = $"api/access/check?deviceId={_deviceId}";
            var response = await _httpClient.GetAsync(checkUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AccessCheckResponse>();
                if (data != null)
                {
                    // Đồng bộ ngày hết hạn nếu server có thông tin mới hơn
                    if (data.PassExpiryDate.HasValue)
                    {
                        Preferences.Set(AccessPassExpiryKey, data.PassExpiryDate.Value.ToString("O"));
                    }
                    
                    // Logic tự động bắt đầu dùng thử cho máy mới
                    if (!data.HasActivePass && data.TrialRemainingDays == 0 && data.FreeTrialUsed == 0)
                    {
                        await StartTrialAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AccessService] Sync trial error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gọi API Server để kích hoạt gói dùng thử lần đầu tiên cho thiết bị này.
    /// </summary>
    private async Task StartTrialAsync()
    {
        try
        {
            var startUrl = $"api/access/start-trial?deviceId={_deviceId}";
            var response = await _httpClient.PostAsync(startUrl, null);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<TrialStartResponse>();
                if (data != null && data.ExpiryDate.HasValue)
                {
                    // Lưu lại ngày hết hạn dùng thử (Server thường trả về Now + 7 days)
                    Preferences.Set(AccessPassExpiryKey, data.ExpiryDate.Value.ToString("O"));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AccessService] Start trial error: {ex.Message}");
        }
    }

    /// <summary>
    /// Xử lý giả lập sau khi thanh toán thành công (Mục đích Demo/Testing).
    /// </summary>
    public void PurchaseSuccess(int days = 7)
    {
        var newExpiry = DateTime.UtcNow.AddDays(days);
        Preferences.Set(AccessPassExpiryKey, newExpiry.ToString("O"));
    }

    // Các class DTO để Deserialization dữ liệu JSON từ API Access
    private class AccessCheckResponse
    {
        public int FreeTrialUsed { get; set; }
        public int FreeTrialLimit { get; set; }
        public bool HasActivePass { get; set; }
        public DateTime? PassExpiryDate { get; set; }
        public bool IsTrial { get; set; }
        public int TrialRemainingDays { get; set; }
    }

    private class TrialStartResponse
    {
        public bool Success { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int RemainingDays { get; set; }
    }
}

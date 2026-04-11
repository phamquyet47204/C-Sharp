using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanh.Mobile.Services;

<<<<<<< HEAD
public record AccessStatus(int FreeTrialUsed, int FreeTrialLimit, bool HasActivePass, DateTime? PassExpiryDate);
=======
public record AccessStatus(bool IsTrialActive, DateTime? TrialExpiresAt, bool HasActivePass, DateTime? PassExpiryDate);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)

public class AccessControlService(HttpClient http)
{
    private const string CacheKey = "access_status_cache";
<<<<<<< HEAD
    private const int FreeTrialLimit = 3;

    /// <summary>
    /// Kiểm tra Visitor có quyền nghe thuyết minh POI không.
    /// - Còn trong Free Trial (< 3 POI duy nhất) → true
    /// - Có Access Pass còn hạn → true
    /// - Ngược lại → false
    /// Khi offline, dùng cache từ lần kiểm tra gần nhất.
    /// </summary>
    public async Task<bool> CheckAccessAsync(int poiId)
    {
        var status = await GetAccessStatusAsync();
        if (status.HasActivePass) return true;
        return status.FreeTrialUsed < status.FreeTrialLimit;
    }

    /// <summary>Lấy trạng thái truy cập đầy đủ từ server hoặc cache.</summary>
=======
    private const string DeviceIdKey = "device_id";

    /// <summary>
    /// Lần đầu mở app → đăng ký device lên server để bắt đầu tính 7 ngày trial.
    /// Nếu đã đăng ký → server trả về thông tin trial cũ (không reset).
    /// </summary>
    public async Task RegisterDeviceAsync()
    {
        try
        {
            var deviceId = GetOrCreateDeviceId();
            await http.PostAsJsonAsync("api/access/register-device", new { deviceId });
        }
        catch { /* Offline - sẽ thử lại lần sau */ }
    }

>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    public async Task<AccessStatus> GetAccessStatusAsync()
    {
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                return LoadCachedStatus();

<<<<<<< HEAD
            var deviceId = GetDeviceId();
=======
            var deviceId = GetOrCreateDeviceId();
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            var response = await http.GetAsync($"api/access/check?deviceId={Uri.EscapeDataString(deviceId)}");

            if (!response.IsSuccessStatusCode)
                return LoadCachedStatus();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = new AccessStatus(
<<<<<<< HEAD
                FreeTrialUsed: root.GetProperty("freeTrialUsed").GetInt32(),
                FreeTrialLimit: root.GetProperty("freeTrialLimit").GetInt32(),
                HasActivePass: root.GetProperty("hasActivePass").GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var exp) && exp.ValueKind != JsonValueKind.Null
                    ? exp.GetDateTime()
                    : null
=======
                IsTrialActive: root.TryGetProperty("isTrialActive", out var ta) && ta.GetBoolean(),
                TrialExpiresAt: root.TryGetProperty("trialExpiresAt", out var te) && te.ValueKind != JsonValueKind.Null
                    ? te.GetDateTime() : null,
                HasActivePass: root.TryGetProperty("hasActivePass", out var hap) && hap.GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var pe) && pe.ValueKind != JsonValueKind.Null
                    ? pe.GetDateTime() : null
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            );

            SaveCachedStatus(status);
            return status;
        }
        catch
        {
            return LoadCachedStatus();
        }
    }

<<<<<<< HEAD
    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Set("device_id", id);
        }
=======
    /// <summary>Kiểm tra có quyền dùng app không (trial còn hạn hoặc có pass).</summary>
    public async Task<bool> CheckAccessAsync()
    {
        var status = await GetAccessStatusAsync();
        return status.HasActivePass || status.IsTrialActive;
    }

    public static string GetOrCreateDeviceId()
    {
        var id = Preferences.Get(DeviceIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(id)) return id;

        // Tạo device ID lần đầu - kết hợp platform + GUID để unique
        id = $"{DeviceInfo.Current.Platform}_{Guid.NewGuid():N}";
        Preferences.Set(DeviceIdKey, id);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
        return id;
    }

    private static AccessStatus LoadCachedStatus()
    {
        var cached = Preferences.Get(CacheKey, string.Empty);
        if (string.IsNullOrWhiteSpace(cached))
<<<<<<< HEAD
            return new AccessStatus(0, FreeTrialLimit, false, null);
=======
            return new AccessStatus(true, DateTime.UtcNow.AddDays(7), false, null); // Mặc định cho phép khi offline
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)

        try
        {
            var doc = JsonDocument.Parse(cached);
            var root = doc.RootElement;
            return new AccessStatus(
<<<<<<< HEAD
                FreeTrialUsed: root.GetProperty("freeTrialUsed").GetInt32(),
                FreeTrialLimit: root.GetProperty("freeTrialLimit").GetInt32(),
                HasActivePass: root.GetProperty("hasActivePass").GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var exp) && exp.ValueKind != JsonValueKind.Null
                    ? exp.GetDateTime()
                    : null
=======
                IsTrialActive: root.TryGetProperty("isTrialActive", out var ta) && ta.GetBoolean(),
                TrialExpiresAt: root.TryGetProperty("trialExpiresAt", out var te) && te.ValueKind != JsonValueKind.Null
                    ? te.GetDateTime() : null,
                HasActivePass: root.TryGetProperty("hasActivePass", out var hap) && hap.GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var pe) && pe.ValueKind != JsonValueKind.Null
                    ? pe.GetDateTime() : null
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            );
        }
        catch
        {
<<<<<<< HEAD
            return new AccessStatus(0, FreeTrialLimit, false, null);
=======
            return new AccessStatus(true, null, false, null);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
        }
    }

    private static void SaveCachedStatus(AccessStatus status)
    {
        var json = JsonSerializer.Serialize(new
        {
<<<<<<< HEAD
            freeTrialUsed = status.FreeTrialUsed,
            freeTrialLimit = status.FreeTrialLimit,
=======
            isTrialActive = status.IsTrialActive,
            trialExpiresAt = status.TrialExpiresAt,
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            hasActivePass = status.HasActivePass,
            passExpiryDate = status.PassExpiryDate
        });
        Preferences.Set(CacheKey, json);
    }
}

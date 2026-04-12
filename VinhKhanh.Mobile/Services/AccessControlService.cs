using System.Net.Http.Json;
using System.Text.Json;

namespace VinhKhanh.Mobile.Services;

public record AccessStatus(int FreeTrialUsed, int FreeTrialLimit, bool HasActivePass, DateTime? PassExpiryDate);

public class AccessControlService(HttpClient http)
{
    private const string CacheKey = "access_status_cache";
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
    public async Task<AccessStatus> GetAccessStatusAsync()
    {
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                return LoadCachedStatus();

            var deviceId = GetDeviceId();
            var response = await http.GetAsync($"api/access/check?deviceId={Uri.EscapeDataString(deviceId)}");

            if (!response.IsSuccessStatusCode)
                return LoadCachedStatus();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var status = new AccessStatus(
                FreeTrialUsed: root.GetProperty("freeTrialUsed").GetInt32(),
                FreeTrialLimit: root.GetProperty("freeTrialLimit").GetInt32(),
                HasActivePass: root.GetProperty("hasActivePass").GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var exp) && exp.ValueKind != JsonValueKind.Null
                    ? exp.GetDateTime()
                    : null
            );

            SaveCachedStatus(status);
            return status;
        }
        catch
        {
            return LoadCachedStatus();
        }
    }

    private static string GetDeviceId()
    {
        var id = Preferences.Get("device_id", string.Empty);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            Preferences.Set("device_id", id);
        }
        return id;
    }

    private static AccessStatus LoadCachedStatus()
    {
        var cached = Preferences.Get(CacheKey, string.Empty);
        if (string.IsNullOrWhiteSpace(cached))
            return new AccessStatus(0, FreeTrialLimit, false, null);

        try
        {
            var doc = JsonDocument.Parse(cached);
            var root = doc.RootElement;
            return new AccessStatus(
                FreeTrialUsed: root.GetProperty("freeTrialUsed").GetInt32(),
                FreeTrialLimit: root.GetProperty("freeTrialLimit").GetInt32(),
                HasActivePass: root.GetProperty("hasActivePass").GetBoolean(),
                PassExpiryDate: root.TryGetProperty("passExpiryDate", out var exp) && exp.ValueKind != JsonValueKind.Null
                    ? exp.GetDateTime()
                    : null
            );
        }
        catch
        {
            return new AccessStatus(0, FreeTrialLimit, false, null);
        }
    }

    private static void SaveCachedStatus(AccessStatus status)
    {
        var json = JsonSerializer.Serialize(new
        {
            freeTrialUsed = status.FreeTrialUsed,
            freeTrialLimit = status.FreeTrialLimit,
            hasActivePass = status.HasActivePass,
            passExpiryDate = status.PassExpiryDate
        });
        Preferences.Set(CacheKey, json);
    }
}

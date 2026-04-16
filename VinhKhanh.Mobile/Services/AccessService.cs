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

public class AccessService
{
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
        
        // Use a persistent GUID for device ID if hardware ID is unavailable
        _deviceId = GetPersistentDeviceId();
    }

    public string DeviceId => _deviceId;

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
        // Fallback for other platforms or if AndroidId fails
        var prefId = Preferences.Get("device_id_guid", string.Empty);
        if (string.IsNullOrWhiteSpace(prefId))
        {
            prefId = Guid.NewGuid().ToString();
            Preferences.Set("device_id_guid", prefId);
        }
        return prefId;
    }

    public bool HasActivePass()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate > DateTime.UtcNow;
        }

        return false;
    }

    public DateTime? GetExpiryDate()
    {
        var expiryStr = Preferences.Get(AccessPassExpiryKey, string.Empty);
        if (DateTime.TryParse(expiryStr, out var expiryDate))
        {
            return expiryDate;
        }
        return null;
    }

    public int GetRemainingDays()
    {
        var expiry = GetExpiryDate();
        if (expiry == null) return 0;
        
        var remaining = (expiry.Value - DateTime.UtcNow).TotalDays;
        return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
    }

    public async Task SyncTrialStatusAsync()
    {
        try
        {
            // 1. Check current status from server
            var checkUrl = $"api/access/check?deviceId={_deviceId}";
            var response = await _httpClient.GetAsync(checkUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<AccessCheckResponse>();
                if (data != null)
                {
                    // If server has an expiry date, sync it locally
                    if (data.PassExpiryDate.HasValue)
                    {
                        Preferences.Set(AccessPassExpiryKey, data.PassExpiryDate.Value.ToString("O"));
                    }
                    
                    // If trial not started yet and they don't have a pass, auto-start it
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
                    Preferences.Set(AccessPassExpiryKey, data.ExpiryDate.Value.ToString("O"));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AccessService] Start trial error: {ex.Message}");
        }
    }

    public void PurchaseSuccess(int days = 7)
    {
        var newExpiry = DateTime.UtcNow.AddDays(days);
        Preferences.Set(AccessPassExpiryKey, newExpiry.ToString("O"));
        // TODO: Sync purchase to server in a real app
    }

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

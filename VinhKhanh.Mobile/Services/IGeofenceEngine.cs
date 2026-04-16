using System;
using System.Threading.Tasks;
using VinhKhanhFoodStreet.Models;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Engine xu ly geofence cho POI.
/// Layer ViewModel co the lang nghe su kien de trigger UI / audio.
/// </summary>
public interface IGeofenceEngine
{
    /// <summary>
    /// Su kien duoc ban khi nguoi dung vao vung kich hoat cua POI.
    /// </summary>
    event Action<POI>? OnPoiEntered;

    /// <summary>
    /// Su kien duoc ban khi nguoi dung ra khoi vung kich hoat cua POI.
    /// </summary>
    event Action<POI>? OnPoiExited;

    /// <summary>
    /// Bat dau engine (dang ky lang nghe vi tri + khoi tao cache).
    /// </summary>
    Task StartAsync(string languageCode);

    /// <summary>
    /// Dung engine (huy lang nghe vi tri).
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Cap nhat ngon ngu va nap lai cache POI theo ngon ngu moi.
    /// </summary>
    Task SetLanguageAsync(string languageCode);

    /// <summary>
    /// Nap lai cache POI thu cong (nut Refresh tren UI).
    /// </summary>
    Task RefreshPoisAsync();

    /// <summary>
    /// Danh dau POI da phat xong de dua vao cooldown tranh spam lap lai.
    /// </summary>
    void MarkPoiAsPlayed(int poiId, TimeSpan? cooldown = null);
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;
using VinhKhanhFoodStreet.Models;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Geofence Engine phuc vu bai toan thuyet minh theo vi tri.
///
/// Diem chinh:
/// - Dung Haversine de tinh khoang cach chinh xac tren be mat cau.
/// - Debounce 2 lan lien tiep trong vung de giam nhiu GPS.
/// - Cooldown POI sau khi da phat xong de tranh spam lap lai.
/// - Priority: neu trung vung nhieu POI, uu tien POI co Priority cao nhat.
/// - Cache POI tren RAM, khong query DB moi giay.
/// </summary>
public class GeofenceEngine : IGeofenceEngine
{
    private const int EnterDebounceThreshold = 2;
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);

    private readonly ILocationService _locationService;
    private readonly IDatabaseService _databaseService;
    private readonly IAppLanguageService _appLanguageService;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private readonly SemaphoreSlim _processLock = new(1, 1);

    private readonly Dictionary<int, int> _insideStableCounters = new();
    private readonly Dictionary<int, DateTimeOffset> _cooldownUntilUtc = new();
    private readonly HashSet<int> _activePoiIds = new();
    private readonly Dictionary<int, POI> _poiMap = new();

    private List<POI> _cachedPois = new();
    private string _currentLanguageCode = "vi";
    private bool _isStarted;

    public event Action<POI>? OnPoiEntered;
    public event Action<POI>? OnPoiExited;

    public GeofenceEngine(
        ILocationService locationService,
        IDatabaseService databaseService,
        IAppLanguageService appLanguageService)
    {
        _locationService = locationService;
        _databaseService = databaseService;
        _appLanguageService = appLanguageService;
    }

    /// <summary>
    /// Bat dau geofence engine voi ngon ngu hien tai.
    /// </summary>
    public async Task StartAsync(string languageCode)
    {
        await _engineLock.WaitAsync();
        try
        {
            if (_isStarted)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(languageCode))
            {
                _currentLanguageCode = languageCode.Trim();
            }

            await _databaseService.InitializeAsync();
            await RefreshPoisCoreAsync();

            _locationService.LocationChanged += OnLocationChanged;
            await _locationService.StartListeningAsync();

            _isStarted = true;
            Debug.WriteLine("[GeofenceEngine] Da bat geofence engine thanh cong");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeofenceEngine] Loi StartAsync: {ex.Message}");
            throw;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Dung geofence engine va huy lang nghe vi tri.
    /// </summary>
    public async Task StopAsync()
    {
        await _engineLock.WaitAsync();
        try
        {
            if (!_isStarted)
            {
                return;
            }

            _locationService.LocationChanged -= OnLocationChanged;
            await _locationService.StopListeningAsync();

            _insideStableCounters.Clear();
            _activePoiIds.Clear();
            _isStarted = false;

            Debug.WriteLine("[GeofenceEngine] Da dung geofence engine");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GeofenceEngine] Loi StopAsync: {ex.Message}");
            throw;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Cap nhat ngon ngu va reload cache POI theo ngon ngu moi.
    /// </summary>
    public async Task SetLanguageAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("languageCode khong duoc de trong.", nameof(languageCode));
        }

        await _engineLock.WaitAsync();
        try
        {
            _currentLanguageCode = languageCode.Trim();
            await RefreshPoisCoreAsync();

            Debug.WriteLine($"[GeofenceEngine] Da doi ngon ngu sang: {_currentLanguageCode}");
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Reload cache POI khi nguoi dung bam nut Refresh.
    /// </summary>
    public async Task RefreshPoisAsync()
    {
        await _engineLock.WaitAsync();
        try
        {
            await RefreshPoisCoreAsync();
            Debug.WriteLine("[GeofenceEngine] Da refresh cache POI tu database");
        }
        finally
        {
            _engineLock.Release();
        }
    }

    /// <summary>
    /// Danh dau POI da phat xong de ap cooldown, tranh vao/ra lien tuc gay lap audio.
    /// </summary>
    public void MarkPoiAsPlayed(int poiId, TimeSpan? cooldown = null)
    {
        if (poiId <= 0)
        {
            return;
        }

        var effectiveCooldown = cooldown ?? DefaultCooldown;
        _cooldownUntilUtc[poiId] = DateTimeOffset.UtcNow.Add(effectiveCooldown);

        Debug.WriteLine($"[GeofenceEngine] POI #{poiId} vao cooldown {effectiveCooldown.TotalMinutes} phut");
    }

    /// <summary>
    /// Handler su kien vi tri. Tinh toan chay trong Task.Run de tranh block UI thread.
    /// </summary>
    private void OnLocationChanged(Location location)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessLocationAsync(location);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeofenceEngine] Loi xu ly vi tri: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Xu ly geofence moi khi co toa do moi.
    /// </summary>
    private async Task ProcessLocationAsync(Location currentLocation)
    {
        await _processLock.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            CleanupExpiredCooldown(now);

            // Tinh khoang cach toi tung POI va danh dau trang thai ben trong/ben ngoai.
            var insideCandidates = new List<POI>();
            var outsidePois = new List<POI>();

            foreach (var poi in _cachedPois)
            {
                var distanceMeters = CalculateDistance(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    poi.Latitude,
                    poi.Longitude);

                Debug.WriteLine($"[GeofenceEngine] Tính toán khoảng cách: {distanceMeters:F1}m - {poi.Name}");

                if (distanceMeters <= poi.Radius)
                {
                    insideCandidates.Add(poi);
                }
                else
                {
                    outsidePois.Add(poi);
                }
            }

            HandleExitedPois(outsidePois);
            HandleInsidePoisWithPriorityAndDebounce(insideCandidates, now);
        }
        finally
        {
            _processLock.Release();
        }
    }

    /// <summary>
    /// Xu ly cac POI ma nguoi dung da ra khoi vung.
    /// </summary>
    private void HandleExitedPois(List<POI> outsidePois)
    {
        foreach (var poi in outsidePois)
        {
            _insideStableCounters[poi.Id] = 0;

            if (_activePoiIds.Remove(poi.Id))
            {
                Debug.WriteLine($"[GeofenceEngine] Đã ra khỏi vùng: {poi.Name}");
                OnPoiExited?.Invoke(poi);
            }
        }
    }

    /// <summary>
    /// Xu ly danh sach dang o trong vung voi debounce + priority + cooldown.
    /// </summary>
    private void HandleInsidePoisWithPriorityAndDebounce(List<POI> insideCandidates, DateTimeOffset now)
    {
        if (insideCandidates.Count == 0)
        {
            return;
        }

        foreach (var poi in insideCandidates)
        {
            _insideStableCounters.TryGetValue(poi.Id, out var count);
            _insideStableCounters[poi.Id] = count + 1;
        }

        // Chi lay cac POI dat nguong debounce, chua active va khong cooldown.
        var readyToEnter = insideCandidates
            .Where(p => _insideStableCounters.GetValueOrDefault(p.Id, 0) >= EnterDebounceThreshold)
            .Where(p => !_activePoiIds.Contains(p.Id))
            .Where(p => !_cooldownUntilUtc.TryGetValue(p.Id, out var cooldownUntil) || cooldownUntil <= now)
            .ToList();

        if (readyToEnter.Count == 0)
        {
            return;
        }

        // Neu trung nhieu POI cung luc, uu tien Priority cao nhat.
        var selectedPoi = readyToEnter
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Id)
            .First();

        // Neu dang active 1 POI khac co priority thap hon, cho exit truoc de tranh trung audio.
        var lowerPriorityActives = _activePoiIds
            .Select(id => _poiMap.GetValueOrDefault(id))
            .Where(p => p is not null)
            .Cast<POI>()
            .Where(p => p.Id != selectedPoi.Id && p.Priority < selectedPoi.Priority)
            .ToList();

        foreach (var lowerPoi in lowerPriorityActives)
        {
            if (_activePoiIds.Remove(lowerPoi.Id))
            {
                Debug.WriteLine($"[GeofenceEngine] Đã ra khỏi vùng: {lowerPoi.Name} (nhường ưu tiên cho {selectedPoi.Name})");
                OnPoiExited?.Invoke(lowerPoi);
            }
        }

        _activePoiIds.Add(selectedPoi.Id);
        Debug.WriteLine($"[GeofenceEngine] Đã vào vùng quán {selectedPoi.Name}");
        OnPoiEntered?.Invoke(selectedPoi);
    }

    /// <summary>
    /// Nap danh sach POI theo ngon ngu vao RAM de tang hieu nang.
    /// </summary>
    private async Task RefreshPoisCoreAsync()
    {
        var allPois = await _databaseService.GetAllPoisAsync();
        var groupedPois = allPois.GroupBy(GetGroupKey).ToList();

        _cachedPois = groupedPois
            .Select(group => SelectLocalizedPoi(group.ToList(), _currentLanguageCode))
            .Where(p => p is not null)
            .Cast<POI>()
            .OrderByDescending(p => p.Priority)
            .ToList();

        _poiMap.Clear();

        foreach (var poi in _cachedPois)
        {
            _poiMap[poi.Id] = poi;
        }

        // Loai bo active/cooldown counter cua POI khong con ton tai trong cache.
        var validIds = _poiMap.Keys.ToHashSet();

        var activeToRemove = _activePoiIds.Where(id => !validIds.Contains(id)).ToList();
        foreach (var id in activeToRemove)
        {
            _activePoiIds.Remove(id);
        }

        var countersToRemove = _insideStableCounters.Keys.Where(id => !validIds.Contains(id)).ToList();
        foreach (var id in countersToRemove)
        {
            _insideStableCounters.Remove(id);
        }

        var cooldownToRemove = _cooldownUntilUtc.Keys.Where(id => !validIds.Contains(id)).ToList();
        foreach (var id in cooldownToRemove)
        {
            _cooldownUntilUtc.Remove(id);
        }

        Debug.WriteLine($"[GeofenceEngine] Refresh cache all={allPois.Count}, localized={_cachedPois.Count}, language={_currentLanguageCode}");
    }

    private POI? SelectLocalizedPoi(IReadOnlyList<POI> variants, string languageCode)
    {
        var fallbackChain = _appLanguageService.GetLanguageFallbackChain(languageCode);
        foreach (var candidateLanguage in fallbackChain)
        {
            var match = variants.FirstOrDefault(p =>
                string.Equals(NormalizeLanguageCode(p.LanguageCode), NormalizeLanguageCode(candidateLanguage), StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return variants
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault();
    }

    private static string GetGroupKey(POI poi)
    {
        if (!string.IsNullOrWhiteSpace(poi.Category))
        {
            return poi.Category.Trim().ToLowerInvariant();
        }

        var roundedLat = Math.Round(poi.Latitude, 4);
        var roundedLng = Math.Round(poi.Longitude, 4);
        return $"{roundedLat}:{roundedLng}";
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().Replace('_', '-').ToLowerInvariant();
        var shortCode = normalized.Split('-')[0];
        return shortCode == "jp" ? "ja" : shortCode;
    }

    /// <summary>
    /// Don cac POI het han cooldown de bo nho gon hon.
    /// </summary>
    private void CleanupExpiredCooldown(DateTimeOffset now)
    {
        var expiredIds = _cooldownUntilUtc
            .Where(x => x.Value <= now)
            .Select(x => x.Key)
            .ToList();

        foreach (var id in expiredIds)
        {
            _cooldownUntilUtc.Remove(id);
        }
    }

    /// <summary>
    /// Tinh khoang cach (met) bang cong thuc Haversine.
    /// Day la cong thuc phu hop cho khoang cach tren be mat cau Trai Dat.
    /// </summary>
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000d;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var rLat1 = DegreesToRadians(lat1);
        var rLat2 = DegreesToRadians(lat2);

        var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d)
                + Math.Cos(rLat1) * Math.Cos(rLat2)
                * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);

        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degree)
    {
        return degree * Math.PI / 180d;
    }
}

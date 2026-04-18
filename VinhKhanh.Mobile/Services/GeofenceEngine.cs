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
/// Geofence Engine: Bộ máy xử lý logic ranh giới địa lý (Geofencing) phục vụ bài toán thuyết minh tự động.
/// 
/// Chức năng chính:
/// - Tính toán khoảng cách giữa vị trí hiện tại của người dùng và các điểm POI (Point of Interest).
/// - Xác định trạng thái "Vào vùng" (Enter) hoặc "Ra khỏi vùng" (Exit) của một POI.
/// - Phát sự kiện thuyết minh dựa trên các điều kiện về độ ưu tiên (Priority), giới hạn nhiễu (Debounce) và thời gian chờ (Cooldown).
///
/// Các kỹ thuật áp dụng:
/// - Công thức Haversine: Tính khoảng cách chính xác theo đường chim bay trên mặt cầu trái đất.
/// - Cơ chế Debounce: Yêu cầu tọa độ nằm trong vùng đủ N lần liên tiếp mới xác nhận "Vào vùng", tránh nhảy nhiễu GPS.
/// - Cơ chế Cooldown: Ngăn chặn một điểm phát thuyết minh liên tục khi người dùng đứng ở rìa ranh giới.
/// - Xử lý Ưu tiên: Nếu một vùng chứa nhiều POI chồng lấn, ưu tiên phát POI có trọng số Priority lớn nhất.
/// - Cache RAM: Lưu trữ danh sách POI trên bộ nhớ đệm để xử lý thời gian thực mỗi giây mà không cần truy vấn Database liên tục.
/// </summary>
public class GeofenceEngine : IGeofenceEngine
{
    // Ngưỡng debounce: Số lần liên tiếp tọa độ phải nằm trong vùng POI để xác nhận là đã vào vùng thật sự.
    private const int EnterDebounceThreshold = 2;
    
    // Thời gian chờ mặc định (10 phút): Sau khi đã phát xong thuyết minh cho POI này, 
    // phải chờ hết thời gian này mới được phát lại (tránh lập audio khi GPS nhảy).
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);

    private readonly ILocationService _locationService;
    private readonly IDatabaseService _databaseService;
    
    // Khóa Semaphore để đảm bảo tính an toàn đa luồng (thread-safety) khi khởi động hoặc dừng engine.
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    
    // Khóa Semaphore riêng cho việc xử lý tọa độ, đảm bảo các gói tọa độ được xử lý tuần tự.
    private readonly SemaphoreSlim _processLock = new(1, 1);

    // Bộ đếm debounce cho từng POI: Dictionary<PoiId, Counter>
    private readonly Dictionary<int, int> _insideStableCounters = new();
    
    // Danh sách các POI đang bị "đóng băng" chờ phát lại: Dictionary<PoiId, Thời điểm hết cooldown>
    private readonly Dictionary<int, DateTimeOffset> _cooldownUntilUtc = new();
    
    // Tập hợp các PoiId hiện đang được coi là "đang ở bên trong" (Đã qua bước debounce)
    private readonly HashSet<int> _activePoiIds = new();
    
    // Bản đồ tra cứu nhanh POI từ ID
    private readonly Dictionary<int, POI> _poiMap = new();

    // Danh sách POI được tải sẵn vào bộ nhớ (Cache)
    private List<POI> _cachedPois = new();
    private string _currentLanguageCode = "vi";
    private bool _isStarted;

    // Sự kiện được kích hoạt khi xác nhận người dùng chính thức vào vùng POI
    public event Action<POI>? OnPoiEntered;
    
    // Sự kiện được kích hoạt khi người dùng rời khỏi vùng POI
    public event Action<POI>? OnPoiExited;

    public GeofenceEngine(
        ILocationService locationService,
        IDatabaseService databaseService)
    {
        _locationService = locationService;
        _databaseService = databaseService;
    }

    /// <summary>
    /// Khởi động Geofence Engine.
    /// - Thiết lập ngôn ngữ thuyết minh.
    /// - Tải dữ liệu POI từ DB lên Cache.
    /// - Bắt đầu lắng nghe thay đổi tọa độ từ GPS.
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

            // Đảm bảo Database đã sẵn sàng
            await _databaseService.InitializeAsync();
            
            // Tải dữ liệu POI vào RAM lần đầu
            await RefreshPoisCoreAsync();

            // Đăng ký nhận sự kiện từ LocationService
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
    /// Dừng Geofence Engine và giải phóng tài nguyên lắng nghe vị trí.
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

            // Xóa sạch trạng thái tạm thời khi dừng
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
    /// Thay đổi ngôn ngữ hoạt động và làm mới Cache POI (để lấy Description đúng ngôn ngữ).
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
    /// Làm mới dữ liệu POI từ Database (thường gọi khi có bản cập nhật mới từ server).
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
    /// Đánh dấu một POI đã hoàn thành việc phát audio. 
    /// Phương thức này sẽ kích hoạt thời gian Cooldown để ngăn chặn việc phát lại ngay lập tức.
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
    /// Callback xử lý khi LocationService báo về có tòa độ GPS mới.
    /// Sử dụng Task.Run để đẩy việc tính toán nặng ra khỏi luồng UI.
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
    /// Logic cốt lõi: So khớp vị trí người dùng với ranh giới của toàn bộ POI trong Cache.
    /// </summary>
    private async Task ProcessLocationAsync(Location currentLocation)
    {
        // Chặn xử lý song song để đảm bảo trạng thái Counter/Active không bị sai lệch.
        await _processLock.WaitAsync();
        try
        {
            var now = DateTimeOffset.UtcNow;
            CleanupExpiredCooldown(now);

            // Phân loại POI dựa trên khoảng cách
            var insideCandidates = new List<POI>();
            var outsidePois = new List<POI>();

            foreach (var poi in _cachedPois)
            {
                // Tính khoảng cách dựa trên công thức Haversine
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

            // Xử lý những POI vừa ra khỏi vùng
            HandleExitedPois(outsidePois);
            
            // Xử lý những POI đang/vừa vào vùng với bộ lọc Debounce và Priority
            HandleInsidePoisWithPriorityAndDebounce(insideCandidates, now);
        }
        finally
        {
            _processLock.Release();
        }
    }

    /// <summary>
    /// Xử lý các POI đã ra khỏi ranh giới.
    /// - Reset bộ đếm ổn định (Debounce).
    /// - Nếu trước đó POI này từng được xác nhận Active, phát sự kiện OnPoiExited.
    /// </summary>
    private void HandleExitedPois(List<POI> outsidePois)
    {
        foreach (var poi in outsidePois)
        {
            // Reset bộ đếm ổn định ngay khi tọa độ văng ra ngoài ranh giới
            _insideStableCounters[poi.Id] = 0;

            if (_activePoiIds.Remove(poi.Id))
            {
                Debug.WriteLine($"[GeofenceEngine] Đã ra khỏi vùng: {poi.Name}");
                OnPoiExited?.Invoke(poi);
            }
        }
    }

    /// <summary>
    /// Xử lý logic phức tạp khi người dùng nằm ở trong ranh giới một hoặc nhiều POI.
    /// - Áp dụng Debounce: Tránh sai số GPS nhảy ra vào liên tục.
    /// - Kiểm tra Cooldown: Không phát lại POI vừa nghe xong.
    /// - Ưu tiên (Priority): Chỉ phát POI quan trọng nhất nếu có sự chồng lấn.
    /// </summary>
    private void HandleInsidePoisWithPriorityAndDebounce(List<POI> insideCandidates, DateTimeOffset now)
    {
        if (insideCandidates.Count == 0)
        {
            return;
        }

        // Tăng bộ đếm liên tiếp cho các POI đang ở bên trong
        foreach (var poi in insideCandidates)
        {
            _insideStableCounters.TryGetValue(poi.Id, out var count);
            _insideStableCounters[poi.Id] = count + 1;
        }

        // Lọc danh sách các ứng viên thực sự sẵn sàng để phát:
        // 1. Phải vượt qua số lần debounce tối thiểu (ví dụ: 2 lần liên tiếp).
        // 2. Hiện tại không ở trạng thái phát thuyết minh (tránh phát lại liên tục khi đang ở trong vùng).
        // 3. Không nằm trong thời gian Cooldown.
        var readyToEnter = insideCandidates
            .Where(p => _insideStableCounters.GetValueOrDefault(p.Id, 0) >= EnterDebounceThreshold)
            .Where(p => !_activePoiIds.Contains(p.Id))
            .Where(p => !_cooldownUntilUtc.TryGetValue(p.Id, out var cooldownUntil) || cooldownUntil <= now)
            .ToList();

        if (readyToEnter.Count == 0)
        {
            return;
        }

        // Nếu người dùng đứng vào vùng chồng lấn của nhiều quán, chọn một quán duy nhất:
        // - Theo độ ưu tiên (Priority) cao nhất.
        // - Nếu priority bằng nhau, chọn POI có ID nhỏ nhất (để ổn định).
        var selectedPoi = readyToEnter
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Id)
            .First();

        // Xử lý NHƯỜNG ƯU TIÊN (Preemption): 
        // Nếu hiện đang có một POI khác đang "Active" (đang phát) nhưng POI mới này có Priority cao hơn, 
        // ta sẽ ép buộc Exit POI cũ để nhường chỗ cho POI mới.
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

        // Đánh dấu POI được chọn là Active và kích hoạt sự kiện thuyết minh
        _activePoiIds.Add(selectedPoi.Id);
        Debug.WriteLine($"[GeofenceEngine] Đã vào vùng quán {selectedPoi.Name}");
        OnPoiEntered?.Invoke(selectedPoi);
    }

    /// <summary>
    /// Tải dữ liệu POI từ SQLite và tổ chức lại Cache RAM.
    /// </summary>
    private async Task RefreshPoisCoreAsync()
    {
        // Lấy dữ liệu đã được bản địa hóa (Localized) tại tầng DatabaseService
        var localizedPois = await _databaseService.GetLocalizedPoisAsync(_currentLanguageCode);

        // Sắp xếp cache theo Priority để tối ưu hóa việc tìm kiếm sau này
        _cachedPois = localizedPois
            .OrderByDescending(p => p.Priority)
            .ToList();

        _poiMap.Clear();

        foreach (var poi in _cachedPois)
        {
            _poiMap[poi.Id] = poi;
        }

        // Dọn dẹp các ID rác trong bộ nhớ nếu dữ liệu vừa tải lên không còn chứa chúng
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

        Debug.WriteLine($"[GeofenceEngine] Refresh cache localized={_cachedPois.Count}, language={_currentLanguageCode}");
    }

    /// <summary>
    /// Giải phóng bộ nhớ Cooldown cho những POI đã hết hạn đóng băng.
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
    /// Tính khoảng cách (mét) giữa hai điểm tọa độ bằng công thức Haversine.
    /// Đây là giải thuật tối ưu để tính khoảng cách cung tròn trên mặt cầu, 
    /// có tính đến bán kính Trái Đất, cung cấp kết quả chính xác hơn công thức Pythagoras thông thường.
    /// </summary>
    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Bán kính Trái đất tính theo mét
        const double earthRadiusMeters = 6_371_000d;

        // Chuyển đổi độ sang Radian để tính toán lượng giác
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var rLat1 = DegreesToRadians(lat1);
        var rLat2 = DegreesToRadians(lat2);

        // Giải thuật Haversine
        var a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d)
                + Math.Cos(rLat1) * Math.Cos(rLat2)
                * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);

        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        
        // Kết quả tính theo mét
        return earthRadiusMeters * c;
    }

    /// <summary>
    /// Chuyển đổi số đo góc từ ĐỘ sang RADIAN.
    /// </summary>
    private static double DegreesToRadians(double degree)
    {
        return degree * Math.PI / 180d;
    }
}

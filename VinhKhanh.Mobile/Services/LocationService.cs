using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// LocationService: Dịch vụ quản lý và theo dõi tọa độ người dùng theo thời gian thực.
/// 
/// Chức năng chính:
/// - Lắng nghe tọa độ GPS từ phần cứng thiết bị.
/// - Áp dụng bộ lọc khoảng cách (Distance Filter) để tiết kiệm tài nguyên.
/// - Tự động điều chỉnh tần suất lấy mẫu (Adaptive Interval) dựa trên trạng thái di chuyển để tối ưu hóa thời lượng Pin.
/// - Hỗ trợ chạy nền (Background Mode) trên cả Android và iOS để đảm bảo thuyết minh không bị ngắt quãng khi tắt màn hình.
/// </summary>
public class LocationService : ILocationService
{
    // Khoảng cách tối thiểu giữa 2 lần cập nhật (1m). Giúp Heatmap mượt mà hơn.
    private const double DistanceFilterMeters = 1d;
    
    // Ngưỡng tốc độ để coi là đứng yên (1 km/h). Tốc độ dưới mức này sẽ được tính vào thời gian dừng.
    private const double StationarySpeedThresholdKmh = 1d;
    
    // Thời gian tối đa giữ im lặng (15 giây).
    private static readonly TimeSpan MaxSilentEmitInterval = TimeSpan.FromSeconds(15);
    
    // Chu kỳ lấy mẫu khi đang di chuyển (15 giây/lần). 
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(15);
    
    // Chu kỳ lấy mẫu khi đứng yên (15 giây/lần). 
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(15);
    
    // Ngưỡng thời gian để xác nhận trạng thái đứng yên (1 phút).
    private static readonly TimeSpan StationaryDurationThreshold = TimeSpan.FromMinutes(1);

    private readonly IGeolocation _geolocation;
    private readonly AnalyticsService _analyticsService;
    
    // Khóa trạng thái để đảm bảo Start/Stop diễn ra an toàn, không bị tranh chấp luồng.
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _listeningTask;
    private bool _isListening;

    // Lưu trữ tọa độ thô vừa nhận được từ GPS
    private Location? _lastRawLocation;
    private DateTimeOffset? _lastRawTimestampUtc;
    
    // Lưu trữ tọa độ cuối cùng mà Service đã phát (Emit) sự kiện ra bên ngoài
    private Location? _lastEmittedLocation;
    private DateTimeOffset? _lastEmittedAtUtc;
    
    // Tổng thời gian người dùng đứng yên liên tục
    private TimeSpan _stationaryDuration = TimeSpan.Zero;
    
    // Khoảng thời gian nghỉ hiện tại giữa các lần lấy mẫu
    private TimeSpan _currentInterval = ActiveInterval;

    public event Action<Location>? LocationChanged;

    public LocationService(IGeolocation geolocation, AnalyticsService analyticsService)
    {
        _geolocation = geolocation;
        _analyticsService = analyticsService;
    }

    /// <summary>
    /// Kích hoạt dịch vụ theo dõi vị trí.
    /// - Kiểm tra và xin quyền truy cập GPS.
    /// - Cấu hình chế độ chạy nền đặc thù cho từng nền tảng (Foreground Service trên Android).
    /// - Bắt đầu vòng lặp lấy mẫu tọa độ.
    /// </summary>
    public async Task StartListeningAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_isListening)
            {
                return;
            }

            Debug.WriteLine("[LocationService] Bắt đầu Service");

            // 1. Kiểm tra quyền
            await EnsureLocationPermissionsAsync();
            
            // 2. Cấu hình chế độ chạy nền (Hệ điều hành yêu cầu cấu hình trước khi Start)
            await ConfigurePlatformBackgroundModeAsync();
            await StartPlatformBackgroundModeAsync();

            _cts = new CancellationTokenSource();
            _isListening = true;
            _currentInterval = ActiveInterval;
            _stationaryDuration = TimeSpan.Zero;

            // Chạy vòng lặp lấy mẫu trên một Task riêng biệt
            _listeningTask = ListenLoopAsync(_cts.Token);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Ngừng theo dõi vị trí và giải phóng các tài nguyên nền.
    /// </summary>
    public async Task StopListeningAsync()
    {
        CancellationTokenSource? ctsToCancel = null;
        Task? listeningTask = null;

        await _stateLock.WaitAsync();
        try
        {
            if (!_isListening)
            {
                return;
            }

            _isListening = false;
            ctsToCancel = _cts;
            listeningTask = _listeningTask;

            _cts = null;
            _listeningTask = null;
            
            // Reset trạng thái
            _lastRawLocation = null;
            _lastRawTimestampUtc = null;
            _lastEmittedLocation = null;
            _lastEmittedAtUtc = null;
            _stationaryDuration = TimeSpan.Zero;
            _currentInterval = ActiveInterval;
        }
        finally
        {
            _stateLock.Release();
        }

        try
        {
            // Hủy bỏ Task vòng lặp
            ctsToCancel?.Cancel();
            if (listeningTask is not null)
            {
                await listeningTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Đây là hành vi bình thường khi Task bị Cancel
        }
        finally
        {
            ctsToCancel?.Dispose();
            
            // Tắt chế độ chạy nền để tiết kiệm tài nguyên hệ thống
            await StopPlatformBackgroundModeAsync();
        }
    }

    /// <summary>
    /// Vòng lặp chính thực hiện truy vấn tọa độ GPS.
    /// Tần suất vòng lặp được thay đổi linh hoạt thông qua biến _currentInterval.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Cấu hình yêu cầu GPS: Ưu tiên Best (độ chính xác cao nhất) và Timeout 15s
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));
                var location = await _geolocation.GetLocationAsync(request, cancellationToken);

                if (location is not null)
                {
                    Debug.WriteLine($"[LocationService] Lấy tọa độ thành công: Lat={location.Latitude}, Lng={location.Longitude}");

                    // Phân tích trạng thái di chuyển để điều chỉnh chu kỳ lấy mẫu tiếp theo
                    UpdateAdaptiveInterval(location);

                    // Kiểm tra xem tọa độ mới có đủ điều kiện để phát sự kiện hay không (Lọc nhiễu)
                    if (ShouldEmitLocationChanged(location))
                    {
                        _lastEmittedLocation = location;
                        _lastEmittedAtUtc = DateTimeOffset.UtcNow;
                        
                        // Thông báo cho các thành phần nội bộ (Geofence, UI)
                        LocationChanged?.Invoke(location);
                        
                        // Chỉ gửi về Backend nếu loop chưa bị hủy (tránh race condition khi đóng app)
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            // Đồng bộ trạng thái Online và vị trí về Backend cho Admin theo dõi
                            _ = _analyticsService.TrackActivityAsync(location.Latitude, location.Longitude, "location_update");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[LocationService] Mất tín hiệu GPS: dữ liệu vị trí null");
                }
            }
            catch (FeatureNotSupportedException ex)
            {
                Debug.WriteLine($"[LocationService] Thiet bi khong ho tro GPS: {ex.Message}");
            }
            catch (PermissionException ex)
            {
                Debug.WriteLine($"[LocationService] Mat quyen truy cap vi tri: {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocationService] Loi khong xac dinh khi lay vi tri: {ex.Message}");
            }

            // Nghỉ một khoảng thời gian trước khi lấy mẫu tiếp theo
            try
            {
                await Task.Delay(_currentInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Bộ lọc sự kiện tọa độ: 
    /// Ngăn chặn việc bắn quá nhiều sự kiện khi người dùng chỉ xê dịch nhẹ (nhiễu GPS).
    /// </summary>
    private bool ShouldEmitLocationChanged(Location location)
    {
        if (_lastEmittedLocation is null)
        {
            return true;
        }

        // 1. Nếu di chuyển vượt ngưỡng DistanceFilterMeters (5m) thì chấp nhận dữ liệu mới
        var distanceKm = Location.CalculateDistance(_lastEmittedLocation, location, DistanceUnits.Kilometers);
        var distanceMeters = distanceKm * 1000d;

        if (distanceMeters >= DistanceFilterMeters)
        {
            return true;
        }

        // 2. Nếu đứng yên quá MaxSilentEmitInterval (10s), vẫn phát sự kiện (Heartbeat). 
        // Điều này rất quan trọng cho GeofenceEngine để thực hiện giải thuật Debounce (cần N lần tọa độ nằm trong vùng liên tiếp).
        if (_lastEmittedAtUtc.HasValue && DateTimeOffset.UtcNow - _lastEmittedAtUtc.Value >= MaxSilentEmitInterval)
        {
            Debug.WriteLine("[LocationService] Heartbeat vi tri khi dung yen de cap nhat geofence");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Thuật toán Thích nghi tần suất (Adaptive Sampling):
    /// - Nếu người dùng đang di chuyển: Lấy mẫu dầy (2s/lần) để đảm bảo không bỏ lỡ quán nào khi đi nhanh.
    /// - Nếu người dùng đứng yên (ở trong quán/dừng đèn đỏ) trên 1 phút: Giãn tần suất (10s/lần) để tiết kiệm Pin.
    /// </summary>
    private void UpdateAdaptiveInterval(Location location)
    {
        var now = DateTimeOffset.UtcNow;

        if (_lastRawLocation is not null && _lastRawTimestampUtc.HasValue)
        {
            var elapsed = now - _lastRawTimestampUtc.Value;
            var speedKmh = ResolveSpeedKmh(location, elapsed);

            // Nếu tốc độ < 1km/h thì được coi là đang đứng yên
            if (speedKmh < StationarySpeedThresholdKmh)
            {
                _stationaryDuration += elapsed;
            }
            else
            {
                // Reset bộ đếm đứng yên ngay khi có dấu hiệu di chuyển
                _stationaryDuration = TimeSpan.Zero;
            }
        }

        // Xác định chu kỳ dựa trên thời gian đứng yên
        var newInterval = _stationaryDuration >= StationaryDurationThreshold
            ? IdleInterval
            : ActiveInterval;

        if (newInterval != _currentInterval)
        {
            _currentInterval = newInterval;
            Debug.WriteLine($"[LocationService] Dieu chinh chu ky lay vi tri: {_currentInterval.TotalSeconds}s/lan");
        }

        _lastRawLocation = location;
        _lastRawTimestampUtc = now;
    }

    /// <summary>
    /// Tính toán vận tốc km/h. 
    /// Ưu tiên lấy trực tiếp từ phần cứng (máy xịn có cảm biến gia tốc/GPS xịn), 
    /// nếu không có thì tính bằng công thức chia quãng đường/thời gian.
    /// </summary>
    private double ResolveSpeedKmh(Location currentLocation, TimeSpan elapsed)
    {
        if (currentLocation.Speed.HasValue && currentLocation.Speed.Value >= 0)
        {
            return currentLocation.Speed.Value * 3.6d; // m/s -> km/h
        }

        if (_lastRawLocation is null || elapsed.TotalSeconds <= 0)
        {
            return 0d;
        }

        var distanceKm = Location.CalculateDistance(_lastRawLocation, currentLocation, DistanceUnits.Kilometers);
        return distanceKm / elapsed.TotalHours;
    }

    /// <summary>
    /// Xử lý cấp quyền truy cập vị trí một cách nghiêm ngặt.
    /// - Yêu cầu "While in use" để đảm bảo tính năng cơ bản.
    /// - Cố gắng xin thêm "Always" để hỗ trợ chạy nền khi tắt màn hình.
    /// </summary>
    private static async Task EnsureLocationPermissionsAsync()
    {
        var whenInUseStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (whenInUseStatus != PermissionStatus.Granted)
        {
            whenInUseStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        if (whenInUseStatus != PermissionStatus.Granted)
        {
            throw new PermissionException("Nguoi dung tu choi quyen LocationWhenInUse.");
        }

        // Quyền "Always Allow" là tùy chọn nâng cao. Nếu bị từ chối, ứng dụng vẫn hoạt động 
        // nhưng có thể bị hệ điều hành tạm dừng khi người dùng thoát ra màn hình chính quá lâu.
        try
        {
            var alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (alwaysStatus != PermissionStatus.Granted)
            {
                alwaysStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
            }

            if (alwaysStatus != PermissionStatus.Granted)
            {
                Debug.WriteLine("[LocationService] LocationAlways bi tu choi, app chi theo doi vi tri khi o foreground.");
                await ShowAlwaysPermissionExplanationAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Khong the xin quyen LocationAlways: {ex.Message}");
        }
    }

    /// <summary>
    /// Hiển thị thông báo giải thích vì sao cần quyền "Always".
    /// Giúp tăng tỷ lệ người dùng mở cài đặt để cho phép ứng dụng chạy ổn định hơn.
    /// </summary>
    private static async Task ShowAlwaysPermissionExplanationAsync()
    {
        try
        {
            var page = Application.Current?.Windows?[0]?.Page;
            if (page is null)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await page.DisplayAlertAsync(
                    "Cần quyền Vị trí Luôn luôn",
                    "Ứng dụng cần quyền vị trí Always để tiếp tục thuyết minh khi bạn tắt màn hình hoặc chuyển ứng dụng sang ứng dụng khác.",
                    "Đã hiểu");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Loi hien thong bao quyen: {ex.Message}");
        }
    }

    /// <summary>
    /// Đăng ký cấu hình chạy nền đặc thù theo Platform.
    /// </summary>
    private static Task ConfigurePlatformBackgroundModeAsync()
    {
#if IOS
        iOSLocationBackgroundConfigurator.Configure();
#endif
        return Task.CompletedTask;
    }

    /// <summary>
    /// Kích hoạt trạng thái Foreground cho phép ứng dụng chiếm dụng GPS khi ở chế độ nền.
    /// Đối với Android, điều này thường đi kèm với việc hiển thị một Thông báo (Notification) không thể xóa.
    /// </summary>
    private static Task StartPlatformBackgroundModeAsync()
    {
#if ANDROID
        AndroidLocationForegroundController.Start();
#endif
        return Task.CompletedTask;
    }

    /// <summary>
    /// Ngừng chiếm dụng GPS ở chế độ nền.
    /// </summary>
    private static Task StopPlatformBackgroundModeAsync()
    {
#if ANDROID
        AndroidLocationForegroundController.Stop();
#endif
        return Task.CompletedTask;
    }
}

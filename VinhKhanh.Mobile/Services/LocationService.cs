using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Service theo doi vi tri theo thoi gian thuc cho module thuyet minh.
/// - Dung Geolocation voi do chinh xac cao (Best).
/// - Co bo loc khoang cach toi thieu 5m de tranh ban su kien qua nhieu.
/// - Co co che thich nghi tan suat lay vi tri de toi uu pin.
/// </summary>
public class LocationService : ILocationService
{
    private const double DistanceFilterMeters = 5d;
    private const double StationarySpeedThresholdKmh = 1d;
    private static readonly TimeSpan MaxSilentEmitInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StationaryDurationThreshold = TimeSpan.FromMinutes(1);

    private readonly IGeolocation _geolocation;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private CancellationTokenSource? _cts;
    private Task? _listeningTask;
    private bool _isListening;

    private Location? _lastRawLocation;
    private DateTimeOffset? _lastRawTimestampUtc;
    private Location? _lastEmittedLocation;
    private DateTimeOffset? _lastEmittedAtUtc;
    private TimeSpan _stationaryDuration = TimeSpan.Zero;
    private TimeSpan _currentInterval = ActiveInterval;

    public event Action<Location>? LocationChanged;

    public LocationService(IGeolocation geolocation)
    {
        _geolocation = geolocation;
    }

    /// <summary>
    /// Bat dau theo doi vi tri va kich hoat che do background mode theo tung nen tang.
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

            await EnsureLocationPermissionsAsync();
            await ConfigurePlatformBackgroundModeAsync();
            await StartPlatformBackgroundModeAsync();

            _cts = new CancellationTokenSource();
            _isListening = true;
            _currentInterval = ActiveInterval;
            _stationaryDuration = TimeSpan.Zero;

            _listeningTask = ListenLoopAsync(_cts.Token);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <summary>
    /// Dung theo doi vi tri va tat che do background mode.
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
            ctsToCancel?.Cancel();
            if (listeningTask is not null)
            {
                await listeningTask;
            }
        }
        catch (OperationCanceledException)
        {
            // Hanh vi mong doi khi stop service.
        }
        finally
        {
            ctsToCancel?.Dispose();
            await StopPlatformBackgroundModeAsync();
        }
    }

    /// <summary>
    /// Vong lap lay vi tri lien tuc theo chu ky thich nghi.
    /// </summary>
    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(15));
                var location = await _geolocation.GetLocationAsync(request, cancellationToken);

                if (location is not null)
                {
                    Debug.WriteLine($"[LocationService] Lấy tọa độ thành công: Lat={location.Latitude}, Lng={location.Longitude}");

                    UpdateAdaptiveInterval(location);

                    if (ShouldEmitLocationChanged(location))
                    {
                        _lastEmittedLocation = location;
                        _lastEmittedAtUtc = DateTimeOffset.UtcNow;
                        LocationChanged?.Invoke(location);
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
                // Ket thuc vong lap khi service bi huy.
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocationService] Loi khong xac dinh khi lay vi tri: {ex.Message}");
            }

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
    /// Bo loc su kien vi tri:
    /// - Neu di chuyen >= 5m thi ban ngay.
    /// - Neu dung yen qua lau, van ban heartbeat dinh ky de geofence debounce co du du lieu kich hoat.
    /// </summary>
    private bool ShouldEmitLocationChanged(Location location)
    {
        if (_lastEmittedLocation is null)
        {
            return true;
        }

        var distanceKm = Location.CalculateDistance(_lastEmittedLocation, location, DistanceUnits.Kilometers);
        var distanceMeters = distanceKm * 1000d;

        if (distanceMeters >= DistanceFilterMeters)
        {
            return true;
        }

        if (_lastEmittedAtUtc.HasValue && DateTimeOffset.UtcNow - _lastEmittedAtUtc.Value >= MaxSilentEmitInterval)
        {
            Debug.WriteLine("[LocationService] Heartbeat vi tri khi dung yen de cap nhat geofence");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Dieu chinh tan suat lay vi tri:
    /// - Dung yen >= 1 phut (toc do &lt; 1km/h): 10s/lan.
    /// - Co di chuyen: 2s/lan.
    /// </summary>
    private void UpdateAdaptiveInterval(Location location)
    {
        var now = DateTimeOffset.UtcNow;

        if (_lastRawLocation is not null && _lastRawTimestampUtc.HasValue)
        {
            var elapsed = now - _lastRawTimestampUtc.Value;
            var speedKmh = ResolveSpeedKmh(location, elapsed);

            if (speedKmh < StationarySpeedThresholdKmh)
            {
                _stationaryDuration += elapsed;
            }
            else
            {
                _stationaryDuration = TimeSpan.Zero;
            }
        }

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
    /// Tinh van toc km/h uu tien tu GPS speed, fallback bang quang duong/thoi gian.
    /// </summary>
    private double ResolveSpeedKmh(Location currentLocation, TimeSpan elapsed)
    {
        if (currentLocation.Speed.HasValue && currentLocation.Speed.Value >= 0)
        {
            return currentLocation.Speed.Value * 3.6d;
        }

        if (_lastRawLocation is null || elapsed.TotalSeconds <= 0)
        {
            return 0d;
        }

        var distanceKm = Location.CalculateDistance(_lastRawLocation, currentLocation, DistanceUnits.Kilometers);
        return distanceKm / elapsed.TotalHours;
    }

    /// <summary>
    /// Xin quyen vi tri:
    /// - Bat buoc LocationWhenInUse.
    /// - Co gang xin LocationAlways nhung khong crash neu bi tu choi (chi warn).
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

        // LocationAlways la tuy chon: neu bi tu choi thi app van chay binh thuong khi o foreground.
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
            // Khong crash neu quyen Always khong kha dung (emulator, mot so thiet bi).
            Debug.WriteLine($"[LocationService] Khong the xin quyen LocationAlways: {ex.Message}");
        }
    }

    /// <summary>
    /// Thong bao ro ly do can quyen Always de nguoi dung hieu va cap lai quyen.
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
                    "Can quyen Vi tri Luon luon",
                    "Ung dung can quyen vi tri Always de tiep tuc thuyet minh khi ban tat man hinh hoac chuyen ung dung sang nen.",
                    "Da hieu");
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Loi hien thong bao quyen: {ex.Message}");
        }
    }

    /// <summary>
    /// Khoi dong che do background mode theo nen tang.
    /// Android se chay Foreground Service de tranh bi he dieu hanh kill khi tat man hinh.
    /// iOS se bat co cho phep cap nhat vi tri nen thong qua CLLocationManager.
    /// </summary>
    private static Task ConfigurePlatformBackgroundModeAsync()
    {
#if IOS
        iOSLocationBackgroundConfigurator.Configure();
#endif
        return Task.CompletedTask;
    }

    /// <summary>
    /// Bat co che background sau khi da xin quyen thanh cong.
    /// </summary>
    private static Task StartPlatformBackgroundModeAsync()
    {
#if ANDROID
        AndroidLocationForegroundController.Start();
#endif
        return Task.CompletedTask;
    }

    /// <summary>
    /// Dung co che background khi khong con tracking.
    /// </summary>
    private static Task StopPlatformBackgroundModeAsync()
    {
#if ANDROID
        AndroidLocationForegroundController.Stop();
#endif
        return Task.CompletedTask;
    }
}

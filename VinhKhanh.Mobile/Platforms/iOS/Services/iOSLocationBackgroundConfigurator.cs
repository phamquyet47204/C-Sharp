#if IOS
using System;
using System.Diagnostics;
using CoreLocation;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Cau hinh native iOS cho location background.
///
/// Diem can luu y:
/// - AllowsBackgroundLocationUpdates = true: cho phep cap nhat vi tri khi app chay nen.
/// - PausesLocationUpdatesAutomatically = false: han che iOS tu dong tam dung update.
///
/// Ngoai code nay, ban can bo sung trong Info.plist:
/// - NSLocationWhenInUseUsageDescription
/// - NSLocationAlwaysAndWhenInUseUsageDescription
/// - UIBackgroundModes voi gia tri "location"
/// </summary>
public static class iOSLocationBackgroundConfigurator
{
    private static CLLocationManager? _locationManager;

    public static void Configure()
    {
        try
        {
            _locationManager ??= new CLLocationManager();
            _locationManager.AllowsBackgroundLocationUpdates = true;
            _locationManager.PausesLocationUpdatesAutomatically = false;

            Debug.WriteLine("[LocationService] Da cau hinh iOS background location thanh cong");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LocationService] Loi cau hinh iOS background location: {ex.Message}");
        }
    }
}
#endif

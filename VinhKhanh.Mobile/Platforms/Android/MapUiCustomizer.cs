#if ANDROID
using Android.Gms.Maps;
using Android.Views;
using Microsoft.Maui.Maps.Handlers;

namespace VinhKhanh.Mobile.Platforms.Android;

public static class MapUiCustomizer
{
    public static void Configure(IMapHandler handler)
    {
        try
        {
            var mapView = handler?.PlatformView as MapView;
            if (mapView == null) return;

            mapView.GetMapAsync(new MapReadyCallback());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] Configure failed: {ex}");
        }
    }

    private sealed class MapReadyCallback : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap)
        {
            // Tắt controls mặc định — dùng custom buttons trong MAUI thay thế
            googleMap.UiSettings.ZoomControlsEnabled = false;
            googleMap.UiSettings.MyLocationButtonEnabled = false;
            googleMap.UiSettings.CompassEnabled = true;
            googleMap.UiSettings.MapToolbarEnabled = false;
        }
    }
}
#endif

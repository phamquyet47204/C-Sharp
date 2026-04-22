#if ANDROID
using Android.Gms.Maps;
using Android.Util;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Maps.Handlers;

namespace VinhKhanhFoodStreet.Platforms.Android;

public static class MapUiCustomizer
{
    public static void Configure(IMapHandler handler)
    {
        try
        {
            var mapView = handler?.PlatformView as MapView;
            if (mapView == null)
            {
                return;
            }

            // Lay GoogleMap khi san sang, sau do tinh chinh UI controls.
            mapView.GetMapAsync(new MyLocationButtonCallback(mapView));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] Configure failed: {ex}");
        }
    }

    private sealed class MyLocationButtonCallback(MapView mapView) : Java.Lang.Object, IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap googleMap)
        {
            try
            {
                // TAT nut zoom UI de giao dien Map phang va sach se nhat, van co the dung 2 ngon tay de zoom.
                googleMap.UiSettings.ZoomControlsEnabled = false;
                
                // TAT chuc nang hien nut My Location cua Google (chung ta da co nut Custom tuyet dep roi)
                googleMap.UiSettings.MyLocationButtonEnabled = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] OnMapReady failed: {ex}");
            }
        }

        private static int DpToPx(MapView mapView, int dp)
        {
            var displayMetrics = mapView.Resources?.DisplayMetrics;
            if (displayMetrics == null)
            {
                return dp;
            }

            return (int)TypedValue.ApplyDimension(
                ComplexUnitType.Dip,
                dp,
                displayMetrics);
        }
    }
}
#endif

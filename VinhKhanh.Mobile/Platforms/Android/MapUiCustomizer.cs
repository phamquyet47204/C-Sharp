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
                // Bat zoom controls de nguoi dung co cum zoom in/out ro rang.
                googleMap.UiSettings.ZoomControlsEnabled = true;
                googleMap.UiSettings.MyLocationButtonEnabled = true;

                MoveMyLocationButtonNearZoom(mapView);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] OnMapReady failed: {ex}");
            }
        }

        private static void MoveMyLocationButtonNearZoom(MapView mapView)
        {
            try
            {
                // Cac view id noi bo cua Google Map UI (on dinh tren Android):
                // "1" la container map controls, "2" la nut My Location.
                var locationButton = mapView.FindViewById(int.Parse("2"));
                var locationButtonParent = locationButton?.Parent as global::Android.Views.View;

                if (locationButton == null || locationButtonParent == null)
                {
                    return;
                }

                if (locationButton.LayoutParameters is not RelativeLayout.LayoutParams layoutParams)
                {
                    return;
                }

                // Bo canh tren, canh phai; day xuong duoi de nam gan cum zoom in/out.
                layoutParams.RemoveRule(LayoutRules.AlignParentTop);
                layoutParams.RemoveRule(LayoutRules.AlignParentLeft);

                layoutParams.AddRule(LayoutRules.AlignParentBottom, (int)LayoutRules.True);
                layoutParams.AddRule(LayoutRules.AlignParentEnd, (int)LayoutRules.True);

                var rightMargin = DpToPx(mapView, 16);
                var bottomMargin = DpToPx(mapView, 120);

                layoutParams.SetMargins(rightMargin, 0, rightMargin, bottomMargin);
                locationButton.LayoutParameters = layoutParams;
                locationButtonParent.RequestLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] Move button failed: {ex}");
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

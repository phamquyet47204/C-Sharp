#if ANDROID
using Android.Gms.Maps;
<<<<<<< HEAD
using Android.Util;
using Android.Views;
using Android.Widget;
=======
using Android.Views;
using Android.Widget;
using Android.Util;
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
using Microsoft.Maui.Maps.Handlers;

namespace VinhKhanhFoodStreet.Platforms.Android;

public static class MapUiCustomizer
{
    public static void Configure(IMapHandler handler)
    {
        try
        {
            var mapView = handler?.PlatformView as MapView;
<<<<<<< HEAD
            if (mapView == null)
            {
                return;
            }

            // Lay GoogleMap khi san sang, sau do tinh chinh UI controls.
            mapView.GetMapAsync(new MyLocationButtonCallback(mapView));
=======
            if (mapView == null) return;
            mapView.GetMapAsync(new MapReadyCallback(mapView));
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] Configure failed: {ex}");
        }
    }

<<<<<<< HEAD
    private sealed class MyLocationButtonCallback(MapView mapView) : Java.Lang.Object, IOnMapReadyCallback
=======
    private sealed class MapReadyCallback(MapView mapView) : Java.Lang.Object, IOnMapReadyCallback
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    {
        public void OnMapReady(GoogleMap googleMap)
        {
            try
            {
<<<<<<< HEAD
                // Bat zoom controls de nguoi dung co cum zoom in/out ro rang.
                googleMap.UiSettings.ZoomControlsEnabled = true;
                googleMap.UiSettings.MyLocationButtonEnabled = true;

                MoveMyLocationButtonNearZoom(mapView);
=======
                // Bật zoom controls mặc định của Google
                googleMap.UiSettings.ZoomControlsEnabled = true;
                googleMap.UiSettings.MyLocationButtonEnabled = true;

                // Tắt callout khi tap pin - MAUI xử lý qua MapClicked
                googleMap.MarkerClick += (_, e) => { e.Handled = true; };

                // Di chuyển zoom controls + MyLocation xuống góc phải dưới
                MoveControlsToBottomRight(mapView);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] OnMapReady failed: {ex}");
            }
        }

<<<<<<< HEAD
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
=======
        private static void MoveControlsToBottomRight(MapView mapView)
        {
            try
            {
                // Google Maps internal view IDs (stable):
                // ID 1 = zoom controls container, ID 2 = MyLocation button
                var zoomControls  = mapView.FindViewById(1);
                var myLocationBtn = mapView.FindViewById(2);

                const int rightDp  = 12;
                const int bottomDp = 80; // trên tab bar 48dp + padding

                if (zoomControls?.LayoutParameters is RelativeLayout.LayoutParams zp)
                {
                    zp.RemoveRule(LayoutRules.AlignParentTop);
                    zp.RemoveRule(LayoutRules.AlignParentLeft);
                    zp.AddRule(LayoutRules.AlignParentBottom, (int)LayoutRules.True);
                    zp.AddRule(LayoutRules.AlignParentEnd,    (int)LayoutRules.True);
                    zp.SetMargins(0, 0, DpToPx(mapView, rightDp), DpToPx(mapView, bottomDp));
                    zoomControls.LayoutParameters = zp;
                }

                if (myLocationBtn?.LayoutParameters is RelativeLayout.LayoutParams lp)
                {
                    lp.RemoveRule(LayoutRules.AlignParentTop);
                    lp.RemoveRule(LayoutRules.AlignParentLeft);
                    lp.AddRule(LayoutRules.AlignParentBottom, (int)LayoutRules.True);
                    lp.AddRule(LayoutRules.AlignParentEnd,    (int)LayoutRules.True);
                    // MyLocation ngay trên zoom controls (~110dp)
                    lp.SetMargins(0, 0, DpToPx(mapView, rightDp), DpToPx(mapView, bottomDp + 110));
                    myLocationBtn.LayoutParameters = lp;
                }

                (zoomControls?.Parent as global::Android.Views.View)?.RequestLayout();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapUiCustomizer] MoveControls failed: {ex}");
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
            }
        }

        private static int DpToPx(MapView mapView, int dp)
        {
<<<<<<< HEAD
            var displayMetrics = mapView.Resources?.DisplayMetrics;
            if (displayMetrics == null)
            {
                return dp;
            }

            return (int)TypedValue.ApplyDimension(
                ComplexUnitType.Dip,
                dp,
                displayMetrics);
=======
            var metrics = mapView.Resources?.DisplayMetrics;
            if (metrics == null) return dp;
            return (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, metrics);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
        }
    }
}
#endif

using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.ComponentModel;
using VinhKhanh.Mobile.Models;
using VinhKhanh.Mobile.Services;
using VinhKhanh.Mobile.ViewModels;

namespace VinhKhanh.Mobile.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;
    private readonly NarrationEngine _narration;
    private double _currentRadiusMeters = 300;
    private readonly Dictionary<Pin, PoiRecord> _pinMap = [];
    private PoiRecord? _currentPoiCard;

    public MapPage(MapViewModel vm, NarrationEngine narration)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _narration = narration;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        MainMap.MapClicked += OnMapClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.StartMonitoring();
        PlacePins(_vm.FilteredPois);
        CenterOnVinhKhanh();
        UpdateFreeTrialBanner();
    }

    protected override void OnDisappearing()
    {
        _vm.StopMonitoring();
        base.OnDisappearing();
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        if (args.NewHandler is null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnHandlerChanging(args);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.FilteredPois) or nameof(MapViewModel.Pois))
            MainThread.BeginInvokeOnMainThread(() => PlacePins(_vm.FilteredPois));

        if (e.PropertyName is nameof(MapViewModel.AccessStatus) or nameof(MapViewModel.FreeTrialMessage))
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PlacePins(_vm.FilteredPois);
                UpdateFreeTrialBanner();
            });
    }

    // Requirement 14.3 & 14.6: hiển thị lock icon trên premium POI khi chưa có pass
    private void PlacePins(IEnumerable<PoiRecord> pois)
    {
        MainMap.Pins.Clear();
        _pinMap.Clear();
        PoiCard.IsVisible = false;
        PremiumSheet.IsVisible = false;

        var hasActivePass = _vm.AccessStatus?.HasActivePass ?? false;

        foreach (var poi in pois)
        {
            // Requirement 14.6: ẩn lock icon khi có Access Pass còn hạn
            var showLock = poi.IsPremium && !hasActivePass;
            var pin = new Pin
            {
                Label = showLock ? $"🔒 {poi.Name}" : poi.Name,
                Address = poi.Category,
                Location = new Location(poi.Latitude, poi.Longitude),
                Type = PinType.Place
            };
            _pinMap[pin] = poi;
            MainMap.Pins.Add(pin);
        }
    }

    // Tap vào bản đồ → tìm pin gần nhất trong 40m
    private void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        const double thresholdDeg = 0.0004; // ~40m
        Pin? nearest = null;
        double minDist = double.MaxValue;

        foreach (var pin in _pinMap.Keys)
        {
            var dLat = pin.Location.Latitude - e.Location.Latitude;
            var dLon = pin.Location.Longitude - e.Location.Longitude;
            var dist = dLat * dLat + dLon * dLon;
            if (dist < minDist)
            {
                minDist = dist;
                nearest = pin;
            }
        }

        if (nearest is not null && minDist < thresholdDeg * thresholdDeg && _pinMap.TryGetValue(nearest, out var poi))
            HandlePoiTap(poi);
        else
        {
            PoiCard.IsVisible = false;
            PremiumSheet.IsVisible = false;
        }
    }

    // Requirement 14.3 & 14.4: xử lý tap vào POI — kiểm tra premium
    private void HandlePoiTap(PoiRecord poi)
    {
        var hasActivePass = _vm.AccessStatus?.HasActivePass ?? false;

        // Requirement 14.4: POI premium + không có pass → hiển thị bottom sheet mua pass
        if (poi.IsPremium && !hasActivePass)
        {
            ShowPremiumSheet(poi);
        }
        else
        {
            ShowPoiCard(poi);
        }
    }

    // Requirement 14.4: bottom sheet với thông tin POI và nút mua Access Pass
    private void ShowPremiumSheet(PoiRecord poi)
    {
        _currentPoiCard = poi;
        PremiumPoiNameLabel.Text = poi.Name;
        PremiumPoiDescLabel.Text = poi.Description;

        // Requirement 4.5: hiển thị thông báo free trial trong bottom sheet
        var status = _vm.AccessStatus;
        if (status is not null && !status.HasActivePass && status.FreeTrialUsed < status.FreeTrialLimit)
        {
            PremiumFreeTrialLabel.Text = $"Bạn đã dùng {status.FreeTrialUsed}/{status.FreeTrialLimit} lượt thử miễn phí";
            PremiumFreeTrialLabel.IsVisible = true;
        }
        else
        {
            PremiumFreeTrialLabel.IsVisible = false;
        }

        PoiCard.IsVisible = false;
        PremiumSheet.IsVisible = true;
    }

    private void ShowPoiCard(PoiRecord poi)
    {
        _currentPoiCard = poi;
        PoiNameLabel.Text = poi.Name;
        PoiDescLabel.Text = poi.Description;
        PoiCategoryLabel.Text = $"🍜 {poi.Category}";

        if (!string.IsNullOrWhiteSpace(poi.ImagePath))
        {
            PoiImage.IsVisible = true;
            PoiImage.Source = CreatePoiImageSource(poi.ImagePath);
        }
        else
        {
            PoiImage.IsVisible = false;
        }

        PremiumSheet.IsVisible = false;
        PoiCard.IsVisible = true;
    }

    // Requirement 4.5: cập nhật banner free trial ở dưới màn hình
    private void UpdateFreeTrialBanner()
    {
        var msg = _vm.FreeTrialMessage;
        if (!string.IsNullOrWhiteSpace(msg))
        {
            FreeTrialLabel.Text = msg;
            FreeTrialBanner.IsVisible = true;
        }
        else
        {
            FreeTrialBanner.IsVisible = false;
        }
    }

    private async void OnListenClicked(object? sender, EventArgs e)
    {
        if (_currentPoiCard is null) return;
        await _narration.EnqueueAsync(_currentPoiCard);
    }

    private void OnClosePoiCard(object? sender, EventArgs e) => PoiCard.IsVisible = false;

    private void OnClosePremiumSheet(object? sender, EventArgs e) => PremiumSheet.IsVisible = false;

    // Requirement 4.6: điều hướng đến màn hình mua Access Pass
    private async void OnBuyAccessPassClicked(object? sender, EventArgs e)
    {
        PremiumSheet.IsVisible = false;
        // Hiển thị thông báo hướng dẫn mua Access Pass
        // (tích hợp với PaymentController khi có UI thanh toán)
        await DisplayAlert(
            "Mua Access Pass",
            "Mua Access Pass với giá $1/7 ngày để nghe thuyết minh không giới hạn tại Phố Vĩnh Khánh.\n\nTính năng thanh toán sẽ sớm được cập nhật.",
            "OK");
    }

    private static ImageSource CreatePoiImageSource(string imagePath)
    {
        if (Uri.TryCreate(imagePath, UriKind.Absolute, out var absoluteUri))
        {
            if (DeviceInfo.Current.Platform == DevicePlatform.Android &&
                (string.Equals(absoluteUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(absoluteUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(absoluteUri.Host, "::1", StringComparison.OrdinalIgnoreCase)))
            {
                absoluteUri = new UriBuilder(absoluteUri)
                {
                    Host = "10.0.2.2"
                }.Uri;
            }

            return ImageSource.FromUri(absoluteUri);
        }

        return ImageSource.FromFile(imagePath);
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        _currentRadiusMeters = Math.Max(50, _currentRadiusMeters / 1.8);
        MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            MainMap.VisibleRegion?.Center ?? new Location(10.7580, 106.7020),
            Distance.FromMeters(_currentRadiusMeters)));
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        _currentRadiusMeters = Math.Min(5000, _currentRadiusMeters * 1.8);
        MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            MainMap.VisibleRegion?.Center ?? new Location(10.7580, 106.7020),
            Distance.FromMeters(_currentRadiusMeters)));
    }

    private async void OnMyLocationClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = await Geolocation.GetLastKnownLocationAsync()
                ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));

            if (location is not null)
                MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                    new Location(location.Latitude, location.Longitude),
                    Distance.FromMeters(_currentRadiusMeters)));
        }
        catch
        {
            CenterOnVinhKhanh();
        }
    }

    private void CenterOnVinhKhanh()
    {
        MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(10.7580, 106.7020),
            Distance.FromMeters(_currentRadiusMeters)));
    }
}

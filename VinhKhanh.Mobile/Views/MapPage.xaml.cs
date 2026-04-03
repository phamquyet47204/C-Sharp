using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using System.ComponentModel;
using VinhKhanh.Mobile.Models;
using VinhKhanh.Mobile.ViewModels;

namespace VinhKhanh.Mobile.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;
    private double _currentRadiusMeters = 300;
    private readonly Dictionary<Pin, PoiRecord> _pinMap = [];

    public MapPage(MapViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
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
    }

    private void PlacePins(IEnumerable<PoiRecord> pois)
    {
        MainMap.Pins.Clear();
        _pinMap.Clear();
        PoiCard.IsVisible = false;

        foreach (var poi in pois)
        {
            var pin = new Pin
            {
                Label = poi.Name,
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
            ShowPoiCard(poi);
        else
            PoiCard.IsVisible = false;
    }

    private void ShowPoiCard(PoiRecord poi)
    {
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

        PoiCard.IsVisible = true;
    }

    private void OnClosePoiCard(object? sender, EventArgs e) => PoiCard.IsVisible = false;

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

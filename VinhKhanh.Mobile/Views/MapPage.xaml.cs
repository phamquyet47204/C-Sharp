using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using VinhKhanh.Mobile.ViewModels;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Views;

public partial class MapPage : ContentPage
{
    private readonly MapViewModel _vm;

    public MapPage(MapViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
        PlacePins(_vm.Pois);
        CenterOnVinhKhanh();
    }

    private void PlacePins(IEnumerable<Poi> pois)
    {
        MainMap.Pins.Clear();
        foreach (var poi in pois)
        {
            MainMap.Pins.Add(new Pin
            {
                Label = poi.Name,
                Address = poi.Description[..Math.Min(60, poi.Description.Length)],
                Location = new Location(poi.Latitude, poi.Longitude)
            });
        }
    }

    private void CenterOnVinhKhanh()
    {
        // Approximate center of Vinh Khanh street, District 4, HCMC
        MainMap.MoveToRegion(MapSpan.FromCenterAndRadius(
            new Location(10.7580, 106.7020),
            Distance.FromMeters(300)));
    }
}

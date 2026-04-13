using VinhKhanh.Mobile.Models;
using VinhKhanh.Mobile.ViewModels;

namespace VinhKhanh.Mobile.Views;

public partial class PoiListPage : ContentPage
{
	private readonly PoiListViewModel _vm;

	public PoiListPage(PoiListViewModel vm)
	{
		InitializeComponent();
		BindingContext = _vm = vm;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.LoadCommand.ExecuteAsync(null);
	}

	private async void OnPoiSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is PoiRecord poi)
		{
			// Reset selection
			((CollectionView)sender!).SelectedItem = null;

			// Navigate back and pass the selected POI
			// We'll use a simple trick: store the selected POI in a shared location or notify the Map
			// For now, let's use the Navigation stack to find MapPage or use a static property (simpler for this demo)
			SelectedPoiHelper.CurrentSelectedPoi = poi;
			await Navigation.PopAsync();
		}
	}
}

public static class SelectedPoiHelper
{
	public static PoiRecord? CurrentSelectedPoi { get; set; }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using Microsoft.Maui.Controls.Shapes;
using VinhKhanhFoodStreet.Configuration;
using VinhKhanhFoodStreet.Models;
using VinhKhanhFoodStreet.Services;
using VinhKhanhFoodStreet.ViewModels;

namespace VinhKhanhFoodStreet;

/// <summary>
/// MainPage - Modern Clean Design with MVVM Pattern
/// Senior MAUI Implementation with optimized performance
/// </summary>
public partial class MainPage : ContentPage
{
	private const double VinhKhanhCenterLat = 10.757600;
	private const double VinhKhanhCenterLng = 106.674800;
	private const int GeoFenceRadiusMeters = 200;

	private readonly IGeofenceEngine _geofenceEngine;
	private readonly ILocationService _locationService;
	private readonly INarrationService _narrationService;
	private readonly IDatabaseService _databaseService;
	private readonly IAppLanguageService _appLanguageService;
	private readonly IServiceProvider _serviceProvider;
	private readonly AccessService _accessService;
	private readonly AnalyticsService _analyticsService;
	private readonly MainPageViewModel _viewModel;
	private readonly HttpClient _apiHttpClient;
	
	private ObservableCollection<POI> _displayItems = new();
	private ObservableCollection<POIGroup> _groupedDisplayItems = new();
	private ObservableCollection<POI> _mapSearchResults = new();
	private Dictionary<string, Pin> _poiPins = new();
	private List<POI> _allPois = new();
	private Dictionary<string, List<POI>> _poiVariantsByGroup = new();
	private static readonly (string Code, string TextKey)[] CategoryOptions =
	[
		("ALL", "CategoryAll"),
		("FOOD_SNAIL", "CategorySnail"),
		("FOOD_BBQ", "CategoryBbq"),
		("FOOD_STREET", "CategoryStreet"),
		("PHOTO_SPOT", "CategoryPhotoSpot"),
		("DRINK", "CategoryDrink"),
		("UTILITY", "CategoryUtility")
	];
	private Location? _currentLocation;
	private bool _eventsAttached;
	private string _currentLanguage = "vi";
	private string _mapCategoryFilter = "ALL";
	private string _listCategoryFilter = "ALL";
	private string _currentSearchText = string.Empty;
	private bool _isUpdatingCategoryPicker;
	private bool _isSearchExpanded;
	private bool _isListTabActive;
	private bool _isSystemLanguageInitialized;
	private bool _isAppVisible;

	private string T(string key)
	{
		return _appLanguageService.T(key, _currentLanguage);
	}

	public MainPage(
		IGeofenceEngine geofenceEngine,
		ILocationService locationService,
		INarrationService narrationService,
		IDatabaseService databaseService,
		IAppLanguageService appLanguageService,
		IServiceProvider serviceProvider,
		AccessService accessService,
		AnalyticsService analyticsService)
	{
		InitializeComponent();
		_geofenceEngine = geofenceEngine;
		_locationService = locationService;
		_narrationService = narrationService;
		_databaseService = databaseService;
		_appLanguageService = appLanguageService;
		_serviceProvider = serviceProvider;
		_accessService = accessService;
		_analyticsService = analyticsService;
		_apiHttpClient = new HttpClient(); // No longer needs BaseAddress here for logs

		
		NavigationPage.SetHasNavigationBar(this, false);
		_viewModel = new MainPageViewModel();
		_narrationService.RegisterMediaElement(NarrationPlayer);
		_currentLanguage = _appLanguageService.GetEffectiveLanguage();
		
		// Gan BindingContext theo MVVM de UI doc du lieu tu ViewModel.
		BindingContext = _viewModel;
		_displayItems = _viewModel.DisplayPois;
		PoiCollectionView.ItemsSource = _groupedDisplayItems;
		MapSearchResultsList.ItemsSource = _mapSearchResults;
		SearchContainer.IsVisible = false;
		InitializeCategoryDropList();
		ApplyLanguageUi();
		ResetPoiUiState();
		SetActiveTab(false);
	}

	private async void OnSettingsClicked(object? sender, EventArgs e)
	{
		try
		{
			var settingsPage = _serviceProvider.GetRequiredService<SettingsPage>();
			await Navigation.PushAsync(settingsPage);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi navigation: {ex.Message}");
			await DisplayAlert("Lỗi", "Không thể mở trang cài đặt.", "OK");
		}
	}

	protected override async void OnAppearing()
	{
		_isAppVisible = true;
		base.OnAppearing();
		AttachEventsIfNeeded();
		MoveCameraToVinhKhanh();

		try
		{
			InitializeLanguageFromSystemIfNeeded();
			
			// Sync trial status with server
			_ = _accessService.SyncTrialStatusAsync();

			await _databaseService.SyncPoisFromServerAsync();
			await LoadMapPinsAndListAsync(reloadFromDatabase: true);

			var canUseLocation = await EnsureLocationReadyAsync();
			if (!canUseLocation)
				return;

			// Khoi dong geofence engine (bao gom location service) sau khi co quyen vi tri.
			try
			{
				await _geofenceEngine.StartAsync(_currentLanguage);
			}
			catch (Exception geoEx)
			{
				Debug.WriteLine($"[MainPage] Loi khoi dong geofence: {geoEx.Message}");
			}

			LocationStatusLabel.Text = T("GpsTracking");

			// Check if language changed from SettingsPage
			var newLang = _appLanguageService.GetEffectiveLanguage();
			if (newLang != _currentLanguage)
			{
				_currentLanguage = newLang;
				ApplyLanguageUi();
				await LoadMapPinsAndListAsync(reloadFromDatabase: true);
				await _geofenceEngine.SetLanguageAsync(_currentLanguage);
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi: {ex.Message}");
			LocationStatusLabel.Text = T("GpsStartFailed");
		}
	}

	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		try
		{
			DetachEventsIfNeeded();
			await _geofenceEngine.StopAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi OnDisappearing: {ex.Message}");
		}
	}

	private async Task LoadMapPinsAndListAsync(bool reloadFromDatabase = false)
	{
		// Đảm bảo DB đã khởi tạo xong trước khi nạp dữ liệu (Tránh lỗi 'database is locked' trên Emulator)
		await _databaseService.InitializeAsync();

		if (reloadFromDatabase || _poiVariantsByGroup.Count == 0)
		{
			await EnsurePoiCacheLoadedAsync();
		}

		try
		{
			_allPois = await _databaseService.GetLocalizedPoisAsync(_currentLanguage);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi lay POI localize: {ex.Message}");
			ApplyLocalizedPoisFromCache();
		}

		Debug.WriteLine($"[MainPage] Loaded {_allPois.Count} POIs for language {_currentLanguage}");

		if (_poiPins.Count == 0 || reloadFromDatabase)
		{
			await RebuildMapPinsAsync();
		}
		else
		{
			await RefreshMapPinTextsAsync();
		}

		await RefreshCollectionViewAsync();
	}

	private async Task RebuildMapPinsAsync()
	{
		PoiMap.Pins.Clear();
		_poiPins.Clear();
		var filteredPois = GetPoisFilteredForView(isMap: true);

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			foreach (var poi in filteredPois)
			{
				var aggregateId = GetAggregateId(poi);
				// Add to map
				var pin = new Pin
				{
					Label = poi.Name,
					Address = string.Empty,
					Location = new Location(poi.Latitude, poi.Longitude),
					Type = PinType.Place
				};
				pin.InfoWindowClicked += (s, e) =>
				{
					var latestPoi = _allPois.FirstOrDefault(x => GetAggregateId(x) == aggregateId);
					if (latestPoi is not null)
					{
						OnPinClicked(latestPoi);
					}
				};
				PoiMap.Pins.Add(pin);
				_poiPins[aggregateId] = pin;
			}
		});
	}

	private async Task RefreshMapPinTextsAsync()
	{
		var filteredPois = GetPoisFilteredForView(isMap: true);
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			PoiMap.Pins.Clear();
			_poiPins.Clear();
			foreach (var poi in filteredPois)
			{
				var aggregateId = GetAggregateId(poi);
				var pin = new Pin
				{
					Label = poi.Name,
					Address = string.Empty,
					Location = new Location(poi.Latitude, poi.Longitude),
					Type = PinType.Place
				};
				pin.InfoWindowClicked += (s, e) => OnPinClicked(poi);
				PoiMap.Pins.Add(pin);
				_poiPins[aggregateId] = pin;
			}
		});
	}

	private async Task EnsurePoiCacheLoadedAsync()
	{
		try
		{
			var allPois = await _databaseService.GetAllPoisAsync();
			_poiVariantsByGroup = allPois
				.GroupBy(GetAggregateId)
				.ToDictionary(g => g.Key, g => g.ToList());

			Debug.WriteLine($"[MainPage] Loaded all POI variants: {allPois.Count}, groups: {_poiVariantsByGroup.Count}");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi load toan bo POI: {ex.Message}");
			_poiVariantsByGroup = new Dictionary<string, List<POI>>();
		}
	}

	private void ApplyLocalizedPoisFromCache()
	{
		_allPois = _poiVariantsByGroup
			.Values
			.Select(variants => SelectLocalizedPoi(variants, _currentLanguage))
			.Where(p => p is not null)
			.Cast<POI>()
			.OrderByDescending(p => p.Priority)
			.ToList();
	}

	private POI? SelectLocalizedPoi(IReadOnlyList<POI> variants, string languageCode)
	{
		var fallbackChain = _appLanguageService.GetLanguageFallbackChain(languageCode);

		foreach (var candidateLanguage in fallbackChain)
		{
			var match = variants.FirstOrDefault(p =>
				string.Equals(NormalizeLanguage(p.LanguageCode), NormalizeLanguage(candidateLanguage), StringComparison.OrdinalIgnoreCase));

			if (match is not null)
			{
				return match;
			}
		}

		return variants
			.OrderByDescending(p => p.Priority)
			.FirstOrDefault();
	}

	private static string GetAggregateId(POI poi)
	{
		if (!string.IsNullOrEmpty(poi.BasePoiId))
		{
			return poi.BasePoiId;
		}

		return poi.Id.ToString();
	}

	private static string NormalizeLanguage(string? languageCode)
	{
		if (string.IsNullOrWhiteSpace(languageCode))
		{
			return "vi";
		}

		var normalized = languageCode.Trim().ToLowerInvariant();
		if (normalized == "jp")
		{
			return "ja";
		}

		return normalized.Split('-')[0];
	}

	private async Task HighlightNearestPoiAsync()
	{
		if (_currentLocation == null || _allPois.Count == 0)
			return;

		var nearest = _allPois.OrderBy(p => 
			CalculateDistance(_currentLocation.Latitude, _currentLocation.Longitude, p.Latitude, p.Longitude)
		).FirstOrDefault();

		if (nearest == null)
			return;

		await MainThread.InvokeOnMainThreadAsync(async () =>
		{
			var nearestAggregateId = GetAggregateId(nearest);

			// Update distances for all items
			foreach (var item in _displayItems)
			{
				item.Distance = (int)CalculateDistance(
					_currentLocation.Latitude, _currentLocation.Longitude, 
					item.Latitude, item.Longitude);
				item.IsNearest = (GetAggregateId(item) == nearestAggregateId);
			}

			// Scroll to nearest
			var nearestPoi = _displayItems.FirstOrDefault(x => GetAggregateId(x) == nearestAggregateId);
			if (nearestPoi != null)
			{
				PoiCollectionView.ScrollTo(nearestPoi);
			}
		});
	}

	private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
	{
		const double R = 6371000; // Earth radius in meters
		var dLat = ToRadians(lat2 - lat1);
		var dLon = ToRadians(lon2 - lon1);
		var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
				Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
				Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
		var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		return R * c;
	}

	private double ToRadians(double degrees) => degrees * Math.PI / 180;

	private void HandleLocationChanged(Location location)
	{
		_currentLocation = location;
		_ = MainThread.InvokeOnMainThreadAsync(() =>
		{
			LocationLabel.Text = $"{location.Latitude:F6}, {location.Longitude:F6}";
			LocationLabel.IsVisible = true;
			LocationStatusLabel.Text = $"{T("LocationUpdated")}: {DateTime.Now:HH:mm:ss}";
		});
		
		_ = HighlightNearestPoiAsync();
	}

	private void HandlePoiEntered(POI poi)
	{
		_ = MainThread.InvokeOnMainThreadAsync(async () =>
		{
			try
			{
				_geofenceEngine.MarkPoiAsPlayed(poi.Id);
				_ = _analyticsService.TrackActivityAsync(poi.Latitude, poi.Longitude, "visit", poi.Id);
				
				if (!string.IsNullOrWhiteSpace(poi.AudioPath))
				{
					try
					{
						_ = _analyticsService.TrackActivityAsync(poi.Latitude, poi.Longitude, "narration", poi.Id);
						await _narrationService.PlayAudioAsync(poi.AudioPath);
						return;
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"[MainPage] Loi phat audio: {ex.Message}");
					}
				}

				var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
				_ = _analyticsService.TrackActivityAsync(poi.Latitude, poi.Longitude, "narration", poi.Id);
				await _narrationService.SpeakAsync(text, _currentLanguage);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[MainPage] Loi geofence event: {ex.Message}");
			}
		});
	}

	private void InitializeCategoryDropList()
	{
		CategoryDropList.Children.Clear();
		foreach (var option in CategoryOptions)
		{
			var row = CreateCategoryDropRow(option.Code, T(option.TextKey));
			CategoryDropList.Children.Add(row);
		}
		UpdateMapCategoryListUi();
	}

	private void InitializeListCategoryChips()
	{
		ListCategoryChips.Children.Clear();

		// Add "ALL" chip
		var allChip = CreateCategoryChip("ALL", T("CategoryAll"));
		ListCategoryChips.Children.Add(allChip);

		// Add other categories
		foreach (var opt in CategoryOptions)
		{
			if (opt.Code == "ALL") continue;
			var chip = CreateCategoryChip(opt.Code, T(opt.TextKey));
			ListCategoryChips.Children.Add(chip);
		}
		
		UpdateListCategoryChipsSelection();
	}

	private View CreateCategoryChip(string code, string label)
	{
		var border = new Border
		{
			StrokeShape = new RoundRectangle { CornerRadius = 18 },
			StrokeThickness = 0,
			Padding = new Thickness(12, 4),
			Margin = new Thickness(0, 4),
			BackgroundColor = Color.FromArgb("#F3F4F6")
		};

		var stack = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		
		if (code != "ALL")
		{
			stack.Children.Add(new Label { Text = GetCategoryIcon(code), FontSize = 14, VerticalOptions = LayoutOptions.Center });
		}
		
		stack.Children.Add(new Label { Text = label, FontSize = 12, TextColor = Color.FromArgb("#4B5563"), VerticalOptions = LayoutOptions.Center });
		
		border.Content = stack;
		border.BindingContext = code;

		var tap = new TapGestureRecognizer();
		tap.Tapped += OnListCategoryChipTapped;
		border.GestureRecognizers.Add(tap);

		return border;
	}

	private async void OnListCategoryChipTapped(object? sender, TappedEventArgs e)
	{
		if (sender is Border border && border.BindingContext is string code)
		{
			if (_listCategoryFilter == code) return;

			_listCategoryFilter = code;
			UpdateListCategoryChipsSelection();
			await RefreshCollectionViewAsync();
		}
	}

	private void UpdateListCategoryChipsSelection()
	{
		foreach (var child in ListCategoryChips.Children)
		{
			if (child is Border border && border.BindingContext is string code)
			{
				bool isSelected = (code == _listCategoryFilter);
				border.BackgroundColor = isSelected ? Color.FromArgb("#FF7F50") : Color.FromArgb("#F3F4F6");
				
				if (border.Content is HorizontalStackLayout stack)
				{
					foreach (var label in stack.Children.OfType<Label>())
					{
						label.TextColor = isSelected ? Colors.White : Color.FromArgb("#4B5563");
					}
				}
			}
		}
	}

	private string GetCategoryIcon(string code)
	{
		return code switch
		{
			"ALL" => "🏠",
			"FOOD_SNAIL" => "🐚",
			"FOOD_BBQ" => "🔥",
			"FOOD_STREET" => "🍢",
			"PHOTO_SPOT" => "📸",
			"DRINK" => "🥤",
			"UTILITY" => "🚻",
			_ => "📍"
		};
	}

	private Border CreateCategoryDropRow(string code, string text)
	{
		var icon = GetCategoryIcon(code);
		
		var grid = new Grid
		{
			ColumnDefinitions = new ColumnDefinitionCollection { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
			Padding = new Thickness(12, 10)
		};

		var iconLabel = new Label { Text = icon, FontSize = 18, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(0,0,12,0) };
		var textLabel = new Label { Text = text, FontSize = 14, VerticalOptions = LayoutOptions.Center, TextColor = Color.FromArgb("#1A1A1A") };

		grid.Add(iconLabel, 0);
		grid.Add(textLabel, 1);

		var border = new Border
		{
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
			StrokeThickness = 0,
			BackgroundColor = Color.FromArgb("#00000000"), // Transparent
			Content = grid,
			BindingContext = code
		};

		var tap = new TapGestureRecognizer();
		tap.Tapped += OnCategoryMapRowTapped;
		border.GestureRecognizers.Add(tap);

		return border;
	}

	private void UpdateMapCategoryListUi()
	{
		foreach (var view in CategoryDropList.Children)
		{
			if (view is Border border && border.BindingContext is string code)
			{
				var isSelected = (code == _mapCategoryFilter);
				border.BackgroundColor = isSelected ? Color.FromArgb("#FFF0EB") : Color.FromArgb("#00000000"); // Light coral for selected
				
				if (border.Content is Grid grid && grid.Children[1] is Label label)
				{
					label.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
					label.TextColor = isSelected ? Color.FromArgb("#FF7F50") : Color.FromArgb("#1A1A1A");
				}
			}
		}
	}

	private async void OnCategoryMapRowTapped(object? sender, EventArgs e)
	{
		if (sender is Border border && border.BindingContext is string code)
		{
			_mapCategoryFilter = code;
			UpdateMapCategoryListUi();
			AdvancedFilterSection.IsVisible = false; // Auto-close
			AdvancedFilterButton.TextColor = Color.FromArgb("#666666");
			
			await RefreshMapPinTextsAsync();
			// Note: We do NOT refresh CollectionView here to keep the POI List independent
		}
	}

	private void OnToggleAdvancedFilter(object? sender, EventArgs e)
	{
		AdvancedFilterSection.IsVisible = !AdvancedFilterSection.IsVisible;
		AdvancedFilterButton.TextColor = AdvancedFilterSection.IsVisible ? Color.FromArgb("#FF7F50") : Color.FromArgb("#666666");
		
		if (AdvancedFilterSection.IsVisible)
		{
			MapSearchResultsContainer.IsVisible = false;
		}
	}

	private void HandlePoiExited(POI poi) { }


	private void OnCategoryPickerChanged(object? sender, EventArgs e)
	{
		// Method kept for compatibility or removed if not referenced.
	}

	private void OnMapSearchTextChanged(object? sender, TextChangedEventArgs e)
	{
		_currentSearchText = e.NewTextValue ?? string.Empty;
		ApplySearchFilter(_currentSearchText);
	}

	private void OnMapSearchPressed(object? sender, EventArgs e)
	{
		_currentSearchText = MapSearchBar.Text ?? string.Empty;
		ApplySearchFilter(_currentSearchText);
	}

	private void ApplySearchFilter(string? query)
	{
		_currentSearchText = query ?? string.Empty;
		_mapSearchResults.Clear();
		
		if (string.IsNullOrWhiteSpace(_currentSearchText))
		{
			MapSearchResultsContainer.IsVisible = false;
			_ = RebuildMapPinsAsync();
			return;
		}

		var searchText = _currentSearchText.ToLowerInvariant();
		
		// Search is now GLOBAL (ignoring category filter) for a better UX, similar to Google Maps
		var filtered = _allPois
			.Where(p => (p.Name?.ToLowerInvariant().Contains(searchText) == true) || 
						(p.Description?.ToLowerInvariant().Contains(searchText) == true));

		// Sort by distance if available
		if (_currentLocation != null)
		{
			foreach (var poi in filtered)
			{
				poi.Distance = (int)CalculateDistance(_currentLocation.Latitude, _currentLocation.Longitude, poi.Latitude, poi.Longitude);
			}
			filtered = filtered.OrderBy(p => p.Distance);
		}

		var resultList = filtered.ToList();
		
		// Map Pin Update
		_ = UpdateMapPinsInPlace(resultList);

		// Overlay Update
		if (resultList.Count > 0)
		{
			foreach (var poi in resultList.Take(10)) // Limit to top 10 for performance
			{
				_mapSearchResults.Add(CreateDisplayPoi(poi));
			}
			MapSearchResultsContainer.IsVisible = true;
			AdvancedFilterSection.IsVisible = false;
		}
		else
		{
			MapSearchResultsContainer.IsVisible = false;
		}
	}

	private async void OnMapSearchResultSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is POI selectedPoi)
		{
			// Clear selection to allow re-selecting the same item
			MapSearchResultsList.SelectedItem = null;
			MapSearchResultsContainer.IsVisible = false;

			var location = new Location(selectedPoi.Latitude, selectedPoi.Longitude);
			PoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(200)));
			
			// Find the actual localized POI to show in card
			var poi = _allPois.FirstOrDefault(p => GetAggregateId(p) == GetAggregateId(selectedPoi));
			if (poi != null)
			{
				OnPinClicked(poi);
			}
		}
	}

	private async void OnSearchPoi(object? sender, EventArgs e)
	{
		_currentSearchText = SearchBarPoi.Text?.Trim() ?? string.Empty;
		await RefreshCollectionViewAsync();
		
		if (!string.IsNullOrWhiteSpace(_currentSearchText))
		{
			// Optional: Sync map pins with list search if desired
			// _ = UpdateMapPinsInPlace(GetPoisFilteredForView(isMap: true));
		}
	}

	private async Task UpdateMapPinsInPlace(List<POI> filteredPois)
	{
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			PoiMap.Pins.Clear();
			_poiPins.Clear();
			foreach (var poi in filteredPois)
			{
				var aggregateId = GetAggregateId(poi);
				var pin = new Pin
				{
					Label = poi.Name,
					Location = new Location(poi.Latitude, poi.Longitude),
					Type = PinType.Place
				};
				pin.InfoWindowClicked += (s, e) => OnPinClicked(poi);
				PoiMap.Pins.Add(pin);
				_poiPins[aggregateId] = pin;
			}
		});
	}

	/// <summary>
	/// Refresh POI collection with current filter
	/// </summary>
	private async Task RefreshCollectionViewAsync()
	{
		_displayItems.Clear();
		_groupedDisplayItems.Clear();
		
		var filteredPois = GetPoisFilteredForView(isMap: false);
		var displayPois = filteredPois.Select(CreateDisplayPoi).ToList();
		
		foreach (var poi in displayPois)
		{
			_displayItems.Add(poi);
		}

		// Grouping vs Flat List
		if (!string.IsNullOrWhiteSpace(_currentSearchText))
		{
			// When searching, show a flat list to prioritize global distance sorting
			var resultsGroupName = T("SearchResults") ?? "Kết quả tìm kiếm";
			_groupedDisplayItems.Add(new POIGroup(resultsGroupName, resultsGroupName, displayPois));
		}
		else
		{
			// Group by category for the standard list view
			var groups = displayPois
				.GroupBy(p => p.CategoryDisplayName ?? T("CategoryStreet"))
				.Select(g => new POIGroup(g.Key, g.Key, g.OrderBy(p => p.Distance)))
				.OrderBy(g => g.GroupDisplayName)
				.ToList();

			foreach (var group in groups)
			{
				_groupedDisplayItems.Add(group);
			}
		}

		PoisCountLabel.Text = $"({_displayItems.Count})";
		await HighlightNearestPoiAsync();
	}

	private POI? _selectedPinPoi;
	private int? _pendingPinCardRatingStars;

	private void OnPinClicked(POI poi)
	{
		Debug.WriteLine($"[MainPage] Pin clicked: {poi.Name}");
		_selectedPinPoi = poi;
		MainThread.BeginInvokeOnMainThread(() =>
		{
			PinCardNameLabel.Text = poi.Name;
			PinCardDescriptionLabel.Text = poi.Description;
			PinCardCategoryLabel.Text = poi.CategoryDisplayName;
			PinCardRatingLabel.Text = "★ -- (0 đánh giá)";
			SetPinCardUserStars(null);
			_pendingPinCardRatingStars = null;
			UpdatePinCardRateButtonState();
			PinCardDistanceLabel.Text = poi.Distance > 0 ? $"{poi.Distance}m" : string.Empty;
			PinCardImage.Source = poi.PoiImageSource;
			PinQuickCard.IsVisible = true;
		});

		_ = LoadRatingSummaryAsync(poi);
	}

	private async void OnPinCardPlay(object? sender, EventArgs e)
	{
		if (_selectedPinPoi is null) return;
		var poi = _selectedPinPoi;
		await _analyticsService.TrackActivityAsync(poi.Latitude, poi.Longitude, "narration", poi.Id);

		SetNarratingPoi(poi);

		try
		{
			if (!string.IsNullOrWhiteSpace(poi.AudioPath))
			{
				try 
				{ 
					await _narrationService.PlayAudioAsync(poi.AudioPath); 
					// Reset playing state after a reasonable time if we can't detect end event
					_ = Task.Delay(5000).ContinueWith(_ => SetNarratingPoi(null));
					return; 
				}
				catch (Exception ex) { Debug.WriteLine($"[MainPage] Audio fallback TTS: {ex.Message}"); }
			}
			var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
			await _narrationService.SpeakAsync(text, _currentLanguage);
			_ = Task.Delay(5000).ContinueWith(_ => SetNarratingPoi(null));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi phat TTS: {ex.Message}");
		}
	}

	private async void OnPinCardNavigate(object? sender, EventArgs e)
	{
		if (_selectedPinPoi is null) return;
		var poi = _selectedPinPoi;
		
		try
		{
			var location = new Location(poi.Latitude, poi.Longitude);
			var options = new MapLaunchOptions 
			{ 
				Name = poi.Name,
				NavigationMode = NavigationMode.Driving 
			};

			await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(location, options);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi mo Google Map ngoai: {ex.Message}");
			await DisplayAlertAsync(T("ErrorTitle"), T("NavigateExternalFailed"), T("Ok"));
		}
	}

	private void OnPinCardClose(object? sender, EventArgs e)
	{
		PinQuickCard.IsVisible = false;
		_selectedPinPoi = null;
		_pendingPinCardRatingStars = null;
		UpdatePinCardRateButtonState();
	}

	private void OnPinCardStarClicked(object? sender, EventArgs e)
	{
		if (sender is not Button button || button.CommandParameter is null)
		{
			return;
		}

		if (!int.TryParse(button.CommandParameter.ToString(), out var stars) || stars is < 1 or > 5)
		{
			return;
		}

		_pendingPinCardRatingStars = stars;
		SetPinCardUserStars(stars);
		UpdatePinCardRateButtonState();
	}

	private async void OnPinCardRate(object? sender, EventArgs e)
	{
		if (_selectedPinPoi is null || !_pendingPinCardRatingStars.HasValue)
		{
			return;
		}

		await SubmitPoiRatingAsync(_pendingPinCardRatingStars.Value);
	}

	private async Task SubmitPoiRatingAsync(int stars)
	{
		if (_selectedPinPoi is null)
		{
			return;
		}

		_pendingPinCardRatingStars = stars;
		SetPinCardUserStars(stars);
		UpdatePinCardRateButtonState();

		var aggregateId = GetAggregateId(_selectedPinPoi);
		var request = new SubmitRatingRequest
		{
			Stars = stars,
			DeviceId = _accessService.DeviceId,
			Latitude = _currentLocation?.Latitude,
			Longitude = _currentLocation?.Longitude
		};

		try
		{
			var response = await _apiHttpClient.PostAsJsonAsync($"api/pois/{aggregateId}/ratings", request);
			if (response.IsSuccessStatusCode)
			{
				await LoadRatingSummaryAsync(_selectedPinPoi);
				return;
			}

			await DisplayAlertAsync("Lỗi", "Không thể gửi đánh giá lúc này.", "OK");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi danh gia: {ex.Message}");
			await DisplayAlertAsync("Lỗi", "Không thể kết nối máy chủ để gửi đánh giá.", "OK");
		}
	}

	private async Task LoadRatingSummaryAsync(POI poi)
	{
		try
		{
			var aggregateId = GetAggregateId(poi);
			var summary = await _apiHttpClient.GetFromJsonAsync<RatingSummaryResponse>(
				$"api/pois/{aggregateId}/ratings?deviceId={Uri.EscapeDataString(_accessService.DeviceId)}");

			if (summary is null)
			{
				return;
			}

			var ratingText = $"★ {summary.AverageStars:0.0} ({summary.RatingCount} đánh giá)";
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				if (_selectedPinPoi is null || GetAggregateId(_selectedPinPoi) != GetAggregateId(poi))
				{
					return;
				}

				PinCardRatingLabel.Text = ratingText;
				SetPinCardUserStars(summary.UserStars);
			});
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi tai tong quan danh gia: {ex.Message}");
		}
	}

	private void SetPinCardUserStars(int? stars)
	{
		var controls = new[] { PinCardStar1, PinCardStar2, PinCardStar3, PinCardStar4, PinCardStar5 };
		for (var i = 0; i < controls.Length; i++)
		{
			controls[i].Text = stars.HasValue && stars.Value >= i + 1 ? "★" : "☆";
		}
	}

	private void UpdatePinCardRateButtonState()
	{
		var hasSelection = _pendingPinCardRatingStars.HasValue;
		PinCardRateButton.IsEnabled = hasSelection;
		PinCardRateButton.Opacity = hasSelection ? 1 : 0.55;
		PinCardRateButton.BackgroundColor = hasSelection
			? Color.FromArgb("#FF7F50")
			: Color.FromArgb("#F6C8B3");
		PinCardRateButton.TextColor = hasSelection
			? Colors.White
			: Color.FromArgb("#FFF8F4");
	}



	private async void OnPlayPoi(object? sender, EventArgs e)
	{
		try
		{
			// Get PoiDisplayItem from button's binding context
			var button = sender as Button;
			if (button?.BindingContext is POI displayItem)
			{
				// Tim POI da localize theo aggregate id tu cache hien tai.
				var poi = _allPois.FirstOrDefault(p => GetAggregateId(p) == GetAggregateId(displayItem));
				if (poi is null)
				{
					return;
				}

				_ = _analyticsService.TrackActivityAsync(poi.Latitude, poi.Longitude, "narration", poi.Id);
					SetNarratingPoi(poi);

					if (!string.IsNullOrWhiteSpace(poi.AudioPath))
					{
						try
						{
							await _narrationService.PlayAudioAsync(poi.AudioPath);
							_ = Task.Delay(5000).ContinueWith(_ => SetNarratingPoi(null));
							return;
						}
						catch (Exception audioEx)
						{
							Debug.WriteLine($"[MainPage] Loi phat file audio, fallback TTS: {audioEx.Message}");
						}
					}
					
					var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
					await _narrationService.SpeakAsync(text, _currentLanguage);
					_ = Task.Delay(5000).ContinueWith(_ => SetNarratingPoi(null));
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi play audio: {ex.Message}");
			await DisplayAlertAsync(T("ErrorTitle"), $"{T("AudioPlayFailed")} {ex.Message}", T("Ok"));
		}
	}

	private void OnNavigatePoi(object? sender, EventArgs e)
	{
		try
		{
			// Get PoiDisplayItem from button's binding context
			var button = sender as Button;
			if (button?.BindingContext is POI displayItem)
			{
				// Tim POI da localize theo aggregate id tu cache hien tai.
				var poi = _allPois.FirstOrDefault(p => GetAggregateId(p) == GetAggregateId(displayItem));
				if (poi != null)
				{
					SetActiveTab(false);
					var location = new Location(poi.Latitude, poi.Longitude);
					PoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(100)));
					OnPinClicked(poi); // Automaticaly show detail card
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi navigate: {ex.Message}");
		}
	}

	/// <summary>
	/// Chi cap nhat text tren danh sach dang hien thi theo ngon ngu moi, khong load lai DB.
	/// </summary>
	private async Task RefreshDisplayItemTextsAsync()
	{
		try
		{
			await MainThread.InvokeOnMainThreadAsync(() =>
			{
				_viewModel.UpdateLocalizedTextsInPlace(_allPois);
				ApplyLocalizedActionTextsToDisplayItems();
			});
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi refresh text item: {ex.Message}");
		}
	}
	private void InitializeLanguageFromSystemIfNeeded()
	{
		if (_isSystemLanguageInitialized)
		{
			return;
		}

		_currentLanguage = _appLanguageService.GetEffectiveLanguage();
		_appLanguageService.SetPreferredLanguage(_currentLanguage);
		ApplyLanguageUi();
		_isSystemLanguageInitialized = true;

		Debug.WriteLine($"[MainPage] Initial language from system: {_currentLanguage}");
	}

	private void ApplyLanguageUi()
	{
		PageTitleLabel.Text = T("AppTitle");
		MapTabButton.Text = T("MapTab");
		ListTabButton.Text = T("ListTab");
		ListHeaderTitleLabel.Text = T("ListHeader");
		MapSearchBar.Placeholder = T("SearchPlaceholder");
		SearchBarPoi.Placeholder = T("SearchPlaceholder");
		
		InitializeCategoryDropList();
		InitializeListCategoryChips();
		ApplyLocalizedActionTextsToDisplayItems();
	}

	private void ApplyLocalizedActionTextsToDisplayItems()
	{
		var playText = T("Play");
		var navigateText = T("Navigate");

		foreach (var item in _displayItems)
		{
			item.PlayButtonText = playText;
			item.NavigateButtonText = navigateText;
		}
	}

	private void ResetPoiUiState()
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			_allPois.Clear();
			_poiPins.Clear();
			_poiVariantsByGroup.Clear();
			PoiMap.Pins.Clear();
			_viewModel.ReplaceDisplayPois(Array.Empty<POI>());
			PoisCountLabel.Text = "(0)";
		});
	}

	private void MoveCameraToVinhKhanh()
	{
		var initialRegion = MapSpan.FromCenterAndRadius(
			new Location(VinhKhanhCenterLat, VinhKhanhCenterLng),
			Distance.FromMeters(500));
		PoiMap.MoveToRegion(initialRegion);
	}

	private async Task<bool> EnsureLocationReadyAsync()
	{
		var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
		if (status != PermissionStatus.Granted)
		{
			status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
		}

		if (status != PermissionStatus.Granted)
		{
			LocationStatusLabel.Text = T("PermissionRequiredStatus");
			LocationLabel.Text = T("NoLocationPermission");
			LocationLabel.IsVisible = true;
			await DisplayAlertAsync(T("ErrorTitle"), T("LocationPermissionNeededMessage"), T("Ok"));
			AppInfo.Current.ShowSettingsUI();
			return false;
		}

		try
		{
			var loc = await Geolocation.Default.GetLocationAsync(
				new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(3)));
			if (loc != null)
			{
				_currentLocation = loc;
				LocationLabel.Text = $"{loc.Latitude:F6}, {loc.Longitude:F6}";
				LocationLabel.IsVisible = true;
				LocationStatusLabel.Text = T("CurrentLocationFetched");
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi GPS: {ex.Message}");
			LocationStatusLabel.Text = T("GpsNotReady");
			LocationLabel.Text = T("CannotGetCurrentLocationMessage");
			LocationLabel.IsVisible = true;
		}

		return true;
	}

	/// <summary>
	/// Dua camera ve vi tri hien tai cua nguoi dung, uu tien toa do moi nhat.
	/// </summary>
	private async void OnCenterToUserLocation(object? sender, EventArgs e)
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			}

			if (status != PermissionStatus.Granted)
			{
				await DisplayAlertAsync(T("ErrorTitle"), T("CenterNoPermissionMessage"), T("Ok"));
				return;
			}

			var location = await Geolocation.Default.GetLocationAsync(
				new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5)));

			if (location is null)
			{
				await DisplayAlertAsync(T("InfoTitle"), T("CannotGetCurrentLocationMessage"), T("Ok"));
				return;
			}

			_currentLocation = location;
			LocationLabel.Text = $"{location.Latitude:F6}, {location.Longitude:F6}";
			LocationLabel.IsVisible = true;
			LocationStatusLabel.Text = T("CenteredToYourLocation");
			PoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(
				new Location(location.Latitude, location.Longitude),
				Distance.FromMeters(250)));
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi center to user location: {ex.Message}");
			await DisplayAlertAsync(T("ErrorTitle"), T("CannotLocateCurrentPositionMessage"), T("Ok"));
		}
	}

	private async void OnToggleSearch(object? sender, EventArgs e)
	{
		_isSearchExpanded = !_isSearchExpanded;
		SearchContainer.IsVisible = _isSearchExpanded;
		CategoryListContainer.IsVisible = !_isSearchExpanded;
		SearchToggleButton.Text = _isSearchExpanded ? "✕" : "🔍";

		if (_isSearchExpanded)
		{
			await MainThread.InvokeOnMainThreadAsync(() => SearchBarPoi.Focus());
		}
		else
		{
			SearchBarPoi.Text = string.Empty;
			_currentSearchText = string.Empty;
			await RefreshCollectionViewAsync();
		}
	}

	private void OnSwitchToMapTab(object? sender, EventArgs e) => SetActiveTab(false);

	private void OnSwitchToListTab(object? sender, EventArgs e) => SetActiveTab(true);

	private void SetActiveTab(bool isListTab)
	{
		_isListTabActive = isListTab;
		MapPanel.IsVisible = !_isListTabActive;
		ListPanel.IsVisible = _isListTabActive;

		MapTabButton.BackgroundColor = _isListTabActive ? Colors.White : Color.FromArgb("#FF7F50");
		MapTabButton.TextColor = _isListTabActive ? Color.FromArgb("#666666") : Colors.White;

		ListTabButton.BackgroundColor = _isListTabActive ? Color.FromArgb("#FF7F50") : Colors.White;
		ListTabButton.TextColor = _isListTabActive ? Colors.White : Color.FromArgb("#666666");
	}

	private void AttachEventsIfNeeded()
	{
		if (_eventsAttached)
			return;

		_geofenceEngine.OnPoiEntered += HandlePoiEntered;
		_geofenceEngine.OnPoiExited += HandlePoiExited;
		_locationService.LocationChanged += HandleLocationChanged;
		_eventsAttached = true;
	}

	private void DetachEventsIfNeeded()
	{
		if (!_eventsAttached)
			return;

		_geofenceEngine.OnPoiEntered -= HandlePoiEntered;
		_geofenceEngine.OnPoiExited -= HandlePoiExited;
		_locationService.LocationChanged -= HandleLocationChanged;
		_eventsAttached = false;
	}

	private void SetNarratingPoi(POI? activePoi)
	{
		MainThread.BeginInvokeOnMainThread(() =>
		{
			foreach (var item in _displayItems)
			{
				item.IsPlaying = (activePoi != null && item.AggregateId == activePoi.AggregateId);
			}
		});
	}

	private sealed class SubmitRatingRequest
	{
		public int Stars { get; set; }
		public string DeviceId { get; set; } = string.Empty;
		public double? Latitude { get; set; }
		public double? Longitude { get; set; }
	}

	private sealed class RatingSummaryResponse
	{
		public int PoiId { get; set; }
		public double AverageStars { get; set; }
		public int RatingCount { get; set; }
		public int? UserStars { get; set; }
	}



	private void OnMapHandlerChanged(object? sender, EventArgs e)
	{
#if ANDROID
		if (PoiMap.Handler is Microsoft.Maui.Maps.Handlers.MapHandler mapHandler)
		{
			var nativeMap = mapHandler.PlatformView as Android.Gms.Maps.MapView;
			if (nativeMap != null)
			{
				nativeMap.GetMapAsync(new MapCallback(this));
			}
		}
#endif
	}

#if ANDROID
	private class MapCallback : Java.Lang.Object, Android.Gms.Maps.IOnMapReadyCallback
	{
		private readonly MainPage _page;
		public MapCallback(MainPage page) => _page = page;

		public void OnMapReady(Android.Gms.Maps.GoogleMap googleMap)
		{
			// Disable default My Location button as we have our own custom FAB in the bottom right
			googleMap.UiSettings.MyLocationButtonEnabled = false;

			// Wire up direct marker click to avoid double-tap requirement
			googleMap.MarkerClick += (s, e) =>
			{
				var marker = e.Marker;
				if (marker != null)
				{
					// Show info window (default behavior)
					marker.ShowInfoWindow();
					
					// Find the POI associated with this pin by name/position
					_page.OnMarkerTapped(marker.Title, marker.Position.Latitude, marker.Position.Longitude);
				}
				// Set Handled to true instead of returning true (C# event requirement)
				e.Handled = true; 
			};
		}
	}

	private void OnMarkerTapped(string? title, double lat, double lng)
	{
		// Find the POI in our cache that matches this location/name
		var poi = _allPois.FirstOrDefault(p => 
			(p.Name == title) && 
			Math.Abs(p.Latitude - lat) < 1e-5 && 
			Math.Abs(p.Longitude - lng) < 1e-5);
			
		if (poi != null)
		{
			OnPinClicked(poi);
		}
	}
#endif


	/// <summary>
	/// Tao doi tuong POI phuc vu hien thi UI, tach biet voi entity goc trong cache.
	/// </summary>
	private POI CreateDisplayPoi(POI source)
	{
		return new POI
		{
			Id = source.Id,
			BasePoiId = source.BasePoiId,
			Name = source.Name,
			Description = source.Description,
			Latitude = source.Latitude,
			Longitude = source.Longitude,
			ImagePath = source.ImagePath,
			AudioPath = source.AudioPath,
			LanguageCode = source.LanguageCode,
			Category = source.Category,
			Priority = source.Priority,
			Radius = source.Radius,
			Distance = _currentLocation != null
				? (int)CalculateDistance(_currentLocation.Latitude, _currentLocation.Longitude, source.Latitude, source.Longitude)
				: 0,
			Rating = 4.5f,
			IsNearest = false
			,
			PlayButtonText = T("Play"),
			NavigateButtonText = T("Navigate")
		};
	}


	/// <summary>
	/// Lay tap POI sau khi loc theo tim kiem hoac category (tuy theo view).
	/// </summary>
	private List<POI> GetPoisFilteredForView(bool isMap)
	{
		var result = _allPois.AsEnumerable();

		// Universal name search (affects both Map and List)
		if (!string.IsNullOrWhiteSpace(_currentSearchText))
		{
			var searchText = _currentSearchText.ToLowerInvariant();
			result = result.Where(p => (p.Name?.ToLowerInvariant().Contains(searchText) == true) || 
			                           (p.Description?.ToLowerInvariant().Contains(searchText) == true));
		}

		// Category filter
		if (isMap)
		{
			if (_mapCategoryFilter != "ALL")
				result = result.Where(p => NormalizeCategoryCode(p.Category) == _mapCategoryFilter);
		}
		else
		{
			if (_listCategoryFilter != "ALL")
				result = result.Where(p => NormalizeCategoryCode(p.Category) == _listCategoryFilter);
		}

		// Always calculate distance and sort by it if location is available
		if (_currentLocation != null)
		{
			var list = result.ToList();
			foreach (var p in list)
			{
				p.Distance = (int)CalculateDistance(_currentLocation.Latitude, _currentLocation.Longitude, p.Latitude, p.Longitude);
			}
			return list.OrderBy(p => p.Distance).ToList();
		}

		return result.ToList();
	}

	private bool CategoryMatchesMapFilter(string? poiCategory)
	{
		if (_mapCategoryFilter == "ALL")
		{
			return true;
		}

		return NormalizeCategoryCode(poiCategory) == _mapCategoryFilter;
	}

	private static string NormalizeCategoryCode(string? category)
	{
		if (string.IsNullOrWhiteSpace(category)) return "FOOD_STREET";
		
		var raw = category.Trim().ToUpperInvariant();
		
		// Map various string aliases to our internal codes
		// Using Contains for robustness against variations like "Oyster & Seafood"
		if (raw.Contains("SNAIL") || raw.Contains("SEA") || raw.Contains("OC") || raw.Contains("HAI SAN") || raw.Contains("OYSTER"))
			return "FOOD_SNAIL";
			
		if (raw.Contains("BBQ") || raw.Contains("NUONG") || raw.Contains("LAU") || raw.Contains("GRILL") || raw.Contains("HOTPOT"))
			return "FOOD_BBQ";
			
		if (raw.Contains("STREET") || raw.Contains("VAT") || raw.Contains("SNACK") || raw.Contains("QUAN AN") || raw.Contains("FOOD"))
			return "FOOD_STREET";
			
		if (raw.Contains("DRINK") || raw.Contains("BEVERAGE") || raw.Contains("CAFE") || raw.Contains("NUOC"))
			return "DRINK";
			
		if (raw.Contains("UTILITY") || raw.Contains("TIEN ICH") || raw.Contains("PARKING") || raw.Contains("TOILET"))
			return "UTILITY";

		// Direct code matching for cases where DB already has clean codes
		return raw switch
		{
			"FOOD_SNAIL" => "FOOD_SNAIL",
			"FOOD_BBQ" => "FOOD_BBQ",
			"FOOD_STREET" => "FOOD_STREET",
			"DRINK" => "DRINK",
			"UTILITY" => "UTILITY",
			_ => "ALL"
		};
	}
}

public class POIGroup : System.Collections.ObjectModel.ObservableCollection<POI>
{
	public string Name { get; private set; }
	public string GroupDisplayName { get; private set; }

	public POIGroup(string name, string displayName, System.Collections.Generic.IEnumerable<POI> pois) : base(pois)
	{
		Name = name;
		GroupDisplayName = displayName;
	}
}


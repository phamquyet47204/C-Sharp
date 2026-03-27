using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
using VinhKhanhFoodStreet.Models;
using VinhKhanhFoodStreet.Services;

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
	
	private ObservableCollection<PoiDisplayItem> _displayItems = new();
	private Dictionary<int, Pin> _poiPins = new();
	private List<POI> _allPois = new();
	private Location? _currentLocation;
	private bool _eventsAttached;
	private string _currentLanguage = "vi";
	private string _currentFilter = "All";
	private bool _isSearchExpanded;
	private bool _isListTabActive;

	public MainPage(
		IGeofenceEngine geofenceEngine,
		ILocationService locationService,
		INarrationService narrationService,
		IDatabaseService databaseService)
	{
		InitializeComponent();
		_geofenceEngine = geofenceEngine;
		_locationService = locationService;
		_narrationService = narrationService;
		_databaseService = databaseService;
		_narrationService.RegisterMediaElement(NarrationPlayer);
		
		// Bind CollectionView to observable collection
		PoiCollectionView.ItemsSource = _displayItems;
		SearchContainer.IsVisible = false;
		SetActiveTab(false);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		AttachEventsIfNeeded();
		MoveCameraToVinhKhanh();

		try
		{
			var canUseLocation = await EnsureLocationReadyAsync();
			if (!canUseLocation)
				return;

			await LoadMapPinsAndListAsync();
			await _geofenceEngine.StartAsync(_currentLanguage);
			LocationStatusLabel.Text = "Đang theo dõi vị trí...";
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi: {ex.Message}");
			LocationStatusLabel.Text = "Không thể khởi động GPS";
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

	private async Task LoadMapPinsAndListAsync()
	{
		_allPois = await LoadDisplayPoisForCurrentLanguageAsync();
		Debug.WriteLine($"[MainPage] Loaded {_allPois.Count} POIs for language {_currentLanguage}");
		PoiMap.Pins.Clear();
		_poiPins.Clear();

		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			foreach (var poi in _allPois)
			{
				// Add to map
				var pin = new Pin
				{
					Label = poi.Name,
					Address = poi.Description ?? "",
					Location = new Location(poi.Latitude, poi.Longitude),
					Type = PinType.Place
				};
				pin.InfoWindowClicked += (s, e) => OnPinClicked(poi);
				PoiMap.Pins.Add(pin);
				_poiPins[poi.Id] = pin;
			}
		});

		await RefreshCollectionViewAsync();
	}

	private async Task<List<POI>> LoadDisplayPoisForCurrentLanguageAsync()
	{
		var allPois = await _databaseService.GetAllPoisAsync();
		if (allPois.Count == 0)
		{
			return new List<POI>();
		}

		var selected = allPois
			.GroupBy(p => (Math.Round(p.Latitude, 5), Math.Round(p.Longitude, 5), p.Category ?? string.Empty))
			.Select(group =>
			{
				var ordered = group.OrderByDescending(p => p.Priority).ToList();
				return ordered.FirstOrDefault(p => string.Equals(p.LanguageCode, _currentLanguage, StringComparison.OrdinalIgnoreCase))
					?? ordered.FirstOrDefault(p => string.Equals(p.LanguageCode, "en", StringComparison.OrdinalIgnoreCase))
					?? ordered.FirstOrDefault(p => string.Equals(p.LanguageCode, "vi", StringComparison.OrdinalIgnoreCase))
					?? ordered.First();
			})
			.OrderByDescending(p => p.Priority)
			.ToList();

		return selected;
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
			// Update distances for all items
			foreach (var item in _displayItems)
			{
				item.Distance = (int)CalculateDistance(
					_currentLocation.Latitude, _currentLocation.Longitude, 
					item.Latitude, item.Longitude);
				item.IsNearest = (item.Id == nearest.Id);
			}

			// Scroll to nearest
			var nearestPoi = _displayItems.FirstOrDefault(x => x.Id == nearest.Id);
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
			LocationLabel.Text = string.Empty;
			LocationStatusLabel.Text = $"Cập nhật: {DateTime.Now:HH:mm}";
		});
		
		_ = HighlightNearestPoiAsync();
	}

	private void HandlePoiEntered(POI poi)
	{
		_ = Task.Run(async () =>
		{
			try
			{
				_geofenceEngine.MarkPoiAsPlayed(poi.Id);
				
				if (!string.IsNullOrWhiteSpace(poi.AudioPath))
				{
					try
					{
						await _narrationService.PlayAudioAsync(poi.AudioPath);
						return;
					}
					catch (Exception ex)
					{
						Debug.WriteLine($"[MainPage] Loi phat audio: {ex.Message}");
					}
				}

				var text = poi.Description ?? $"Đây là {poi.Name}";
				await _narrationService.SpeakAsync(text, poi.LanguageCode);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[MainPage] Loi geofence event: {ex.Message}");
			}
		});
	}

	private void HandlePoiExited(POI poi) { }

	// ==================== FILTER METHODS ====================

	/// <summary>
	/// Filter: Show all POIs
	/// </summary>
	private async void OnFilterAll(object? sender, EventArgs e)
	{
		_currentFilter = "All";
		await UpdateFilterUIAsync();
		await RefreshCollectionViewAsync();
	}

	/// <summary>
	/// Filter: Oyster/Seafood restaurants
	/// </summary>
	private async void OnFilterOyster(object? sender, EventArgs e)
	{
		_currentFilter = "Oyster";
		await UpdateFilterUIAsync();
		await RefreshCollectionViewAsync();
	}

	/// <summary>
	/// Filter: BBQ & Hotpot places
	/// </summary>
	private async void OnFilterBbq(object? sender, EventArgs e)
	{
		_currentFilter = "Bbq";
		await UpdateFilterUIAsync();
		await RefreshCollectionViewAsync();
	}

	/// <summary>
	/// Filter: Beverages & coffee
	/// </summary>
	private async void OnFilterBeverage(object? sender, EventArgs e)
	{
		_currentFilter = "Beverage";
		await UpdateFilterUIAsync();
		await RefreshCollectionViewAsync();
	}

	/// <summary>
	/// Search POI by name
	/// </summary>
	private async void OnSearchPoi(object? sender, EventArgs e)
	{
		var query = SearchBarPoi.Text?.Trim();
		if (string.IsNullOrWhiteSpace(query))
		{
			await RefreshCollectionViewAsync();
			return;
		}

		_displayItems.Clear();
		var filteredByCategory = _currentFilter switch
		{
			"Oyster" => _allPois.Where(p => p.Category == "Oyster"),
			"Bbq" => _allPois.Where(p => p.Category == "Bbq"),
			"Beverage" => _allPois.Where(p => p.Category == "Beverage"),
			_ => _allPois.AsEnumerable()
		};

		var filtered = filteredByCategory
			.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
						(p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
			.ToList();

		foreach (var poi in filtered)
		{
			var item = new PoiDisplayItem
			{
				Id = poi.Id,
				Name = poi.Name,
				Description = poi.Description ?? "Chưa có mô tả",
				ImagePath = poi.ImagePath ?? "dotnet_bot.png",
				Latitude = poi.Latitude,
				Longitude = poi.Longitude,
				AudioPath = poi.AudioPath,
				LanguageCode = poi.LanguageCode,
				Distance = _currentLocation != null ? (int)CalculateDistance(
					_currentLocation.Latitude, _currentLocation.Longitude, 
					poi.Latitude, poi.Longitude) : 0,
				Rating = 4.5f,
				IsNearest = false
			};
			_displayItems.Add(item);
		}

		PoisCountLabel.Text = $"({_displayItems.Count})";
		await HighlightNearestPoiAsync();
	}

	/// <summary>
	/// Update filter buttons UI state
	/// </summary>
	private async Task UpdateFilterUIAsync()
	{
		await MainThread.InvokeOnMainThreadAsync(() =>
		{
			// Reset all chips
			ChipAll.BackgroundColor = _currentFilter == "All" 
				? Color.FromArgb("#FF7F50") 
				: Colors.White;
			ChipAll.TextColor = _currentFilter == "All" ? Colors.White : Color.FromArgb("#666666");

			ChipOyster.BackgroundColor = _currentFilter == "Oyster" 
				? Color.FromArgb("#FF7F50") 
				: Colors.White;
			ChipOyster.TextColor = _currentFilter == "Oyster" ? Colors.White : Color.FromArgb("#666666");

			ChipBbq.BackgroundColor = _currentFilter == "Bbq" 
				? Color.FromArgb("#FF7F50") 
				: Colors.White;
			ChipBbq.TextColor = _currentFilter == "Bbq" ? Colors.White : Color.FromArgb("#666666");

			ChipBeverage.BackgroundColor = _currentFilter == "Beverage" 
				? Color.FromArgb("#FF7F50") 
				: Colors.White;
			ChipBeverage.TextColor = _currentFilter == "Beverage" ? Colors.White : Color.FromArgb("#666666");
		});
	}

	/// <summary>
	/// Refresh POI collection with current filter
	/// </summary>
	private async Task RefreshCollectionViewAsync()
	{
		_displayItems.Clear();
		
		// Filter POIs based on _currentFilter
		var filteredPois = _currentFilter switch
		{
			"Oyster" => _allPois.Where(p => p.Category == "Oyster").ToList(),
			"Bbq" => _allPois.Where(p => p.Category == "Bbq").ToList(),
			"Beverage" => _allPois.Where(p => p.Category == "Beverage").ToList(),
			_ => _allPois // "All"
		};

		foreach (var poi in filteredPois)
		{
			var item = new PoiDisplayItem
			{
				Id = poi.Id,
				Name = poi.Name,
				Description = poi.Description ?? "Chưa có mô tả",
				ImagePath = poi.ImagePath ?? "dotnet_bot.png",
				Latitude = poi.Latitude,
				Longitude = poi.Longitude,
				AudioPath = poi.AudioPath,
				LanguageCode = poi.LanguageCode,
				Distance = _currentLocation != null ? (int)CalculateDistance(
					_currentLocation.Latitude, _currentLocation.Longitude, 
					poi.Latitude, poi.Longitude) : 0,
				Rating = 4.5f,
				IsNearest = false
			};
			_displayItems.Add(item);
		}

		PoisCountLabel.Text = $"({_displayItems.Count})";
		await HighlightNearestPoiAsync();
	}

	private void OnPinClicked(POI poi)
	{
		Debug.WriteLine($"[MainPage] Pin clicked: {poi.Name}");
	}

	private async void OnPlayPoi(object? sender, EventArgs e)
	{
		try
		{
			// Get PoiDisplayItem from button's binding context
			var button = sender as Button;
			if (button?.BindingContext is PoiDisplayItem displayItem)
			{
				// Find the actual POI from _allPois
				var poi = _allPois.FirstOrDefault(p => p.Id == displayItem.Id);
				if (poi != null)
				{
					if (!string.IsNullOrWhiteSpace(poi.AudioPath))
					{
						await _narrationService.PlayAudioAsync(poi.AudioPath);
						return;
					}
					
					var text = poi.Description ?? $"Đây là {poi.Name}";
					await _narrationService.SpeakAsync(text, poi.LanguageCode);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi play audio: {ex.Message}");
			await DisplayAlertAsync("Lỗi", $"Không thể phát âm thanh: {ex.Message}", "OK");
		}
	}

	private void OnNavigatePoi(object? sender, EventArgs e)
	{
		try
		{
			// Get PoiDisplayItem from button's binding context
			var button = sender as Button;
			if (button?.BindingContext is PoiDisplayItem displayItem)
			{
				// Find the actual POI from _allPois
				var poi = _allPois.FirstOrDefault(p => p.Id == displayItem.Id);
				if (poi != null)
				{
					SetActiveTab(false);
					var location = new Location(poi.Latitude, poi.Longitude);
					PoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(200)));
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi navigate: {ex.Message}");
		}
	}

	private async void OnToggleLanguage(object? sender, EventArgs e)
	{
		// Cycle through 3 languages: vi -> en -> ja -> vi
		_currentLanguage = _currentLanguage switch
		{
			"vi" => "en",
			"en" => "ja",
			_ => "vi"
		};

		LanguageButton.Text = _currentLanguage switch
		{
			"vi" => "🌐 VN",
			"en" => "🌐 EN",
			"ja" => "🌐 JP",
			_ => "🌐 VN"
		};

		Debug.WriteLine($"[MainPage] Switched language to: {_currentLanguage}");

		try
		{
			await _geofenceEngine.SetLanguageAsync(_currentLanguage);
			await LoadMapPinsAndListAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi doi ngon ngu: {ex.Message}");
			await DisplayAlertAsync("Lỗi", "Không thể tải lại dữ liệu theo ngôn ngữ mới.", "OK");
		}
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
			LocationStatusLabel.Text = "Cần cấp quyền vị trí";
			LocationLabel.Text = string.Empty;
			await DisplayAlertAsync("Lỗi", "App cần quyền vị trí để hoạt động", "OK");
			AppInfo.Current.ShowSettingsUI();
			return false;
		}

		try
		{
			var loc = await Geolocation.Default.GetLocationAsync(
				new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(3)));
			if (loc != null)
				_currentLocation = loc;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi GPS: {ex.Message}");
		}

		return true;
	}

	private async void OnToggleSearch(object? sender, EventArgs e)
	{
		_isSearchExpanded = !_isSearchExpanded;
		SearchContainer.IsVisible = _isSearchExpanded;
		SearchToggleButton.Text = _isSearchExpanded ? "✖" : "🔍";

		if (_isSearchExpanded)
		{
			await MainThread.InvokeOnMainThreadAsync(() => SearchBarPoi.Focus());
		}
		else
		{
			SearchBarPoi.Text = string.Empty;
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
}

/// <summary>
/// Display model cho CollectionView binding
/// </summary>
public class PoiDisplayItem : INotifyPropertyChanged
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string ImagePath { get; set; } = string.Empty;
	public double Latitude { get; set; }
	public double Longitude { get; set; }
	public string AudioPath { get; set; } = string.Empty;
	public string LanguageCode { get; set; } = "vi";
	public int Distance { get; set; }
	public float Rating { get; set; }
	
	private bool _isNearest;
	public bool IsNearest
	{
		get => _isNearest;
		set
		{
			if (_isNearest != value)
			{
				_isNearest = value;
				OnPropertyChanged(nameof(IsNearest));
				OnPropertyChanged(nameof(IsNearestBorderWidth));
			}
		}
	}

	public int IsNearestBorderWidth => IsNearest ? 3 : 1;

	public Color IsNearestCardStroke => IsNearest 
		? Color.FromArgb("#FF7F50")  // Coral for nearest
		: Color.FromArgb("#E0E0E0"); // Light border for others

	public event PropertyChangedEventHandler? PropertyChanged;

	protected void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}





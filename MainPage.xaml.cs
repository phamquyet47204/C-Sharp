using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
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
	private readonly MainPageViewModel _viewModel;
	
	private ObservableCollection<POI> _displayItems = new();
	private Dictionary<int, Pin> _poiPins = new();
	private List<POI> _allPois = new();
	private Dictionary<int, List<POI>> _poiVariantsByGroup = new();
	private static readonly (string Code, string TextKey)[] CategoryOptions =
	[
		("ALL", "CategoryAll"),
		("FOOD_SNAIL", "CategorySnail"),
		("FOOD_BBQ", "CategoryBbq"),
		("FOOD_STREET", "CategoryStreet"),
		("DRINK", "CategoryDrink"),
		("UTILITY", "CategoryUtility")
	];
	private Location? _currentLocation;
	private bool _eventsAttached;
	private string _currentLanguage = "vi";
	private string _currentFilter = "ALL";
	private bool _isUpdatingCategoryPicker;
	private bool _isSearchExpanded;
	private bool _isListTabActive;
	private bool _isSystemLanguageInitialized;

	private string T(string key)
	{
		return (_currentLanguage, key) switch
		{
			("en", "AppTitle") => "Vinh Khanh Food Street",
			("ja", "AppTitle") => "ビンカイン フードストリート",
			(_, "AppTitle") => "Vĩnh Khánh Food Street",

			("en", "MapTab") => "Map",
			("ja", "MapTab") => "地図",
			(_, "MapTab") => "Bản đồ",

			("en", "ListTab") => "List",
			("ja", "ListTab") => "一覧",
			(_, "ListTab") => "Danh sách",

			("en", "CategoryAll") => "All",
			("ja", "CategoryAll") => "すべて",
			(_, "CategoryAll") => "Tất cả",

			("en", "CategorySnail") => "Snails & Seafood",
			("ja", "CategorySnail") => "巻貝・海鮮",
			(_, "CategorySnail") => "Ốc & Hải sản",

			("en", "CategoryBbq") => "BBQ & Hotpot",
			("ja", "CategoryBbq") => "焼肉/鍋",
			(_, "CategoryBbq") => "Đồ nướng & Lẩu",

			("en", "CategoryStreet") => "Street Food",
			("ja", "CategoryStreet") => "ストリートフード",
			(_, "CategoryStreet") => "Ăn vặt",

			("en", "CategoryDrink") => "Drinks",
			("ja", "CategoryDrink") => "ドリンク",
			(_, "CategoryDrink") => "Đồ uống",

			("en", "CategoryUtility") => "Utilities",
			("ja", "CategoryUtility") => "ユーティリティ",
			(_, "CategoryUtility") => "Tiện ích",

			("en", "MyLocation") => "Me",
			("ja", "MyLocation") => "私",
			(_, "MyLocation") => "Tôi",

			("en", "ListHeader") => "Restaurant List",
			("ja", "ListHeader") => "レストラン一覧",
			(_, "ListHeader") => "Danh Sách Quán Ăn",

			("en", "Search") => "Find",
			("ja", "Search") => "検索",
			(_, "Search") => "Tìm",

			("en", "Close") => "Close",
			("ja", "Close") => "閉じる",
			(_, "Close") => "Đóng",

			("en", "SearchPlaceholder") => "Search restaurants...",
			("ja", "SearchPlaceholder") => "店名を検索...",
			(_, "SearchPlaceholder") => "Tìm quán ăn...",

			("en", "Play") => "🔊",
			("ja", "Play") => "🔊",
			(_, "Play") => "🔊",

			("en", "Navigate") => "🧭",
			("ja", "Navigate") => "🧭",
			(_, "Navigate") => "🧭",

			("en", "GpsChecking") => "Checking GPS...",
			("ja", "GpsChecking") => "GPSを確認中...",
			(_, "GpsChecking") => "Đang kiểm tra GPS...",

			("en", "GpsTracking") => "Tracking location...",
			("ja", "GpsTracking") => "位置追跡中...",
			(_, "GpsTracking") => "Đang theo dõi vị trí...",

			("en", "GpsStartFailed") => "Cannot start GPS",
			("ja", "GpsStartFailed") => "GPSを開始できません",
			(_, "GpsStartFailed") => "Không thể khởi động GPS",

			("en", "LocationUpdated") => "Updated",
			("ja", "LocationUpdated") => "更新",
			(_, "LocationUpdated") => "Cập nhật",

			("en", "PermissionRequiredStatus") => "Location permission required",
			("ja", "PermissionRequiredStatus") => "位置情報の許可が必要です",
			(_, "PermissionRequiredStatus") => "Cần cấp quyền vị trí",

			("en", "NoLocationPermission") => "No location permission",
			("ja", "NoLocationPermission") => "位置情報の許可がありません",
			(_, "NoLocationPermission") => "Chưa có quyền truy cập vị trí",

			("en", "CurrentLocationFetched") => "Current location acquired",
			("ja", "CurrentLocationFetched") => "現在地を取得しました",
			(_, "CurrentLocationFetched") => "Đã lấy vị trí hiện tại",

			("en", "GpsNotReady") => "GPS not ready",
			("ja", "GpsNotReady") => "GPSの準備ができていません",
			(_, "GpsNotReady") => "GPS chưa sẵn sàng",

			("en", "CenteredToYourLocation") => "Centered to your location",
			("ja", "CenteredToYourLocation") => "現在地に移動しました",
			(_, "CenteredToYourLocation") => "Đã căn giữa vị trí của bạn",

			("en", "ErrorTitle") => "Error",
			("ja", "ErrorTitle") => "エラー",
			(_, "ErrorTitle") => "Lỗi",

			("en", "InfoTitle") => "Notice",
			("ja", "InfoTitle") => "お知らせ",
			(_, "InfoTitle") => "Thông báo",

			("en", "Ok") => "OK",
			("ja", "Ok") => "OK",
			(_, "Ok") => "OK",

			("en", "AudioPlayFailed") => "Cannot play audio:",
			("ja", "AudioPlayFailed") => "音声を再生できません:",
			(_, "AudioPlayFailed") => "Không thể phát âm thanh:",

			("en", "ReloadByLanguageFailed") => "Cannot reload data for selected language.",
			("ja", "ReloadByLanguageFailed") => "選択した言語でデータを再読み込みできません。",
			(_, "ReloadByLanguageFailed") => "Không thể tải lại dữ liệu theo ngôn ngữ mới.",

			("en", "LocationPermissionNeededMessage") => "The app needs location permission to work.",
			("ja", "LocationPermissionNeededMessage") => "アプリの利用には位置情報の許可が必要です。",
			(_, "LocationPermissionNeededMessage") => "App cần quyền vị trí để hoạt động",

			("en", "CenterNoPermissionMessage") => "Location permission is required to center map.",
			("ja", "CenterNoPermissionMessage") => "地図を現在地へ移動するには位置情報の許可が必要です。",
			(_, "CenterNoPermissionMessage") => "Chưa có quyền vị trí để định vị người dùng.",

			("en", "CannotGetCurrentLocationMessage") => "Cannot get current location.",
			("ja", "CannotGetCurrentLocationMessage") => "現在地を取得できません。",
			(_, "CannotGetCurrentLocationMessage") => "Không lấy được vị trí hiện tại.",

			("en", "CannotLocateCurrentPositionMessage") => "Cannot locate current position.",
			("ja", "CannotLocateCurrentPositionMessage") => "現在地を特定できません。",
			(_, "CannotLocateCurrentPositionMessage") => "Không thể định vị vị trí hiện tại.",

			("en", "NarrationFallback") => "This is",
			("ja", "NarrationFallback") => "こちらは",
			(_, "NarrationFallback") => "Đây là",

			_ => key
		};
	}

	public MainPage(
		IGeofenceEngine geofenceEngine,
		ILocationService locationService,
		INarrationService narrationService,
		IDatabaseService databaseService,
		IAppLanguageService appLanguageService)
	{
		InitializeComponent();
		_geofenceEngine = geofenceEngine;
		_locationService = locationService;
		_narrationService = narrationService;
		_databaseService = databaseService;
		_appLanguageService = appLanguageService;
		_viewModel = new MainPageViewModel();
		_narrationService.RegisterMediaElement(NarrationPlayer);
		_currentLanguage = _appLanguageService.GetEffectiveLanguage();
		
		// Gan BindingContext theo MVVM de UI doc du lieu tu ViewModel.
		BindingContext = _viewModel;
		_displayItems = _viewModel.DisplayPois;
		PoiCollectionView.ItemsSource = _viewModel.DisplayPois;
		SearchContainer.IsVisible = false;
		InitializeCategoryPicker();
		ApplyLanguageUi();
		ResetPoiUiState();
		SetActiveTab(false);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		AttachEventsIfNeeded();
		MoveCameraToVinhKhanh();

		try
		{
			InitializeLanguageFromSystemIfNeeded();

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
		var filteredPois = GetFilteredPoisForCurrentFilter();

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
		var filteredPois = GetFilteredPoisForCurrentFilter();

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
			_poiVariantsByGroup = new Dictionary<int, List<POI>>();
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

	private static int GetAggregateId(POI poi)
	{
		if (poi.BasePoiId > 0)
		{
			return poi.BasePoiId;
		}

		return poi.Id;
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

				var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
				await _narrationService.SpeakAsync(text, _currentLanguage);
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[MainPage] Loi geofence event: {ex.Message}");
			}
		});
	}

	private void HandlePoiExited(POI poi) { }

	private async void OnCategoryPickerChanged(object? sender, EventArgs e)
	{
		if (_isUpdatingCategoryPicker || CategoryPicker.SelectedIndex < 0 || CategoryPicker.SelectedIndex >= CategoryOptions.Length)
		{
			return;
		}

		_currentFilter = CategoryOptions[CategoryPicker.SelectedIndex].Code;
		await RefreshMapPinTextsAsync();
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
		var filteredByCategory = _allPois.Where(p => CategoryMatchesCurrentFilter(p.Category));

		var filtered = filteredByCategory
			.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
						(p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
			.ToList();

		foreach (var poi in filtered)
		{
			var item = CreateDisplayPoi(poi);
			_displayItems.Add(item);
		}

		PoisCountLabel.Text = $"({_displayItems.Count})";
		await HighlightNearestPoiAsync();
	}

	/// <summary>
	/// Refresh POI collection with current filter
	/// </summary>
	private async Task RefreshCollectionViewAsync()
	{
		_displayItems.Clear();
		
		// Filter POIs based on _currentFilter
		var filteredPois = GetFilteredPoisForCurrentFilter();

		_viewModel.ReplaceDisplayPois(filteredPois.Select(CreateDisplayPoi));

		PoisCountLabel.Text = $"({_displayItems.Count})";
		await HighlightNearestPoiAsync();
	}

	private POI? _selectedPinPoi;

	private void OnPinClicked(POI poi)
	{
		Debug.WriteLine($"[MainPage] Pin clicked: {poi.Name}");
		_selectedPinPoi = poi;
		MainThread.BeginInvokeOnMainThread(() =>
		{
			PinCardNameLabel.Text = poi.Name;
			PinQuickCard.IsVisible = true;
		});
	}

	private async void OnPinCardPlay(object? sender, EventArgs e)
	{
		if (_selectedPinPoi is null) return;
		var poi = _selectedPinPoi;
		PinQuickCard.IsVisible = false;

		try
		{
			if (!string.IsNullOrWhiteSpace(poi.AudioPath))
			{
				try { await _narrationService.PlayAudioAsync(poi.AudioPath); return; }
				catch (Exception ex) { Debug.WriteLine($"[MainPage] Audio fallback TTS: {ex.Message}"); }
			}
			var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
			await _narrationService.SpeakAsync(text, _currentLanguage);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi phat TTS: {ex.Message}");
		}
	}

	private void OnPinCardClose(object? sender, EventArgs e)
	{
		PinQuickCard.IsVisible = false;
		_selectedPinPoi = null;
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
				if (poi != null)
				{
					if (!string.IsNullOrWhiteSpace(poi.AudioPath))
					{
						try
						{
							await _narrationService.PlayAudioAsync(poi.AudioPath);
							return;
						}
						catch (Exception audioEx)
						{
							Debug.WriteLine($"[MainPage] Loi phat file audio, fallback TTS: {audioEx.Message}");
						}
					}
					
					var text = poi.Description ?? $"{T("NarrationFallback")} {poi.Name}";
					await _narrationService.SpeakAsync(text, _currentLanguage);
				}
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
					PoiMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, Distance.FromMeters(200)));
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

	private async void OnToggleLanguage(object? sender, EventArgs e)
	{
		// Cycle through 3 languages: vi -> en -> ja -> vi
		_currentLanguage = _currentLanguage switch
		{
			"vi" => "en",
			"en" => "ja",
			_ => "vi"
		};

		_appLanguageService.SetPreferredLanguage(_currentLanguage);

		ApplyLanguageUi();

		Debug.WriteLine($"[MainPage] Switched language to: {_currentLanguage}");

		try
		{
			await _geofenceEngine.SetLanguageAsync(_currentLanguage);
			await LoadMapPinsAndListAsync(reloadFromDatabase: true);
			await _geofenceEngine.StartAsync(_currentLanguage);
			await HighlightNearestPoiAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[MainPage] Loi doi ngon ngu: {ex.Message}");
			await DisplayAlertAsync(T("ErrorTitle"), T("ReloadByLanguageFailed"), T("Ok"));
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
		RefreshCategoryPickerTexts();
		ListHeaderTitleLabel.Text = T("ListHeader");
		SearchToggleButton.Text = _isSearchExpanded ? "✕" : "🔍";
		SearchBarPoi.Placeholder = T("SearchPlaceholder");
		LocationStatusLabel.Text = T("GpsChecking");
		ApplyLocalizedActionTextsToDisplayItems();

		LanguageButton.Text = _currentLanguage switch
		{
			"vi" => "VN",
			"en" => "EN",
			"ja" => "JP",
			_ => "VN"
		};
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
		SearchToggleButton.Text = _isSearchExpanded ? "✕" : "🔍";

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

	private void InitializeCategoryPicker()
	{
		RefreshCategoryPickerTexts();
	}

	private void RefreshCategoryPickerTexts()
	{
		_isUpdatingCategoryPicker = true;

		var selectedCode = _currentFilter;
		CategoryPicker.ItemsSource = CategoryOptions
			.Select(x => T(x.TextKey))
			.ToList();

		var selectedIndex = Array.FindIndex(CategoryOptions, x => x.Code == selectedCode);
		CategoryPicker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;

		_isUpdatingCategoryPicker = false;
	}

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
	/// Lay tap POI theo filter dang chon de dong bo map va list.
	/// </summary>
	private List<POI> GetFilteredPoisForCurrentFilter()
	{
		return _allPois.Where(p => CategoryMatchesCurrentFilter(p.Category)).ToList();
	}

	private bool CategoryMatchesCurrentFilter(string? poiCategory)
	{
		if (_currentFilter == "ALL")
		{
			return true;
		}

		return NormalizeCategoryCode(poiCategory) == _currentFilter;
	}

	private static string NormalizeCategoryCode(string? category)
	{
		var code = category?.Trim().ToUpperInvariant();
		return code switch
		{
			"FOOD_SNAIL" => "FOOD_SNAIL",
			"FOOD_BBQ" => "FOOD_BBQ",
			"FOOD_STREET" => "FOOD_STREET",
			"DRINK" => "DRINK",
			"UTILITY" => "UTILITY",
			"OYSTER" => "FOOD_SNAIL",
			"BBQ" => "FOOD_BBQ",
			"BEVERAGE" => "DRINK",
			"ALL" => "ALL",
			_ => "FOOD_STREET"
		};
	}
}


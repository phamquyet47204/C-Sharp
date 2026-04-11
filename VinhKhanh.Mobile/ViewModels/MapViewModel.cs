using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Services;
using VinhKhanh.Mobile.Models;

namespace VinhKhanh.Mobile.ViewModels;

public partial class MapViewModel(LocalDatabase db, GeofenceService geofence, SyncService sync, AccessControlService accessControl)
    : ObservableObject
{
    private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(15);
    private CancellationTokenSource? _autoSyncCts;
    private Task? _autoSyncTask;

    [ObservableProperty] private List<PoiRecord> _pois = [];
    [ObservableProperty] private List<PoiRecord> _filteredPois = [];
    [ObservableProperty] private string _selectedLanguage = "vi-VN";
    [ObservableProperty] private bool _isUnlocked;
    [ObservableProperty] private string _selectedCategory = "Tất cả";
    [ObservableProperty] private AccessStatus? _accessStatus;
    [ObservableProperty] private string _freeTrialMessage = string.Empty;
<<<<<<< HEAD

    public List<string> Languages { get; } = ["vi-VN", "en-US", "ja-JP"];
    public List<string> Categories { get; private set; } = ["Tất cả"];

    partial void OnSelectedCategoryChanged(string value) => ApplyCategoryFilter();
=======
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isListTabActive;
    [ObservableProperty] private bool _isRefreshing;

    // Event để MapPage biết cần zoom vào POI nào
    public event Action<PoiRecord>? NavigateToPoiOnMap;

    public List<string> Languages { get; } = ["vi-VN", "en-US", "ja-JP"];
    public List<string> Categories { get; private set; } = ["Tất cả"];

    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedLanguageChanged(string value)
    {
        Preferences.Set("language", value);
        // Reload POI theo ngôn ngữ mới (fire-and-forget)
        _ = LoadLocalStateAsync();
    }
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)

    partial void OnPoisChanged(List<PoiRecord> value)
    {
        // Cập nhật danh sách category từ dữ liệu thực
        var cats = value
            .Select(p => p.CategoryDisplayName)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        cats.Insert(0, "Tất cả");
        Categories = cats;
        OnPropertyChanged(nameof(Categories));
<<<<<<< HEAD
        ApplyCategoryFilter();
    }

    private void ApplyCategoryFilter()
    {
        FilteredPois = SelectedCategory == "Tất cả"
            ? Pois
            : Pois.Where(p => p.CategoryDisplayName == SelectedCategory).ToList();
=======
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var result = Pois.AsEnumerable();

        // Lọc theo category
        if (SelectedCategory != "Tất cả")
            result = result.Where(p => p.CategoryDisplayName == SelectedCategory);

        // Lọc theo search text
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            result = result.Where(p =>
                p.Name.ToLowerInvariant().Contains(q) ||
                p.Description.ToLowerInvariant().Contains(q));
        }

        FilteredPois = result.ToList();
    }

    /// <summary>Gọi từ danh sách khi user nhấn "Xem trên bản đồ".</summary>
    [RelayCommand]
    void ShowOnMap(PoiRecord poi)
    {
        IsListTabActive = false;
        NavigateToPoiOnMap?.Invoke(poi);
    }

    /// <summary>Gọi từ danh sách khi user nhấn icon 🔊.</summary>
    public event Action<PoiRecord>? ListenRequested;

    [RelayCommand]
    void Listen(PoiRecord poi)
    {
        ListenRequested?.Invoke(poi);
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    }

    [RelayCommand]
    async Task LoadAsync()
    {
        try
        {
            await SyncAndReloadAsync(showAlert: true);
        }
        catch (Exception ex)
        {
            var page = Application.Current?.MainPage;
            if (page is not null)
                await page.DisplayAlert("Lỗi đồng bộ", ex.Message, "OK");

            await LoadLocalStateAsync();
        }
<<<<<<< HEAD
    }

    public void StartAutoSync()
    {
        if (_autoSyncTask is not null && !_autoSyncTask.IsCompleted)
            return;

        _autoSyncCts = new CancellationTokenSource();
        _autoSyncTask = RunAutoSyncLoopAsync(_autoSyncCts.Token);
    }

    public void StopAutoSync()
    {
        _autoSyncCts?.Cancel();
        _autoSyncCts?.Dispose();
        _autoSyncCts = null;
    }

    public void StartMonitoring()
    {
        geofence.Start();
        StartAutoSync();
    }

    public void StopMonitoring()
    {
        StopAutoSync();
        geofence.Stop();
    }

    private async Task RunAutoSyncLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_syncInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try { await SyncAndReloadAsync(showAlert: false); }
            catch { }
        }
    }

    private async Task SyncAndReloadAsync(bool showAlert)
    {
        var syncResult = await sync.SyncIfConnectedAsync();

        if (showAlert && !string.IsNullOrWhiteSpace(syncResult.Message))
        {
            var page = Application.Current?.MainPage;
            if (page is not null)
                await page.DisplayAlert("Đồng bộ dữ liệu", syncResult.Message, "OK");
        }

        // Requirement 14.5: gọi GetAccessStatusAsync() sau mỗi lần sync
        await RefreshAccessStatusAsync();
        await LoadLocalStateAsync();
    }

    public async Task RefreshAccessStatusAsync()
    {
        try
        {
            var status = await accessControl.GetAccessStatusAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AccessStatus = status;
                // Requirement 4.5: hiển thị "Bạn đã dùng X/3 lượt thử miễn phí"
                FreeTrialMessage = (!status.HasActivePass && status.FreeTrialUsed < status.FreeTrialLimit)
                    ? $"Bạn đã dùng {status.FreeTrialUsed}/{status.FreeTrialLimit} lượt thử miễn phí"
                    : string.Empty;
            });
        }
        catch
        {
            // Giữ trạng thái cũ nếu lỗi
        }
    }

    private async Task LoadLocalStateAsync()
    {
        var activePois = await db.GetActivePoisAsync();
        var unlocked = Preferences.Get("unlocked", false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Pois = activePois;
            IsUnlocked = unlocked;
        });
=======
>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    }

    [RelayCommand]
    async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await SyncAndReloadAsync(showAlert: false);
        }
        catch { }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void StartAutoSync()
    {
        if (_autoSyncTask is not null && !_autoSyncTask.IsCompleted)
            return;

        _autoSyncCts = new CancellationTokenSource();
        _autoSyncTask = RunAutoSyncLoopAsync(_autoSyncCts.Token);
    }

    public void StopAutoSync()
    {
        _autoSyncCts?.Cancel();
        _autoSyncCts?.Dispose();
        _autoSyncCts = null;
    }

    public void StartMonitoring()
    {
        geofence.Start();
        StartAutoSync();
    }

    public void StopMonitoring()
    {
        StopAutoSync();
        geofence.Stop();
    }

    private async Task RunAutoSyncLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_syncInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try { await SyncAndReloadAsync(showAlert: false); }
            catch { }
        }
    }

    private async Task SyncAndReloadAsync(bool showAlert)
    {
        var syncResult = await sync.SyncIfConnectedAsync();

        if (showAlert && !string.IsNullOrWhiteSpace(syncResult.Message))
        {
            var page = Application.Current?.MainPage;
            if (page is not null)
                await page.DisplayAlert("Đồng bộ dữ liệu", syncResult.Message, "OK");
        }

        // Requirement 14.5: gọi GetAccessStatusAsync() sau mỗi lần sync
        await RefreshAccessStatusAsync();
        await LoadLocalStateAsync();
    }

    public async Task RefreshAccessStatusAsync()
    {
        try
        {
            var status = await accessControl.GetAccessStatusAsync();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                AccessStatus = status;
                // Hiển thị ngày hết hạn trial
                if (!status.HasActivePass && status.IsTrialActive && status.TrialExpiresAt.HasValue)
                {
                    var daysLeft = (int)(status.TrialExpiresAt.Value - DateTime.UtcNow).TotalDays + 1;
                    FreeTrialMessage = $"Dùng thử miễn phí: còn {daysLeft} ngày";
                }
                else if (!status.HasActivePass && !status.IsTrialActive)
                {
                    FreeTrialMessage = "Thời gian dùng thử đã hết. Vui lòng mua Access Pass.";
                }
                else
                {
                    FreeTrialMessage = string.Empty;
                }
            });
        }
        catch
        {
            // Giữ trạng thái cũ nếu lỗi
        }
    }

    private async Task LoadLocalStateAsync()
    {
        var lang = Preferences.Get("language", "vi-VN");
        var activePois = await db.GetActivePoisByLanguageAsync(lang);
        var unlocked = Preferences.Get("unlocked", false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Pois = activePois;
            IsUnlocked = unlocked;
        });
    }

    [RelayCommand]
    async Task UnlockAsync()
    {
        Preferences.Set("unlocked", true);
        Preferences.Set("unlock_expiry", DateTime.UtcNow.AddDays(7).Ticks);
        IsUnlocked = true;
        var page = Application.Current?.MainPage;
        if (page is not null)
            await page.DisplayAlert("Cảm ơn!", "Đã mở khóa 7 ngày.", "OK");
    }
}

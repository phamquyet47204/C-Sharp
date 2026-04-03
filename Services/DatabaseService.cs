using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using VinhKhanhFoodStreet.Configuration;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using SQLite;
using VinhKhanhFoodStreet.Models;

namespace VinhKhanhFoodStreet.Services;

/// <summary>
/// Service thao tác SQLite cho POI.
/// Thiết kế theo hướng async để không chặn UI thread trong .NET MAUI.
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly string _databasePath;
    private readonly HttpClient _httpClient;
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private const string UpdatesEndpoint = "api/pois/updates";
    private const string LastSyncPreferenceKey = "root_last_sync_utc";

    /// <summary>
    /// Nhận đường dẫn database từ DI để dễ cấu hình theo từng môi trường.
    /// </summary>
    /// <param name="databasePath">Đường dẫn file SQLite (.db3).</param>
    public DatabaseService(string databasePath)
    {
        _databasePath = databasePath;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(AppConfig.BaseApiUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    /// <summary>
    /// Khởi tạo kết nối SQLite và tạo bảng POI nếu chưa tồn tại.
    /// Hàm an toàn khi được gọi nhiều lần nhờ cơ chế khóa và cờ trạng thái.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            _database = new SQLiteAsyncConnection(_databasePath);
            await _database.CreateTableAsync<POI>();
            await EnsureSchemaCompatibilityAsync();
            await RemoveSeedPoisAsync();
            await NormalizeBasePoiIdsAsync();
            _isInitialized = true;

            // Ghi log de de dang kiem tra qua trinh khoi tao DB khi debug/chay app.
            var successMessage = $"[DatabaseService] Khoi tao SQLite thanh cong. DB Path: {_databasePath}";
            Debug.WriteLine(successMessage);
            Console.WriteLine(successMessage);
        }
        catch (Exception ex)
        {
            // Ném lỗi lên tầng trên để ViewModel/UseCase quyết định thông báo UI phù hợp.
            throw new InvalidOperationException("Khong the khoi tao co so du lieu SQLite.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Thêm mới một POI.
    /// </summary>
    public async Task<int> AddPoiAsync(POI poi)
    {
        try
        {
            await EnsureInitializedAsync();
            ValidatePoiInput(poi);

            return await _database!.InsertAsync(poi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the them POI vao database.", ex);
        }
    }

    /// <summary>
    /// Cập nhật thông tin POI theo Id.
    /// </summary>
    public async Task<int> UpdatePoiAsync(POI poi)
    {
        try
        {
            await EnsureInitializedAsync();
            ValidatePoiInput(poi);

            return await _database!.UpdateAsync(poi);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the cap nhat POI trong database.", ex);
        }
    }

    /// <summary>
    /// Xóa POI theo khóa chính Id.
    /// </summary>
    public async Task<int> DeletePoiAsync(int poiId)
    {
        try
        {
            await EnsureInitializedAsync();

            if (poiId <= 0)
            {
                throw new ArgumentException("Id POI khong hop le.", nameof(poiId));
            }

            return await _database!.DeleteAsync<POI>(poiId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the xoa POI khoi database.", ex);
        }
    }

    /// <summary>
    /// Đồng bộ POI từ backend SQL Server về SQLite local theo cơ chế delta-sync.
    /// </summary>
    public async Task<bool> SyncPoisFromServerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureInitializedAsync();

            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                Debug.WriteLine("[DatabaseService] Skip sync: khong co Internet");
                return false;
            }

            await _syncLock.WaitAsync(cancellationToken);
            try
            {
                var lastSync = GetLastSyncTime();
                var requestUrl =
                    $"{UpdatesEndpoint}?lastSync={Uri.EscapeDataString(lastSync.ToString("O", CultureInfo.InvariantCulture))}&includeAudio=true";

                using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[DatabaseService] Sync that bai: {(int)response.StatusCode}");
                    return false;
                }

                var payload = await response.Content.ReadFromJsonAsync<RemoteSyncResponse>(cancellationToken: cancellationToken);
                if (payload is null)
                {
                    Debug.WriteLine("[DatabaseService] Sync that bai: payload null");
                    return false;
                }

                await ApplyServerChangesAsync(payload, cancellationToken);
                SaveLastSyncTime(payload.ServerTime);

                Debug.WriteLine(
                    $"[DatabaseService] Sync OK. Updated={payload.UpdatedPois.Count}, Deleted={payload.DeletedIds.Count}, ServerTime={payload.ServerTime:O}");
                return true;
            }
            finally
            {
                _syncLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[DatabaseService] Sync bi huy boi cancellation token.");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DatabaseService] Loi SyncPoisFromServerAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Lấy toàn bộ POI để hiển thị đầy đủ trên bản đồ.
    /// </summary>
    public async Task<List<POI>> GetAllPoisAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            return await _database!
                .Table<POI>()
                .OrderByDescending(x => x.Priority)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the lay toan bo danh sach POI.", ex);
        }
    }

    /// <summary>
    /// Lay danh sach POI da duoc gom theo BasePoiId va localize theo 3-tier fallback.
    /// Tier 1: ngon ngu duoc yeu cau.
    /// Tier 2: tieng Anh.
    /// Tier 3: tieng Viet.
    /// </summary>
    public async Task<List<POI>> GetLocalizedPoisAsync(string langCode)
    {
        try
        {
            await EnsureInitializedAsync();

            var targetLang = NormalizeLanguageCode(langCode);
            var allPois = await _database!
                .Table<POI>()
                .OrderByDescending(x => x.Priority)
                .ToListAsync();

            var grouped = allPois
                .GroupBy(p => p.BasePoiId > 0 ? p.BasePoiId : p.Id)
                .ToList();

            var localized = new List<POI>();

            foreach (var group in grouped)
            {
                var variants = group.ToList();
                var selected = SelectByFallback(variants, targetLang);

                if (selected is null)
                {
                    continue;
                }

                localized.Add(CloneForDisplay(selected));
            }

            return localized
                .OrderByDescending(x => x.Priority)
                .ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the lay danh sach POI da localize.", ex);
        }
    }

    /// <summary>
    /// Đảm bảo database đã được khởi tạo trước khi thao tác CRUD.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (_database is null)
        {
            throw new InvalidOperationException("Database chua san sang.");
        }
    }

    private async Task ApplyServerChangesAsync(RemoteSyncResponse payload, CancellationToken cancellationToken)
    {
        var existingPois = await _database!.Table<POI>().ToListAsync();

        foreach (var remotePoi in payload.UpdatedPois)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var basePoiId = ParseBasePoiId(remotePoi);
            var localizations = remotePoi.Localizations ?? [];

            foreach (var localization in localizations)
            {
                var normalizedLang = NormalizeLanguageCode(localization.LanguageCode);
                var matched = existingPois.FirstOrDefault(x =>
                    x.BasePoiId == basePoiId &&
                    string.Equals(NormalizeLanguageCode(x.LanguageCode), normalizedLang, StringComparison.OrdinalIgnoreCase));

                if (matched is null)
                {
                    matched = new POI();
                    existingPois.Add(matched);
                }

                matched.BasePoiId = basePoiId;
                matched.Name = localization.Name?.Trim() ?? string.Empty;
                matched.Description = localization.Description?.Trim() ?? string.Empty;
                matched.Latitude = remotePoi.Latitude;
                matched.Longitude = remotePoi.Longitude;
                matched.Radius = remotePoi.Radius;
                matched.AudioPath = localization.AudioFile ?? string.Empty;
                matched.ImagePath = ResolveRemoteMediaPath(remotePoi.ImageUrl)
                    ?? (string.IsNullOrWhiteSpace(matched.ImagePath) ? "dotnet_bot.png" : matched.ImagePath);
                matched.LanguageCode = normalizedLang;
                matched.Priority = remotePoi.Priority;
                matched.Category = string.IsNullOrWhiteSpace(matched.Category)
                    ? InferCategory(localization.Name, localization.Description)
                    : matched.Category;
                matched.IsDownloaded = !string.IsNullOrWhiteSpace(matched.AudioPath);

                if (matched.Id > 0)
                {
                    await _database.UpdateAsync(matched);
                }
                else
                {
                    await _database.InsertAsync(matched);
                }
            }
        }

        foreach (var deletedId in payload.DeletedIds)
        {
            await _database.ExecuteAsync("DELETE FROM POI WHERE BasePoiId = ?", deletedId);
            await _database.ExecuteAsync("DELETE FROM POI WHERE Id = ?", deletedId);
        }
    }

    private static int ParseBasePoiId(RemotePoi remotePoi)
    {
        if (int.TryParse(remotePoi.BasePoiId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBasePoiId) &&
            parsedBasePoiId > 0)
        {
            return parsedBasePoiId;
        }

        return remotePoi.Id > 0 ? remotePoi.Id : 0;
    }

    private static string InferCategory(string? name, string? description)
    {
        var source = $"{name} {description}".ToLowerInvariant();
        if (source.Contains("oc") || source.Contains("oyster") || source.Contains("snail"))
        {
            return "Oyster";
        }

        if (source.Contains("bbq") || source.Contains("nuong") || source.Contains("lau") || source.Contains("hotpot"))
        {
            return "Bbq";
        }

        if (source.Contains("coffee") || source.Contains("ca phe") || source.Contains("drink") || source.Contains("beverage"))
        {
            return "Beverage";
        }

        return "All";
    }

    private static string? ResolveRemoteMediaPath(string? mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return null;
        }

        if (Uri.TryCreate(mediaPath, UriKind.Absolute, out var absoluteUri))
        {
            return NormalizeAndroidLoopbackUri(absoluteUri).ToString();
        }

        var baseUri = new Uri(AppConfig.BaseApiUrl, UriKind.Absolute);
        return new Uri(baseUri, mediaPath).ToString();
    }

    private static Uri NormalizeAndroidLoopbackUri(Uri uri)
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.Android)
        {
            return uri;
        }

        if (!string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Host = "10.0.2.2"
        };

        return builder.Uri;
    }

    private static DateTime GetLastSyncTime()
    {
        var stored = Preferences.Get(LastSyncPreferenceKey, string.Empty);
        if (DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return DateTime.MinValue;
    }

    private static void SaveLastSyncTime(DateTime serverTime)
    {
        Preferences.Set(LastSyncPreferenceKey, serverTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Kiểm tra dữ liệu đầu vào POI để tránh lưu dữ liệu không hợp lệ.
    /// </summary>
    private static void ValidatePoiInput(POI poi)
    {
        if (poi is null)
        {
            throw new ArgumentNullException(nameof(poi));
        }

        if (string.IsNullOrWhiteSpace(poi.Name))
        {
            throw new ArgumentException("Ten POI khong duoc de trong.", nameof(poi));
        }

        if (string.IsNullOrWhiteSpace(poi.LanguageCode))
        {
            throw new ArgumentException("LanguageCode khong duoc de trong.", nameof(poi));
        }

        if (poi.Radius < 0)
        {
            throw new ArgumentException("Radius khong the am.", nameof(poi));
        }
    }

    /// <summary>
    /// Xoa du lieu mau co san trong cac ban build cu de app chi hien thi du lieu dong bo tu backend.
    /// </summary>
    private async Task RemoveSeedPoisAsync()
    {
        await _database!.ExecuteAsync("DELETE FROM POI WHERE BasePoiId IN (1001, 1002, 1003)");
    }

    /// <summary>
    /// Dam bao schema co du cot can thiet cho gom nhom da ngon ngu.
    /// </summary>
    private async Task EnsureSchemaCompatibilityAsync()
    {
        try
        {
            await _database!.ExecuteAsync("ALTER TABLE POI ADD COLUMN BasePoiId INTEGER NOT NULL DEFAULT 0");
            Debug.WriteLine("[DatabaseService] Da bo sung cot BasePoiId");
        }
        catch (Exception ex)
        {
            // Neu cot da ton tai thi bo qua, tranh lam fail qua trinh khoi tao.
            Debug.WriteLine($"[DatabaseService] Skip migrate BasePoiId: {ex.Message}");
        }
    }

    /// <summary>
    /// Chuan hoa BasePoiId cho du lieu cu de cac ban dich cung quan duoc gom nhom dung.
    /// </summary>
    private async Task NormalizeBasePoiIdsAsync()
    {
        var allPois = await _database!.Table<POI>().ToListAsync();
        var grouped = allPois.GroupBy(BuildLegacyGroupKey);

        foreach (var group in grouped)
        {
            var groupList = group.ToList();
            var existingBase = groupList
                .Select(p => p.BasePoiId)
                .FirstOrDefault(x => x > 0);

            var effectiveBaseId = existingBase > 0
                ? existingBase
                : groupList.Min(p => p.Id);

            foreach (var poi in groupList)
            {
                if (poi.BasePoiId == effectiveBaseId)
                {
                    continue;
                }

                poi.BasePoiId = effectiveBaseId;
                await _database.UpdateAsync(poi);
            }
        }
    }

    private static string BuildLegacyGroupKey(POI poi)
    {
        if (poi.BasePoiId > 0)
        {
            return $"base:{poi.BasePoiId}";
        }

        var category = poi.Category?.Trim().ToLowerInvariant() ?? "all";
        var roundedLat = Math.Round(poi.Latitude, 4);
        var roundedLng = Math.Round(poi.Longitude, 4);
        return $"{category}:{roundedLat}:{roundedLng}";
    }

    private static POI? SelectByFallback(List<POI> variants, string targetLang)
    {
        var primary = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), targetLang, StringComparison.OrdinalIgnoreCase));
        if (primary is not null)
        {
            return primary;
        }

        var english = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), "en", StringComparison.OrdinalIgnoreCase));
        if (english is not null)
        {
            return english;
        }

        var vietnamese = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), "vi", StringComparison.OrdinalIgnoreCase));
        if (vietnamese is not null)
        {
            return vietnamese;
        }

        return variants
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault();
    }

    private static POI CloneForDisplay(POI source)
    {
        return new POI
        {
            Id = source.Id,
            BasePoiId = source.BasePoiId,
            Name = source.Name,
            Latitude = source.Latitude,
            Longitude = source.Longitude,
            Radius = source.Radius,
            Description = source.Description,
            AudioPath = source.AudioPath,
            ImagePath = source.ImagePath,
            LanguageCode = source.LanguageCode,
            Category = source.Category,
            Priority = source.Priority,
            IsDownloaded = source.IsDownloaded
        };
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "vi";
        }

        var normalized = languageCode.Trim().Replace('_', '-').ToLowerInvariant();
        var shortCode = normalized.Split('-')[0];
        return shortCode == "jp" ? "ja" : shortCode;
    }

    private sealed class RemoteSyncResponse
    {
        public List<RemotePoi> UpdatedPois { get; set; } = new();
        public List<int> DeletedIds { get; set; } = new();
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    }

    private sealed class RemotePoi
    {
        public int Id { get; set; }
        public string BasePoiId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } = 50;
        public int Priority { get; set; }
        public string? ImageUrl { get; set; }
        public List<RemotePoiLocalization> Localizations { get; set; } = new();
    }

    private sealed class RemotePoiLocalization
    {
        public string LanguageCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AudioFile { get; set; }
    }
}

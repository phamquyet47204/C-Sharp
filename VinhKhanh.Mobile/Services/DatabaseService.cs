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
/// DatabaseService: Dịch vụ quản lý cơ sở dữ liệu SQLite cục bộ (Offline Storage).
/// 
/// Vai trò:
/// - Lưu trữ thông tin POI (Point of Interest) đã đồng bộ từ Server để app hoạt động Offline.
/// - Thực hiện đồng bộ Delta (Delta-Sync) để cập nhật dữ liệu mới nhất từ backend SQL Server.
/// - Áp dụng cơ chế Fallback ngôn ngữ (Tiếng bản địa -> Tiếng Anh -> Tiếng Việt) tại tầng dữ liệu.
/// - Xử lý chuẩn hóa dữ liệu cũ (Legacy Data) và di chuyển Schema (Migration).
/// </summary>
public class DatabaseService : IDatabaseService
{
    private readonly string _databasePath;
    private readonly HttpClient _httpClient;
    
    // Kết nối Async tới SQLite (SQLite-net-pcl)
    private SQLiteAsyncConnection? _database;
    
    // Lock khởi tạo để tránh việc tạo bảng song song gây lỗi DB Locked
    private readonly SemaphoreSlim _initLock = new(1, 1);
    
    // Lock đồng bộ để đảm bảo chỉ có 1 tiến trình Sync chạy tại một thời điểm
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private bool _isInitialized;
    private const string UpdatesEndpoint = "api/pois/updates";
    private const string LastSyncPreferenceKey = "root_last_sync_utc";

    /// <summary>
    /// Khởi cấu hình DatabaseService.
    /// </summary>
    /// <param name="databasePath">Đường dẫn đầy đủ tới file .db3 trên bộ nhớ thiết bị.</param>
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
    /// Khởi tạo kết nối DB và tạo bảng.
    /// Bao gồm các bước dọn dẹp dữ liệu cũ và chuẩn hóa Schema nếu cần.
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
            
            // Tạo bảng POI dựa trên Model định nghĩa (Class-to-Table Mapping)
            await _database.CreateTableAsync<POI>();

            // One-time Reset: Xóa dữ liệu cũ nếu BasePoiId vẫn đang chứa dữ liệu dạng số sai lệch
            // (Chỉ áp dụng trong giai đoạn Migration này)
            var count = await _database.Table<POI>().CountAsync();
            if (count > 0)
            {
                var sample = await _database.Table<POI>().FirstOrDefaultAsync();
                if (sample != null && int.TryParse(sample.BasePoiId, out _))
                {
                    Debug.WriteLine("[DatabaseService] Detect legacy int BasePoiIds. Wiping for integrity.");
                    await _database.DeleteAllAsync<POI>();
                    Preferences.Remove(LastSyncPreferenceKey);
                }
            }
            
            // Migration: Đảm bảo Schema đủ các cột cần thiết cho các bản cập nhật mới
            await EnsureSchemaCompatibilityAsync();
            
            // Dọn dẹp: Xóa các dữ liệu mẫu (Seed) không còn cần thiết
            await RemoveSeedPoisAsync();
            
            // Chuẩn hóa: Gán BasePoiId cho các bản ghi cũ chưa có thông tin gom nhóm đa ngôn ngữ
            await NormalizeBasePoiIdsAsync();
            
            _isInitialized = true;

            var successMessage = $"[DatabaseService] Khoi tao SQLite thanh cong. DB Path: {_databasePath}";
            Debug.WriteLine(successMessage);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the khoi tao co so du lieu SQLite.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Thêm một POI mới vào Local DB.
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
    /// Cập nhật thông tin POI hiện có.
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
    /// Xóa POI khỏi LOCAL DB theo Id.
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
    /// Cơ chế Đồng bộ Delta (Delta Synchronization):
    /// 1. Lấy thời điểm đồng bộ thành công cuối cùng (LastSyncTime) từ Preference.
    /// 2. Gửi request lên Server để lấy các thay đổi (Update/Delete) kể từ thời điểm đó.
    /// 3. Cập nhật các bản ghi mới/sửa và xóa các bản ghi bị hủy bỏ trên Server vào SQLite cục bộ.
    /// 4. Lưu lại thời điểm máy chủ (ServerTime) làm mốc cho lần đồng bộ sau.
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
                
                // Sử dụng định dạng ISO "O" (Roundtrip) để đảm bảo độ chính xác của DateTime qua HTTP
                var requestUrl =
                    $"{UpdatesEndpoint}?lastSync={Uri.EscapeDataString(lastSync.ToString("O", CultureInfo.InvariantCulture))}";

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

                // Thực thi việc cập nhật các thay đổi vào DB
                await ApplyServerChangesAsync(payload, cancellationToken);
                
                // Lưu mốc thời gian đồng bộ
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
    /// Lấy toàn bộ danh sách POI thô (Raw Data) có trong cơ sở dữ liệu.
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
    /// Lấy danh sách POI đã được xử lý hiển thị:
    /// 1. Gom nhóm (Group by BasePoiId): gom nhiều bản dịch của cùng một quán vào một đại diện duy nhất.
    /// 2. Áp dụng Fallback: Chọn ngôn ngữ tốt nhất để hiển thị dựa trên yêu cầu của người dùng.
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

            // Nhóm các bản dịch dựa trên BasePoiId (Trường do Server định nghĩa)
            var grouped = allPois
                .GroupBy(p => !string.IsNullOrEmpty(p.BasePoiId) ? p.BasePoiId : p.Id.ToString())
                .ToList();

            var localized = new List<POI>();

            foreach (var group in grouped)
            {
                var variants = group.ToList();
                
                // Áp dụng thuật toán chọn lọc ngôn ngữ Fallback
                var selected = SelectByFallback(variants, targetLang);

                if (selected is null)
                {
                    continue;
                }

                // Sao chép sang Object mới để tránh side-effect khi hiển thị ở UI
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
    /// Đảm bảo kết nối DB sẵn sàng.
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

    /// <summary>
    /// Xử lý cập nhật các bản ghi từ Server vào Local.
    /// - Cập nhật thông tin thực tế (Kinh độ, Vĩ độ, Bán kính) cho từng bản ghi.
    /// - Đồng bộ các bản dịch Localization.
    /// - Xử lý xóa POI nếu Server báo ID đó đã bị hủy.
    /// </summary>
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
                
                // Tìm xem Local đã có bản dịch ngôn ngữ này cho Quán này chưa
                var matched = existingPois.FirstOrDefault(x =>
                    x.BasePoiId == basePoiId &&
                    string.Equals(NormalizeLanguageCode(x.LanguageCode), normalizedLang, StringComparison.OrdinalIgnoreCase));

                if (matched is null)
                {
                    matched = new POI();
                    existingPois.Add(matched);
                }

                // Cập nhật thông tin dùng chung từ remote POI
                matched.BasePoiId = basePoiId;
                matched.Latitude = remotePoi.Latitude;
                matched.Longitude = remotePoi.Longitude;
                matched.Radius = remotePoi.Radius;
                matched.Priority = remotePoi.Priority;
                
                // Ưu tiên CategoryCode do Server quy định, nếu không có thì tự suy luận từ Text Content
                matched.Category = !string.IsNullOrWhiteSpace(remotePoi.CategoryCode)
                    ? remotePoi.CategoryCode
                    : InferCategory(localization.Name, localization.Description);

                // Cập nhật thông tin bản dịch cụ thể
                matched.Name = localization.Name?.Trim() ?? string.Empty;
                matched.Description = localization.Description?.Trim() ?? string.Empty;
                matched.AudioPath = localization.AudioFile ?? string.Empty;
                matched.LanguageCode = normalizedLang;
                
                // Chuẩn hóa đường dẫn hình ảnh (Hỗ trợ URL tuyệt đối hoặc tương đối)
                matched.ImagePath = ResolveRemoteMediaPath(remotePoi.ImageUrl) ?? matched.ImagePath;
                
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

        // Thực hiện xóa các POI mà Server báo đã bị gỡ (Legacy DeletedIds support)
        foreach (var deletedId in payload.DeletedIds)
        {
            await _database.ExecuteAsync("DELETE FROM POI WHERE BasePoiId = ? OR Id = ?", deletedId, deletedId);
        }

        // PRUNING: Xóa các POI cục bộ không còn nằm trong danh sách Active của Server
        try
        {
            if (payload.ActiveBasePoiIds != null && payload.ActiveBasePoiIds.Count > 0)
            {
                var localPois = await _database.Table<POI>().ToListAsync();
                var toDelete = localPois.Where(lp => !payload.ActiveBasePoiIds.Contains(lp.BasePoiId)).ToList();

                foreach (var poi in toDelete)
                {
                    Debug.WriteLine($"[DatabaseService] Pruning stale POI: BasePoiId={poi.BasePoiId}");
                    await _database.DeleteAsync(poi);
                }
            }
        }
        catch (Exception pruneEx)
        {
            Debug.WriteLine($"[DatabaseService] Critical error during pruning: {pruneEx.Message}");
        }
    }

    /// <summary>
    /// Trình phân tích BasePoiId: Đảm bảo lấy ID gốc của quán để gom nhóm.
    /// Tra về chuỗi (Hex hoăc ID) để đồng nhất với Server.
    /// </summary>
    private static string ParseBasePoiId(RemotePoi remotePoi)
    {
        if (!string.IsNullOrEmpty(remotePoi.BasePoiId))
        {
            return remotePoi.BasePoiId;
        }

        return remotePoi.Id.ToString();
    }

    /// <summary>
    /// Logic suy luận Category dữ liệu dựa trên nội dung mô tả (AI-like Inference).
    /// Dùng làm Fallback khi Server không cung cấp CategoryCode cụ thể.
    /// </summary>
    private static string InferCategory(string? name, string? description)
    {
        var source = $"{name} {description}".ToLowerInvariant();
        if (source.Contains("oc") || source.Contains("oyster") || source.Contains("snail") || source.Contains("hai san"))
        {
            return "FOOD_SNAIL";
        }

        if (source.Contains("bbq") || source.Contains("nuong") || source.Contains("lau") || source.Contains("hotpot"))
        {
            return "FOOD_BBQ";
        }

        if (source.Contains("coffee") || source.Contains("ca phe") || source.Contains("drink") || source.Contains("beverage") || source.Contains("nuoc"))
        {
            return "DRINK";
        }

        if (source.Contains("street") || source.Contains("vat") || source.Contains("snack"))
        {
            return "FOOD_STREET";
        }

        return "FOOD_STREET";
    }

    /// <summary>
    /// Chuyển đổi đường dẫn media từ Server thành URL hoàn chỉnh có thể truy cập được từ Mobile.
    /// Xử lý đặc thù cho Android Emulator khi trỏ vào localhost (127.0.0.1 -> 10.0.2.2).
    /// </summary>
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

        // Nếu là đường dẫn tương đối, ghép với Base Address của API
        var baseUri = new Uri(AppConfig.BaseApiUrl, UriKind.Absolute);
        return new Uri(baseUri, mediaPath).ToString();
    }

    /// <summary>
    /// Hack Android Loopback: Android Emulator không hiểu localhost là máy Host, 
    /// cần chuyển thành IP đặc biệt 10.0.2.2.
    /// </summary>
    private static Uri NormalizeAndroidLoopbackUri(Uri uri)
    {
        if (string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/media/", StringComparison.OrdinalIgnoreCase))
        {
            return BuildBackendMediaUri(uri.AbsolutePath);
        }

        // Chỉ áp dụng cho môi trường DI của Android Emulator.
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

    private static Uri BuildBackendMediaUri(string mediaPath)
    {
        var baseUri = new Uri(AppConfig.BaseApiUrl, UriKind.Absolute);
        return new Uri(baseUri, mediaPath.TrimStart('/'));
    }

    /// <summary>
    /// Đọc mốc thời gian đồng bộ thành công gần nhất.
    /// </summary>
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
    /// Xác thực dữ liệu POI trước khi ghi xuống đĩa để tránh hỏng DB (Data Integrity).
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
    /// Dọn dẹp dữ liệu rác từ các phiên bản thử nghiệm trước đó.
    /// </summary>
    private async Task RemoveSeedPoisAsync()
    {
        await _database!.ExecuteAsync("DELETE FROM POI WHERE BasePoiId IN (1001, 1002, 1003)");
    }

    /// <summary>
    /// Nâng cấp Schema DB: Thêm cột BasePoiId nếu nó chưa tồn tại mà không làm mất dữ liệu cũ.
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
            // SQLite không hỗ trợ IF NOT EXISTS cho cột, nên ta dựa vào Exception để biết cột đã tồn tại.
            Debug.WriteLine($"[DatabaseService] Skip migrate BasePoiId (Already exists): {ex.Message}");
        }
    }

    /// <summary>
    /// Logic sửa lỗi dữ liệu (Data Healing): 
    /// Tự động gán BasePoiId cho các bản ghi đơn lẻ dựa trên vị trí địa lý (Lat/Lng) và Category 
    /// để App có thể gom nhóm đa ngôn ngữ cho các dữ liệu cũ.
    /// </summary>
    private async Task NormalizeBasePoiIdsAsync()
    {
        var allPois = await _database!.Table<POI>().ToListAsync();
        var grouped = allPois.GroupBy(BuildLegacyGroupKey);

        foreach (var group in grouped)
        {
            var groupList = group.ToList();
            var existingBase = groupList.Select(p => p.BasePoiId).FirstOrDefault(x => !string.IsNullOrEmpty(x));

            var effectiveBaseId = !string.IsNullOrEmpty(existingBase) ? existingBase : groupList.Min(p => p.Id).ToString();

            foreach (var poi in groupList)
            {
                if (poi.BasePoiId == effectiveBaseId) continue;

                poi.BasePoiId = effectiveBaseId;
                await _database.UpdateAsync(poi);
            }
        }
    }

    private static string BuildLegacyGroupKey(POI poi)
    {
        if (!string.IsNullOrEmpty(poi.BasePoiId)) return $"base:{poi.BasePoiId}";

        var category = poi.Category?.Trim().ToLowerInvariant() ?? "all";
        // Làm tròn 4 chữ số thập phân (độ chính xác ~10m) để gom nhóm các quán cùng vị trí
        var roundedLat = Math.Round(poi.Latitude, 4);
        var roundedLng = Math.Round(poi.Longitude, 4);
        return $"{category}:{roundedLat}:{roundedLng}";
    }

    /// <summary>
    /// Thuật toán Chọn lọc Ngôn ngữ (Language Fallback Algorithm):
    /// 1. Tìm bản dịch trùng khớp hoàn toàn với ngôn ngữ yêu cầu (targetLang).
    /// 2. Nếu không có, tìm bản dịch Tiếng Anh (en).
    /// 3. Nếu không có, tìm bản dịch Tiếng Việt (vi - Mặc định của dự án).
    /// 4. Cuối cùng, lấy bất kỳ bản dịch nào có độ ưu tiên (Priority) cao nhất.
    /// </summary>
    private static POI? SelectByFallback(List<POI> variants, string targetLang)
    {
        // Tier 1: Ưu tiên ngôn ngữ đích
        var primary = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), targetLang, StringComparison.OrdinalIgnoreCase));
        if (primary is not null) return primary;

        // Tier 2: Tiếng Anh là ngôn ngữ trung gian phổ biến nhất
        var english = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), "en", StringComparison.OrdinalIgnoreCase));
        if (english is not null) return english;

        // Tier 3: Tiếng Việt là gốc của hệ thống
        var vietnamese = variants.FirstOrDefault(p =>
            string.Equals(NormalizeLanguageCode(p.LanguageCode), "vi", StringComparison.OrdinalIgnoreCase));
        if (vietnamese is not null) return vietnamese;

        // Cuối cùng: Lấy bản ghi có trọng số cao nhất
        return variants.OrderByDescending(p => p.Priority).FirstOrDefault();
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

    /// <summary>
    /// Chuẩn hóa mã ngôn ngữ về dạng 2 ký tự (ISO 639-1).
    /// Ví dụ: vi-VN -> vi, ja-JP -> ja, jp -> ja.
    /// </summary>
    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return "vi";

        var normalized = languageCode.Trim().Replace('_', '-').ToLowerInvariant();
        var shortCode = normalized.Split('-')[0];
        return shortCode == "jp" ? "ja" : shortCode;
    }

    // Các lớp nội bộ phục vụ việc ánh xạ dữ liệu nhận được từ JSON API
    private sealed class RemoteSyncResponse
    {
        public List<RemotePoi> UpdatedPois { get; set; } = new();
        public List<int> DeletedIds { get; set; } = new();
        public List<string> ActiveBasePoiIds { get; set; } = new();
        public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    }

    private sealed class RemotePoi
    {
        public int Id { get; set; }
        public string BasePoiId { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = string.Empty;
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

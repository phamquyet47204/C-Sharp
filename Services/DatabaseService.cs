using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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
    private SQLiteAsyncConnection? _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    /// <summary>
    /// Nhận đường dẫn database từ DI để dễ cấu hình theo từng môi trường.
    /// </summary>
    /// <param name="databasePath">Đường dẫn file SQLite (.db3).</param>
    public DatabaseService(string databasePath)
    {
        _databasePath = databasePath;
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
            await EnsureDefaultPoisAsync();
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
    /// Lấy danh sách POI theo mã ngôn ngữ người dùng chọn.
    /// Kết quả được sắp xếp theo Priority giảm dần để ưu tiên điểm quan trọng.
    /// </summary>
    public async Task<List<POI>> GetPoisByLanguage(string lang)
    {
        try
        {
            await EnsureInitializedAsync();

            if (string.IsNullOrWhiteSpace(lang))
            {
                throw new ArgumentException("Ngon ngu khong duoc de trong.", nameof(lang));
            }

            return await _database!
                .Table<POI>()
                .Where(x => x.LanguageCode == lang)
                .OrderByDescending(x => x.Priority)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Khong the lay danh sach POI theo ngon ngu.", ex);
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
    /// Dam bao bo du lieu mau mac dinh luon day du.
    /// Truong hop DB cu da co 1 vai ban ghi, ham van bo sung cac POI con thieu.
    /// </summary>
    private async Task EnsureDefaultPoisAsync()
    {
        var samplePois = new List<POI>
        {
            // ========== OYSTER RESTAURANTS ==========
            new POI
            {
                Name = "Quán Ốc Oanh",
                Latitude = 10.756895449216689,
                Longitude = 106.6740947680869,
                Radius = 30,
                Description = "Quán ốc nổi tiếng khu phố ẩm thực Vĩnh Khánh. Ốc tươi ngon, giá cả hợp lý.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/vi/quan-oc-oanh.mp3",
                LanguageCode = "vi",
                Category = "Oyster",
                Priority = 100,
                IsDownloaded = false
            },
            new POI
            {
                Name = "Sea Snail Restaurant",
                Latitude = 10.756895449216689,
                Longitude = 106.6740947680869,
                Radius = 30,
                Description = "Fresh oysters and snails from the sea. Best restaurant in the area.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/en/sea-snail.mp3",
                LanguageCode = "en",
                Category = "Oyster",
                Priority = 95,
                IsDownloaded = false
            },
            new POI
            {
                Name = "カキのレストラン",
                Latitude = 10.7569,
                Longitude = 106.6741,
                Radius = 30,
                Description = "新鮮でおいしいカキとニシン。地域で最高のレストラン。",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/ja/kaki-restaurant.mp3",
                LanguageCode = "ja",
                Category = "Oyster",
                Priority = 90,
                IsDownloaded = false
            },

            // ========== BBQ & HOTPOT ==========
            new POI
            {
                Name = "Lẩu & Nướng Sài Gòn",
                Latitude = 10.758,
                Longitude = 106.675,
                Radius = 25,
                Description = "Lẩu và nướng chất lượng cao. Tươi ngon mỗi ngày. Đặc biệt là thịt bò Úc.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/vi/lau-nuong.mp3",
                LanguageCode = "vi",
                Category = "Bbq",
                Priority = 85,
                IsDownloaded = false
            },
            new POI
            {
                Name = "Hotpot Hanoi",
                Latitude = 10.7585,
                Longitude = 106.6755,
                Radius = 25,
                Description = "Traditional Vietnamese hotpot with premium imported beef and seafood.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/en/hotpot-hanoi.mp3",
                LanguageCode = "en",
                Category = "Bbq",
                Priority = 80,
                IsDownloaded = false
            },
            new POI
            {
                Name = "焼肉屋トウキョウ",
                Latitude = 10.759,
                Longitude = 106.676,
                Radius = 25,
                Description = "日本式焼肉としゃぶしゃぶ。高品質な和牛を使用しています。",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/ja/yakiniku-tokyo.mp3",
                LanguageCode = "ja",
                Category = "Bbq",
                Priority = 75,
                IsDownloaded = false
            },

            // ========== BEVERAGES & COFFEE ==========
            new POI
            {
                Name = "Cà Phê Vĩnh Khánh",
                Latitude = 10.757,
                Longitude = 106.674,
                Radius = 20,
                Description = "Cà phê truyền thống Việt với không gian yên tĩnh, ổn định.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/vi/ca-phe-vinh-khanh.mp3",
                LanguageCode = "vi",
                Category = "Beverage",
                Priority = 70,
                IsDownloaded = false
            },
            new POI
            {
                Name = "Sweet Dreams Coffee",
                Latitude = 10.7575,
                Longitude = 106.6745,
                Radius = 20,
                Description = "Premium coffee and tropical drinks. Perfect for relaxing and studying.",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/en/sweet-dreams.mp3",
                LanguageCode = "en",
                Category = "Beverage",
                Priority = 65,
                IsDownloaded = false
            },
            new POI
            {
                Name = "ドリームカフェ",
                Latitude = 10.758,
                Longitude = 106.6763,
                Radius = 20,
                Description = "最高の日本式コーヒーと地元の飲料。居心地の良い雰囲気。",
                ImagePath = "dotnet_bot.png",
                AudioPath = "audio/ja/dream-cafe.mp3",
                LanguageCode = "ja",
                Category = "Beverage",
                Priority = 60,
                IsDownloaded = false
            }
        };

        var existingPois = await _database!.Table<POI>().ToListAsync();
        var inserted = 0;

        foreach (var poi in samplePois)
        {
            var isExisting = existingPois.Any(x =>
                string.Equals(x.Name, poi.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.LanguageCode, poi.LanguageCode, StringComparison.OrdinalIgnoreCase));

            if (isExisting)
            {
                continue;
            }

            await _database.InsertAsync(poi);
            inserted++;
        }

        var totalAfter = await _database.Table<POI>().CountAsync();
        Debug.WriteLine($"[DatabaseService] Bo sung {inserted} POI mau, tong hien tai: {totalAfter}");
        Console.WriteLine($"[DatabaseService] Bo sung {inserted} POI mau, tong hien tai: {totalAfter}");
    }
}

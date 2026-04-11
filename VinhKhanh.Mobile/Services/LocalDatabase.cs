using SQLite;
using VinhKhanh.Mobile.Models;

namespace VinhKhanh.Mobile.Services;

public class LocalDatabase
{
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public LocalDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<PoiRecord>().Wait();
        _db.CreateTableAsync<NarrationEvent>().Wait();
    }

    public async Task<List<PoiRecord>> GetActivePoisAsync()
    {
        await _dbLock.WaitAsync();
        try
        {
            return await _db.Table<PoiRecord>().Where(p => p.IsActive).OrderByDescending(p => p.Priority).ToListAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

<<<<<<< HEAD
=======
    /// <summary>
    /// Lấy POI theo ngôn ngữ ưu tiên. Fallback: vi → en → bất kỳ.
    /// Mỗi BasePoiId chỉ trả về 1 bản ghi theo ngôn ngữ phù hợp nhất.
    /// </summary>
    public async Task<List<PoiRecord>> GetActivePoisByLanguageAsync(string languageCode)
    {
        await _dbLock.WaitAsync();
        try
        {
            var all = await _db.Table<PoiRecord>().Where(p => p.IsActive).ToListAsync();

            // Chuẩn hóa: "vi-VN" → "vi", "en-US" → "en", "ja-JP" → "ja"
            var lang = languageCode.Split('-')[0].ToLowerInvariant();
            var fallbackChain = lang switch
            {
                "en" => new[] { "en", "vi", "ja" },
                "ja" => new[] { "ja", "vi", "en" },
                _    => new[] { "vi", "en", "ja" }
            };

            var grouped = all.GroupBy(p => p.BasePoiId);
            var result = new List<PoiRecord>();

            foreach (var group in grouped)
            {
                PoiRecord? chosen = null;
                foreach (var targetLang in fallbackChain)
                {
                    chosen = group.FirstOrDefault(p =>
                        p.LanguageCode.Split('-')[0].Equals(targetLang, StringComparison.OrdinalIgnoreCase));
                    if (chosen is not null) break;
                }
                chosen ??= group.First();
                result.Add(chosen);
            }

            return result.OrderByDescending(p => p.Priority).ToList();
        }
        finally
        {
            _dbLock.Release();
        }
    }

>>>>>>> bb1d8ae5 (feat: UI improvements, device trial, category fix, pull-to-refresh, map pin card)
    public async Task<PoiRecord?> GetPoiByIdAsync(int poiId)
    {
        await _dbLock.WaitAsync();
        try
        {
            return await _db.Table<PoiRecord>().Where(p => p.Id == poiId).FirstOrDefaultAsync();
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task UpsertPoisAsync(IEnumerable<PoiRecord> pois)
    {
        await _dbLock.WaitAsync();
        try
        {
            foreach (var poi in pois)
            {
                await _db.InsertOrReplaceAsync(poi);
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task DeletePoisAsync(IEnumerable<int> ids)
    {
        await _dbLock.WaitAsync();
        try
        {
            foreach (var id in ids)
            {
                var rows = await _db.Table<PoiRecord>()
                    .Where(p => p.BasePoiId == id)
                    .ToListAsync();

                foreach (var row in rows)
                {
                    await _db.DeleteAsync(row);
                }
            }
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task LogNarrationAsync(NarrationEvent e)
    {
        await _dbLock.WaitAsync();
        try
        {
            await _db.InsertAsync(e);
        }
        finally
        {
            _dbLock.Release();
        }
    }
}

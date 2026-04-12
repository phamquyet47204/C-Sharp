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

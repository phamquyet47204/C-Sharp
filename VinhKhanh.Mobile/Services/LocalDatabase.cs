using SQLite;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Services;

public class LocalDatabase
{
    private readonly SQLiteAsyncConnection _db;

    public LocalDatabase(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Poi>().Wait();
        _db.CreateTableAsync<NarrationEvent>().Wait();
    }

    public Task<List<Poi>> GetActivePoisAsync() =>
        _db.Table<Poi>().Where(p => p.IsActive).OrderByDescending(p => p.Priority).ToListAsync();

    public async Task UpsertPoisAsync(IEnumerable<Poi> pois)
    {
        foreach (var poi in pois)
            await _db.InsertOrReplaceAsync(poi);
    }

    public async Task DeletePoisAsync(IEnumerable<int> ids)
    {
        foreach (var id in ids)
            await _db.DeleteAsync<Poi>(id);
    }

    public Task<DateTime> GetLastSyncTimeAsync() =>
        _db.Table<NarrationEvent>()
           .OrderByDescending(e => e.TriggeredAt)
           .FirstOrDefaultAsync()
           .ContinueWith(t => t.Result?.TriggeredAt ?? DateTime.MinValue);

    public Task LogNarrationAsync(NarrationEvent e) => _db.InsertAsync(e);
}

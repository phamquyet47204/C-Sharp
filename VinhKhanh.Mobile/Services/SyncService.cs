using System.Net.Http.Json;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Services;

public class SyncService(HttpClient http, LocalDatabase db)
{
    private const string BaseUrl = "https://your-api.azurewebsites.net";

    public async Task SyncIfConnectedAsync()
    {
        if (Connectivity.NetworkAccess != NetworkAccess.Internet) return;

        var lastSync = await db.GetLastSyncTimeAsync();
        var response = await http.PostAsJsonAsync($"{BaseUrl}/api/sync",
            new SyncRequest { LastSyncAt = lastSync });

        if (!response.IsSuccessStatusCode) return;

        var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
        if (result is null) return;

        await db.UpsertPoisAsync(result.UpdatedPois);
        await db.DeletePoisAsync(result.DeletedIds);
    }
}

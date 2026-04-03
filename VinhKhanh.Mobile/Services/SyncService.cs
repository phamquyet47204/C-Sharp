using System.Globalization;
using System.Net.Http.Json;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using VinhKhanh.Mobile.Models;
using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Services;

public sealed record SyncOutcome(bool Success, bool TextOnlyMode, string Message);

public class SyncService(HttpClient http, LocalDatabase db)
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private const string LastSyncPreferenceKey = "last_sync_utc";
    private const long LowStorageThresholdBytes = 200L * 1024 * 1024;
    private const string UpdatesEndpoint = "api/pois/updates";

    public async Task<SyncOutcome> SyncIfConnectedAsync()
    {
        await _syncLock.WaitAsync();
        try
        {
            if (Connectivity.NetworkAccess != NetworkAccess.Internet)
            {
                return new SyncOutcome(false, false, string.Empty);
            }

            var freeSpaceBytes = GetFreeSpaceBytes();
            var textOnlyMode = freeSpaceBytes > 0 && freeSpaceBytes < LowStorageThresholdBytes;
            var includeAudio = !textOnlyMode;

            var lastSync = GetLastSyncTime();
            var requestUrl = $"{UpdatesEndpoint}?lastSync={Uri.EscapeDataString(lastSync.ToString("O", CultureInfo.InvariantCulture))}&includeAudio={includeAudio.ToString().ToLowerInvariant()}";
            var response = await http.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                return new SyncOutcome(false, textOnlyMode, string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<SyncResponse>();
            if (result is null)
            {
                return new SyncOutcome(false, textOnlyMode, string.Empty);
            }

            var mappedPois = MapToLocalPois(result.UpdatedPois, includeAudio);

            await db.UpsertPoisAsync(mappedPois);
            await db.DeletePoisAsync(result.DeletedIds);

            SaveLastSyncTime(result.ServerTime);

            if (textOnlyMode)
            {
                return new SyncOutcome(true, true, "Dung lượng trống dưới 200MB. Hệ thống đã chỉ đồng bộ dữ liệu văn bản và sẽ dùng TTS thay cho MP3.");
            }

            return new SyncOutcome(true, false, string.Empty);
        }
        finally
        {
            _syncLock.Release();
        }
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

    private static long GetFreeSpaceBytes()
    {
#if ANDROID
        var appDataPath = FileSystem.AppDataDirectory;
        var statFs = new Android.OS.StatFs(appDataPath);
        return statFs.AvailableBytes;
#else
        return long.MaxValue;
#endif
    }

    private List<PoiRecord> MapToLocalPois(IEnumerable<Poi> remotePois, bool includeAudio)
    {
        var localPois = new List<PoiRecord>();

        foreach (var remotePoi in remotePois)
        {
            var localizations = remotePoi.Localizations ?? [];
            var imagePath = BuildRemoteMediaUrl(remotePoi.ImageUrl);

            foreach (var localization in localizations)
            {
                var languageSlot = GetLanguageSlot(localization.LanguageCode);
                var localId = (remotePoi.Id * 10) + languageSlot;

                localPois.Add(new PoiRecord
                {
                    Id = localId,
                    BasePoiId = remotePoi.Id,
                    Name = localization.Name,
                    Latitude = remotePoi.Latitude,
                    Longitude = remotePoi.Longitude,
                    Radius = remotePoi.Radius,
                    Description = localization.Description,
                    AudioPath = includeAudio ? localization.AudioFile ?? string.Empty : string.Empty,
                    ImagePath = imagePath,
                    LanguageCode = localization.LanguageCode,
                    Category = string.Empty,
                    Priority = remotePoi.Priority,
                    IsDownloaded = includeAudio && !string.IsNullOrWhiteSpace(localization.AudioFile),
                    IsActive = remotePoi.IsActive,
                    UpdatedAt = remotePoi.UpdatedAt
                });
            }
        }

        return localPois;
    }

    private string BuildRemoteMediaUrl(string? mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(mediaPath, UriKind.Absolute, out var absoluteUri))
        {
            return NormalizeAndroidLoopbackUri(absoluteUri).ToString();
        }

        var baseAddress = http.BaseAddress;
        if (baseAddress is null)
        {
            return mediaPath;
        }

        return new Uri(baseAddress, mediaPath).ToString();
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

    private static int GetLanguageSlot(string? languageCode) => languageCode?.Trim().ToLowerInvariant() switch
    {
        "vi" => 1,
        "en" => 2,
        "ja" => 3,
        "ko" => 4,
        _ => 9
    };
}

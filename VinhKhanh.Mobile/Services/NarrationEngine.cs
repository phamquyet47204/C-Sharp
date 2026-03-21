using VinhKhanh.Shared.Models;

namespace VinhKhanh.Mobile.Services;

/// <summary>
/// Manages narration queue, cooldown, TTS/audio playback.
/// </summary>
public class NarrationEngine(LocalDatabase db)
{
    private readonly Queue<Poi> _queue = new();
    private readonly Dictionary<int, DateTime> _cooldowns = new(); // poiId -> last played
    private readonly TimeSpan _cooldownDuration = TimeSpan.FromMinutes(20);
    private bool _isPlaying;

    public async Task EnqueueAsync(Poi poi)
    {
        if (IsOnCooldown(poi.Id)) return;
        if (_queue.Any(p => p.Id == poi.Id)) return; // already queued

        _queue.Enqueue(poi);
        if (!_isPlaying) await PlayNextAsync();
    }

    private bool IsOnCooldown(int poiId) =>
        _cooldowns.TryGetValue(poiId, out var last) &&
        DateTime.UtcNow - last < _cooldownDuration;

    private async Task PlayNextAsync()
    {
        if (_queue.Count == 0) { _isPlaying = false; return; }

        _isPlaying = true;
        var poi = _queue.Dequeue();

        _cooldowns[poi.Id] = DateTime.UtcNow;
        await db.LogNarrationAsync(new NarrationEvent { PoiId = poi.Id });

        if (!string.IsNullOrEmpty(poi.AudioFile))
            await PlayAudioAsync(poi.AudioFile);
        else
            await SpeakAsync(poi.Description);

        await PlayNextAsync(); // chain next
    }

    private static async Task SpeakAsync(string text)
    {
        var locale = await GetLocaleAsync();
        await TextToSpeech.SpeakAsync(text, new SpeechOptions { Locale = locale });
    }

    private static async Task<Locale?> GetLocaleAsync()
    {
        var locales = await TextToSpeech.GetLocalesAsync();
        var lang = Preferences.Get("language", "vi-VN");
        return locales.FirstOrDefault(l => l.Language.StartsWith(lang[..2]));
    }

    private static Task PlayAudioAsync(string filePath)
    {
        // Platform-specific audio player (MediaElement / AVAudioPlayer)
        // Placeholder: replace with MediaElement from MAUI Community Toolkit
        return Task.CompletedTask;
    }
}

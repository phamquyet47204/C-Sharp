using VinhKhanh.Mobile.Models;

namespace VinhKhanh.Mobile.Services;

/// <summary>
/// Manages narration queue, cooldown, TTS/audio playback.
/// Supports: language auto-detect per POI, pitch/rate from Preferences, skip/clear queue.
/// </summary>
public class NarrationEngine(LocalDatabase db)
{
    private readonly Queue<PoiRecord> _queue = new();
    private readonly Dictionary<int, DateTime> _cooldowns = new();
    private readonly TimeSpan _cooldownDuration = TimeSpan.FromMinutes(20);
    private bool _isPlaying;
    private CancellationTokenSource? _speakCts;

    private const float PitchMin = 0.5f;
    private const float PitchMax = 2.0f;
    private const float RateMin = 0.5f;
    private const float RateMax = 2.0f;
    private const float DefaultValue = 1.0f;

    /// <summary>Phát sự kiện khi hàng chờ thay đổi (enqueue, skip, clear).</summary>
    public event Action? QueueChanged;

    /// <summary>Danh sách POI đang trong hàng chờ (read-only cho UI binding).</summary>
    public IReadOnlyList<PoiRecord> CurrentQueue => _queue.ToList().AsReadOnly();

    /// <summary>POI đang được đọc thuyết minh.</summary>
    public PoiRecord? CurrentlyPlaying { get; private set; }

    /// <summary>Trạng thái đang phát.</summary>
    public bool IsPlaying => _isPlaying;

    public async Task EnqueueAsync(PoiRecord poi)
    {
        if (IsOnCooldown(poi.Id)) return;
        if (_queue.Any(p => p.Id == poi.Id)) return;

        _queue.Enqueue(poi);
        QueueChanged?.Invoke();

        if (!_isPlaying) await PlayNextAsync();
    }

    /// <summary>Bỏ qua POI hiện tại và chuyển sang POI tiếp theo trong hàng chờ.</summary>
    public async Task SkipCurrentAsync()
    {
        if (!_isPlaying && _queue.Count == 0) return;

        _speakCts?.Cancel();
        await Task.Delay(50); // Cho TTS dừng
        CurrentlyPlaying = null;
        _isPlaying = false;

        QueueChanged?.Invoke();

        if (_queue.Count > 0)
            await PlayNextAsync();
    }

    /// <summary>Xóa toàn bộ hàng chờ và dừng thuyết minh hiện tại.</summary>
    public async Task ClearQueueAsync()
    {
        _speakCts?.Cancel();
        await Task.Delay(50);
        _queue.Clear();
        CurrentlyPlaying = null;
        _isPlaying = false;
        QueueChanged?.Invoke();
    }

    private bool IsOnCooldown(int poiId) =>
        _cooldowns.TryGetValue(poiId, out var last) &&
        DateTime.UtcNow - last < _cooldownDuration;

    private async Task PlayNextAsync()
    {
        if (_queue.Count == 0) { _isPlaying = false; CurrentlyPlaying = null; QueueChanged?.Invoke(); return; }

        _isPlaying = true;
        var poi = _queue.Dequeue();
        CurrentlyPlaying = poi;
        QueueChanged?.Invoke();

        _cooldowns[poi.Id] = DateTime.UtcNow;
        await db.LogNarrationAsync(new NarrationEvent { PoiId = poi.Id, TriggeredAt = DateTime.UtcNow });

        _speakCts = new CancellationTokenSource();
        try
        {
            if (!string.IsNullOrEmpty(poi.AudioPath))
                await PlayAudioAsync(poi.AudioPath);
            else
                await SpeakAsync(poi.Description, poi.LanguageCode, _speakCts.Token);
        }
        catch (OperationCanceledException) { /* skip/clear triggered */ }
        finally
        {
            _speakCts?.Dispose();
            _speakCts = null;
        }

        await PlayNextAsync();
    }

    private static async Task SpeakAsync(string text, string? languageCode, CancellationToken ct)
    {
        var locale = await ResolveLocaleAsync(languageCode);
        var pitch = ClampTtsValue(Preferences.Get("tts_pitch", DefaultValue));
        var rate = ClampTtsValue(Preferences.Get("tts_rate", DefaultValue));

        await TextToSpeech.SpeakAsync(text, new SpeechOptions
        {
            Locale = locale,
            Pitch = pitch,
            Volume = 1.0f
        });
    }

    /// <summary>
    /// Chọn locale TTS theo thứ tự ưu tiên:
    /// 1. LanguageCode của POI
    /// 2. Preferences "language"
    /// 3. System default (null)
    /// </summary>
    private static async Task<Locale?> ResolveLocaleAsync(string? languageCode)
    {
        var locales = await TextToSpeech.GetLocalesAsync();

        // 1. Ưu tiên LanguageCode của POI
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var match = locales.FirstOrDefault(l =>
                l.Language.StartsWith(languageCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        // 2. Fallback về Preferences
        var prefLang = Preferences.Get("language", "vi-VN");
        var prefMatch = locales.FirstOrDefault(l =>
            l.Language.StartsWith(prefLang[..2], StringComparison.OrdinalIgnoreCase));
        if (prefMatch is not null) return prefMatch;

        // 3. System default
        return null;
    }

    /// <summary>Clamp giá trị Pitch/Rate về [0.5, 2.0]; nếu ngoài khoảng trả về 1.0.</summary>
    private static float ClampTtsValue(float value) =>
        value >= PitchMin && value <= PitchMax ? value : DefaultValue;

    private static Task PlayAudioAsync(string filePath)
    {
        // Platform-specific audio player (MediaElement / AVAudioPlayer)
        return Task.CompletedTask;
    }
}

// Feature: vinh-khanh-tts-missing-features, Property 15: NarrationEngine chọn locale TTS theo LanguageCode của POI
// Feature: vinh-khanh-tts-missing-features, Property 16: Pitch và Rate ngoài khoảng [0.5, 2.0] được thay bằng 1.0
// Feature: vinh-khanh-tts-missing-features, Property 17: SpeakAsync nhận đúng Pitch và Rate từ Preferences
// Feature: vinh-khanh-tts-missing-features, Property 18: SkipCurrentAsync giảm queue và chuyển sang item tiếp theo
// Feature: vinh-khanh-tts-missing-features, Property 19: ClearQueueAsync luôn tạo ra hàng chờ rỗng
// Feature: vinh-khanh-tts-missing-features, Property 20: QueueChanged event được phát cho mọi thao tác thay đổi queue

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace VinhKhanh.Tests;

/// <summary>
/// Testable queue that mirrors NarrationEngine's queue logic without MAUI dependencies.
/// </summary>
public class TestableNarrationQueue
{
    private readonly Queue<int> _queue = new();
    public int QueueChangedCount { get; private set; }
    public event Action? QueueChanged;

    public void Enqueue(int poiId)
    {
        if (_queue.Contains(poiId)) return;
        _queue.Enqueue(poiId);
        QueueChanged?.Invoke();
        QueueChangedCount++;
    }

    public void Skip()
    {
        if (_queue.Count == 0) return;
        _queue.Dequeue();
        QueueChanged?.Invoke();
        QueueChangedCount++;
    }

    public void Clear()
    {
        _queue.Clear();
        QueueChanged?.Invoke();
        QueueChangedCount++;
    }

    public int Count => _queue.Count;
    public IReadOnlyList<int> Items => _queue.ToList().AsReadOnly();
}

/// <summary>
/// Property tests 15–20 for NarrationEngine pure logic.
/// MAUI APIs (TextToSpeech, Preferences) are not available in test projects,
/// so all logic is tested via extracted pure functions and TestableNarrationQueue.
/// </summary>
public class NarrationEngine_Property15_20_Tests
{
    // ── Pure functions mirroring NarrationEngine ──────────────────────────────

    private static float ClampTtsValue(float value) =>
        value >= 0.5f && value <= 2.0f ? value : 1.0f;

    /// <summary>
    /// Mirrors ResolveLocaleAsync priority logic without MAUI Locale type.
    /// Priority: 1. languageCode match, 2. prefLang match, 3. null
    /// </summary>
    private static string? ResolveLocale(string? languageCode, string[] availableLocales, string prefLang)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
        {
            var match = availableLocales.FirstOrDefault(l =>
                l.StartsWith(languageCode.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        var prefMatch = availableLocales.FirstOrDefault(l =>
            l.StartsWith(prefLang[..2], StringComparison.OrdinalIgnoreCase));
        return prefMatch;
    }

    // ── Generators ────────────────────────────────────────────────────────────

    private static readonly string[] SampleLocales =
        { "vi-VN", "en-US", "en-GB", "fr-FR", "ja-JP", "zh-CN", "ko-KR" };

    private static readonly string[] SamplePrefLangs =
        { "vi-VN", "en-US", "fr-FR", "ja-JP", "zh-CN" };

    private static readonly Arbitrary<(string languageCode, string[] availableLocales, string prefLang)>
        LocaleMatchScenarioArb = Arb.ToArbitrary(
            from matchLocale in Gen.Elements(SampleLocales)
            from extraCount in Gen.Choose(0, 4)
            from extras in Gen.Elements(SampleLocales).ListOf(extraCount)
            let allLocales = extras.Append(matchLocale).Distinct().ToArray()
            from prefLang in Gen.Elements(SamplePrefLangs)
            let languageCode = matchLocale[..2]
            select (languageCode, allLocales, prefLang));

    private static readonly Arbitrary<float> OutOfRangeFloatArb = Arb.ToArbitrary(
        Gen.OneOf(
            Gen.Choose(-10000, 49).Select(i => i / 100f),
            Gen.Choose(201, 10000).Select(i => i / 100f)
        ));

    private static readonly Arbitrary<float> InRangeFloatArb = Arb.ToArbitrary(
        Gen.Choose(50, 200).Select(i => i / 100f));

    private static readonly Arbitrary<List<int>> PoiIdListArb = Arb.ToArbitrary(
        from count in Gen.Choose(1, 10)
        from ids in Gen.Choose(1, 1000).ListOf(count)
        select ids.Distinct().ToList());

    // ── Property 15 ──────────────────────────────────────────────────────────

    /// <summary>
    /// When languageCode matches an available locale, ResolveLocale must return that locale.
    /// Validates: Requirements 10.1, 10.5
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolveLocale_WhenLanguageCodeMatches_ReturnsMatchingLocale()
    {
        return Prop.ForAll(LocaleMatchScenarioArb, scenario =>
        {
            var (languageCode, availableLocales, prefLang) = scenario;
            var result = ResolveLocale(languageCode, availableLocales, prefLang);

            if (result is null)
                return Prop.Label(false,
                    $"Expected a locale match for languageCode='{languageCode}' but got null");

            if (!result.StartsWith(languageCode, StringComparison.OrdinalIgnoreCase))
                return Prop.Label(false,
                    $"Returned locale '{result}' does not start with languageCode='{languageCode}'");

            return Prop.Label(true, $"OK: languageCode='{languageCode}' → locale='{result}'");
        });
    }

    // ── Property 16 ──────────────────────────────────────────────────────────

    /// <summary>
    /// For any float outside [0.5, 2.0], ClampTtsValue must return exactly 1.0.
    /// Validates: Requirements 11.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClampTtsValue_OutOfRange_ReturnsDefault()
    {
        return Prop.ForAll(OutOfRangeFloatArb, value =>
        {
            var result = ClampTtsValue(value);
            if (result != 1.0f)
                return Prop.Label(false, $"Expected 1.0 for out-of-range value={value} but got {result}");
            return Prop.Label(true, $"OK: value={value} → clamped to 1.0");
        });
    }

    /// <summary>
    /// For any float inside [0.5, 2.0], ClampTtsValue must return the value unchanged.
    /// Validates: Requirements 11.6
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClampTtsValue_InRange_ReturnsValueUnchanged()
    {
        return Prop.ForAll(InRangeFloatArb, value =>
        {
            var result = ClampTtsValue(value);
            if (result != value)
                return Prop.Label(false, $"Expected {value} (in-range) but got {result}");
            return Prop.Label(true, $"OK: value={value} → unchanged");
        });
    }

    // ── Property 17 ──────────────────────────────────────────────────────────

    /// <summary>
    /// ClampTtsValue applied to any float always produces a result within [0.5, 2.0].
    /// Validates: Requirements 11.4
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ClampTtsValue_ResultAlwaysInValidRange()
    {
        return Prop.ForAll(ArbMap.Default.ArbFor<float>(), value =>
        {
            var result = ClampTtsValue(value);

            if (result < 0.5f || result > 2.0f)
                return Prop.Label(false, $"ClampTtsValue({value}) = {result} is outside [0.5, 2.0]");

            if (value >= 0.5f && value <= 2.0f && result != value)
                return Prop.Label(false, $"In-range input {value} was changed to {result}");

            if ((value < 0.5f || value > 2.0f) && result != 1.0f)
                return Prop.Label(false, $"Out-of-range input {value} should give 1.0 but got {result}");

            return Prop.Label(true, $"OK: ClampTtsValue({value}) = {result}");
        });
    }

    // ── Property 18 ──────────────────────────────────────────────────────────

    /// <summary>
    /// After Skip(), queue count decreases by 1 (or stays 0 if already empty).
    /// Validates: Requirements 12.2
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Skip_DecreasesQueueCountByOne()
    {
        return Prop.ForAll(PoiIdListArb, poiIds =>
        {
            var queue = new TestableNarrationQueue();
            foreach (var id in poiIds) queue.Enqueue(id);

            var countBefore = queue.Count;
            queue.Skip();
            var countAfter = queue.Count;
            var expected = countBefore > 0 ? countBefore - 1 : 0;

            if (countAfter != expected)
                return Prop.Label(false,
                    $"Expected count={expected} after skip but got {countAfter} (was {countBefore})");

            return Prop.Label(true, $"OK: count {countBefore} → {countAfter}");
        });
    }

    [Fact]
    public void Skip_OnEmptyQueue_StaysAtZero()
    {
        var queue = new TestableNarrationQueue();
        queue.Skip();
        Assert.Equal(0, queue.Count);
    }

    // ── Property 19 ──────────────────────────────────────────────────────────

    /// <summary>
    /// After Clear(), queue count is always 0 regardless of prior state.
    /// Validates: Requirements 12.3
    /// </summary>
    [Property(MaxTest = 100)]
    public Property Clear_AlwaysProducesEmptyQueue()
    {
        return Prop.ForAll(PoiIdListArb, poiIds =>
        {
            var queue = new TestableNarrationQueue();
            foreach (var id in poiIds) queue.Enqueue(id);

            queue.Clear();

            if (queue.Count != 0)
                return Prop.Label(false, $"Expected empty queue after Clear but got count={queue.Count}");

            return Prop.Label(true, $"OK: cleared {poiIds.Count} items → count=0");
        });
    }

    // ── Property 20 ──────────────────────────────────────────────────────────

    /// <summary>
    /// QueueChanged fires exactly once per Enqueue (for new items), Skip, and Clear.
    /// Validates: Requirements 12.8
    /// </summary>
    [Property(MaxTest = 100)]
    public Property QueueChanged_FiresExactlyOncePerOperation()
    {
        return Prop.ForAll(PoiIdListArb, poiIds =>
        {
            var queue = new TestableNarrationQueue();
            int eventCount = 0;
            queue.QueueChanged += () => eventCount++;

            var distinctIds = poiIds.Distinct().ToList();
            foreach (var id in distinctIds) queue.Enqueue(id);

            if (eventCount != distinctIds.Count)
                return Prop.Label(false,
                    $"Expected {distinctIds.Count} QueueChanged events after enqueue but got {eventCount}");

            var countBeforeSkip = queue.Count;
            eventCount = 0;
            queue.Skip();
            var expectedSkipEvents = countBeforeSkip > 0 ? 1 : 0;
            if (eventCount != expectedSkipEvents)
                return Prop.Label(false,
                    $"Expected {expectedSkipEvents} QueueChanged event(s) after skip but got {eventCount}");

            eventCount = 0;
            queue.Clear();
            if (eventCount != 1)
                return Prop.Label(false, $"Expected 1 QueueChanged event after clear but got {eventCount}");

            return Prop.Label(true,
                $"OK: enqueue={distinctIds.Count}, skip={expectedSkipEvents}, clear=1");
        });
    }
}

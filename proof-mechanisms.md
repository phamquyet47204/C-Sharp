# Proof of Mechanisms

> Chứng minh code thực tế xử lý 4 cơ chế: **Trùng · Ưu tiên · Hàng đợi · Performance**

---

## 1. Xử lý Trùng (Duplicate Prevention)

### 1a. Trùng lặp sự kiện (Chống Spam POI do GPS)
**Vấn đề**: GPS ngoài trời nhảy lung tung làm app lầm tưởng user ra/vào quán liên tục (Spam sự kiện).
**Giải pháp**: Ép đứng trong vùng ít nhất 2 lần cập nhật GPS liên tiếp (Debounce).
**`GeofenceEngine.cs` — `HandleInsidePoisWithPriorityAndDebounce`**
```csharp
private const int EnterDebounceThreshold = 2;          // phải vào vùng >= 2 lần liên tiếp
private readonly Dictionary<int, int> _insideStableCounters = new();

foreach (var poi in insideCandidates)
    _insideStableCounters[poi.Id] = _insideStableCounters.GetValueOrDefault(poi.Id) + 1;

var readyToEnter = insideCandidates
    .Where(p => _insideStableCounters.GetValueOrDefault(p.Id) >= EnterDebounceThreshold)
    .Where(p => !_activePoiIds.Contains(p.Id))
    .Where(p => !_cooldownUntilUtc.TryGetValue(p.Id, out var t) || t <= now)
    .ToList();
```
Khi ra ngoài → reset về 0: `_insideStableCounters[poi.Id] = 0;`

### 1b. Trùng lặp vùng địa lý (Overlapping POIs)
**Vấn đề**: Nhiều quán nằm sát nhau, bán kính đè lên nhau. Quét 1 điểm trúng 2-3 quán.
**Giải pháp**: Duyệt TOÀN BỘ danh sách, gom tất cả quán trùng vào `insideCandidates` để xét Priority.
**`GeofenceEngine.cs` — `ProcessLocationAsync`**
```csharp
foreach (var poi in _cachedPois)          // duyệt TOÀN BỘ, không dùng FirstOrDefault
{
    var dist = CalculateDistance(...);
    if (dist <= poi.Radius)
        insideCandidates.Add(poi);        // Gom hết các quán đang trùng nhau → OK
    else
        outsidePois.Add(poi);
}
HandleInsidePoisWithPriorityAndDebounce(insideCandidates, now);
```

### 1c. Trùng lặp dữ liệu DB (Data Duplication)

**FreeTrialRecord — Chống bào lượt nghe thử**
```csharp
// AppDbContext.cs
builder.Entity<FreeTrialRecord>().HasIndex(f => new { f.DeviceId, f.PoiId }).IsUnique();

// AnalyticsController.cs
var alreadyExists = await dbContext.FreeTrialRecords
    .AnyAsync(f => f.DeviceId == deviceId && f.PoiId == command.PoiId.Value);
```

**Payment — Chống thanh toán 2 lần (Idempotent)**
```csharp
// AppDbContext.cs
builder.Entity<Payment>().HasIndex(p => p.TransactionId).IsUnique();

// PaymentController.cs
var exists = await dbContext.Payments.AnyAsync(p => p.TransactionId == request.TransactionId);
if (exists) return Conflict(new { error = "Giao dịch đã tồn tại." });
```

**PoiRating — Đánh giá dùng Upsert chống spam rate**
```csharp
// PoiRatingsController.cs
var existing = await dbContext.PoiRatings.FirstOrDefaultAsync(...);
if (existing is null) dbContext.PoiRatings.Add(new PoiRating { ... });
else existing.Stars = request.Stars;   // cập nhật, không tạo mới
```

---

## 2. Xử lý Độ ưu tiên (Priority)

### 2a. Chọn quán Priority cao nhất khi vùng chồng lấn
**`GeofenceEngine.cs` — `HandleInsidePoisWithPriorityAndDebounce`**
```csharp
var selectedPoi = readyToEnter
    .OrderByDescending(p => p.Priority)   // Premium=100 > thường=0
    .ThenBy(p => p.Id)                    // tie-breaker ổn định
    .First();
```

### 2b. Preemption — ngắt quán thường khi quán Premium vào vùng
**`GeofenceEngine.cs`**
```csharp
var lowerPriorityActives = _activePoiIds
    .Select(id => _poiMap.GetValueOrDefault(id))
    .Cast<POI>()
    .Where(p => p.Id != selectedPoi.Id && p.Priority < selectedPoi.Priority)
    .ToList();

foreach (var lowerPoi in lowerPriorityActives)
    if (_activePoiIds.Remove(lowerPoi.Id))
        OnPoiExited?.Invoke(lowerPoi);    // buộc dừng quán thường

_activePoiIds.Add(selectedPoi.Id);
OnPoiEntered?.Invoke(selectedPoi);        // phát quán Premium
```

### 2c. Admin gán Priority
**`AdminController.cs` — `TogglePremium`**
```csharp
poi.IsPremium = !poi.IsPremium;
poi.Priority  = poi.IsPremium ? 100 : 0;
poi.UpdatedAt = DateTime.UtcNow;          // Mobile sync nhận thay đổi lần sau
```

### 2d. Language Fallback — 4 tầng ưu tiên ngôn ngữ
**`DatabaseService.cs` — `SelectByFallback`**
```csharp
// Tier 1: ngôn ngữ đích (ja, en...)
var primary = variants.FirstOrDefault(p => lang == targetLang);
if (primary is not null) return primary;
// Tier 2: tiếng Anh
var english = variants.FirstOrDefault(p => lang == "en");
if (english is not null) return english;
// Tier 3: tiếng Việt (gốc hệ thống)
var vietnamese = variants.FirstOrDefault(p => lang == "vi");
if (vietnamese is not null) return vietnamese;
// Tier 4: Priority cao nhất
return variants.OrderByDescending(p => p.Priority).FirstOrDefault();
```

### 2e. Gemini model fallback — thử lần lượt 4 model
**`GeminiAiService.cs`**
```csharp
var modelNames = new[] {
    "gemini-2.5-flash", "gemini-1.5-flash",
    "gemini-2.0-flash", "gemini-2.5-flash-lite"
};
foreach (var model in modelNames)
{
    // retry 2 lần với exponential backoff
    if (response.IsSuccessStatusCode) return parsed;
    if (statusCode is 503 or 429) break;  // chuyển model tiếp
}
```

---

## 3. Xử lý Hàng đợi (Queue)

### 3a. AudioQueueManager — 1 slot, hủy request cũ ngay
**`AudioQueueManager.cs`**
```csharp
private readonly SemaphoreSlim _queueLock = new(1, 1);  // chỉ 1 slot
private CancellationTokenSource? _currentCts;

public async Task RunExclusiveAsync(Func<CancellationToken, Task> work)
{
    _currentCts?.Cancel();                  // hủy ngay request đang chạy
    var localCts = new CancellationTokenSource();
    _currentCts = localCts;

    await _queueLock.WaitAsync();           // chờ slot trống
    try   { await work(localCts.Token); }
    catch (OperationCanceledException) { }  // bị hủy bởi request mới → bình thường
    finally { _queueLock.Release(); }
}
```
Cả `SpeakAsync` (TTS) và `PlayAudioAsync` (MP3) đều đi qua `RunExclusiveNarrationAsync` → `AudioQueueManager`. Tại mọi thời điểm chỉ có **1 giọng đọc**.

### 3b. GeofenceEngine._processLock — GPS xử lý tuần tự
**`GeofenceEngine.cs` — `ProcessLocationAsync`**
```csharp
private readonly SemaphoreSlim _processLock = new(1, 1);

private async Task ProcessLocationAsync(Location location)
{
    await _processLock.WaitAsync();         // GPS update sau chờ GPS update trước
    try   { /* phân loại POI, debounce, preemption */ }
    finally { _processLock.Release(); }
}
```
Tránh race condition trên `_insideStableCounters`, `_activePoiIds`, `_cooldownUntilUtc`.

### 3c. DatabaseService._syncLock — chỉ 1 sync tại một thời điểm
**`DatabaseService.cs` — `SyncPoisFromServerAsync`**
```csharp
private readonly SemaphoreSlim _syncLock = new(1, 1);

await _syncLock.WaitAsync(cancellationToken);
try   { /* GET /api/pois/updates → ApplyServerChangesAsync → SaveLastSyncTime */ }
finally { _syncLock.Release(); }
```

### 3d. DatabaseService._initLock — Double-check init
**`DatabaseService.cs` — `InitializeAsync`**
```csharp
private readonly SemaphoreSlim _initLock = new(1, 1);

if (_isInitialized) return;               // fast path
await _initLock.WaitAsync();
try
{
    if (_isInitialized) return;           // double-check sau khi có lock
    _database = new SQLiteAsyncConnection(_databasePath);
    await _database.CreateTableAsync<POI>();
    _isInitialized = true;
}
finally { _initLock.Release(); }
```

---

## 4. Performance

### 4a. RAM Cache — không query SQLite mỗi GPS update
**`GeofenceEngine.cs` — `RefreshPoisCoreAsync`**
```csharp
// Load 1 lần vào RAM, sort sẵn theo Priority
_cachedPois = (await _databaseService.GetLocalizedPoisAsync(_currentLanguageCode))
    .OrderByDescending(p => p.Priority)
    .ToList();

_poiMap.Clear();
foreach (var poi in _cachedPois)
    _poiMap[poi.Id] = poi;               // O(1) lookup theo Id
```
GPS update mỗi 10s → chỉ tính Haversine trên RAM, không I/O.

### 4b. Adaptive GPS interval — tiết kiệm pin
**`LocationService.cs` — `UpdateAdaptiveInterval`**
```csharp
private static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(10);
private static readonly TimeSpan IdleInterval   = TimeSpan.FromSeconds(10);
private static readonly TimeSpan StationaryDurationThreshold = TimeSpan.FromMinutes(1);

// Nếu đứng yên > 1 phút → giãn interval (tiết kiệm pin)
_currentInterval = _stationaryDuration >= StationaryDurationThreshold
    ? IdleInterval : ActiveInterval;
```
Tốc độ < 1 km/h → tích lũy `_stationaryDuration`. Di chuyển → reset về 0.

### 4c. Distance Filter — không emit GPS thừa
**`LocationService.cs` — `ShouldEmitLocationChanged`**
```csharp
private const double DistanceFilterMeters = 1d;

var distanceMeters = Location.CalculateDistance(_lastEmittedLocation, location, ...) * 1000;
if (distanceMeters >= DistanceFilterMeters) return true;   // di chuyển đủ → emit

// Heartbeat 10s khi đứng yên — cần cho Debounce counter của GeofenceEngine
if (DateTimeOffset.UtcNow - _lastEmittedAtUtc >= MaxSilentEmitInterval) return true;

return false;   // bỏ qua nếu không đủ điều kiện
```

### 4d. Delta Sync — chỉ tải dữ liệu thay đổi
**`DatabaseService.cs` — `SyncPoisFromServerAsync`**
```csharp
var lastSync = Preferences.Get("root_last_sync_utc", "");
var url = $"api/pois/updates?lastSync={lastSync:O}";   // chỉ lấy UpdatedAt > lastSync
```
Server side — `PoiRepository.cs`:
```csharp
return await context.Pois
    .Where(p => p.UpdatedAt > lastSyncTimestamp && p.Status == PoiStatus.Approved)
    .ToListAsync();
```

### 4e. Pruning — xóa POI stale khỏi SQLite
**`DatabaseService.cs` — `ApplyServerChangesAsync`**
```csharp
var toDelete = localPois
    .Where(lp => !payload.ActiveBasePoiIds.Contains(lp.BasePoiId))
    .ToList();

foreach (var poi in toDelete)
    await _database.DeleteAsync(poi);    // xóa POI đã bị Admin xóa trên server
```

### 4f. SignalR Throttle — chống flood dashboard
**`AnalyticsController.cs` — `PublishRealtimeUpdateAsync`**
```csharp
private static DateTime _lastRealtimePush = DateTime.MinValue;
private static readonly object _pushLock = new();

lock (_pushLock)
{
    if (DateTime.UtcNow - _lastRealtimePush < TimeSpan.FromSeconds(1))
        return;                          // bỏ qua nếu < 1s từ lần push trước
    _lastRealtimePush = DateTime.UtcNow;
}
await analyticsHub.Clients.Group("AdminGroup").SendAsync("analytics:realtime", payload);
```
500 thiết bị gửi event đồng thời → dashboard chỉ nhận **1 update/giây**.

### 4g. Heatmap Snap-to-POI — gom điểm về tâm quán
**`AnalyticsController.cs` — `BuildHeatmapPoints`**
```csharp
// Bước 1: Mỗi DeviceId → 1 vị trí trung bình (loại nhiễu GPS)
var userPositions = events
    .GroupBy(e => e.DeviceId)
    .Select(g => new { Lat = g.Average(e => e.Latitude), Lng = g.Average(e => e.Longitude), ... });

// Bước 2: Nếu trong bán kính POI → hút về tâm POI
var nearestPoi = poiRefs
    .Where(p => p.Dist <= p.Radius / 1000.0)
    .OrderBy(p => p.Dist).FirstOrDefault();

// Bước 3: Gom nhóm theo POI hoặc lưới 44m
.GroupBy(x => x.Poi != null
    ? $"poi:{x.Poi.Name}"
    : $"grid:{Math.Round(x.Lat * 2500.0)/2500.0}:{Math.Round(x.Lng * 2500.0)/2500.0}")
.Take(HeatmapMaxPoints)   // giới hạn 500 điểm gửi về frontend
```

---

## Tóm tắt

| Cơ chế | File | Kỹ thuật |
|---|---|---|
| Vùng chồng lấn | `GeofenceEngine.cs` | `foreach` toàn bộ, không `FirstOrDefault` |
| Chống nhiễu GPS | `GeofenceEngine.cs` | `_insideStableCounters`, threshold=2 |
| Trùng FreeTrialRecord | `AnalyticsController.cs` + `AppDbContext.cs` | `AnyAsync` + Unique index |
| Trùng Payment | `PaymentController.cs` + `AppDbContext.cs` | `AnyAsync` + Unique index |
| Trùng Rating | `PoiRatingsController.cs` | Upsert pattern |
| Chọn quán ưu tiên | `GeofenceEngine.cs` | `OrderByDescending(Priority).ThenBy(Id).First()` |
| Preemption | `GeofenceEngine.cs` | `lowerPriorityActives` → `OnPoiExited` |
| Language fallback | `DatabaseService.cs` | 4-tier: target → en → vi → Priority |
| Gemini fallback | `GeminiAiService.cs` | 4 model × 2 retry + exponential backoff |
| Audio queue | `AudioQueueManager.cs` | `SemaphoreSlim(1,1)` + `CancellationTokenSource` |
| GPS tuần tự | `GeofenceEngine.cs` | `_processLock SemaphoreSlim(1,1)` |
| Sync tuần tự | `DatabaseService.cs` | `_syncLock SemaphoreSlim(1,1)` |
| Init 1 lần | `DatabaseService.cs` | Double-check + `_initLock` |
| RAM Cache | `GeofenceEngine.cs` | `_cachedPois` + `_poiMap` Dictionary |
| Adaptive GPS | `LocationService.cs` | `_stationaryDuration` → `IdleInterval` |
| Distance Filter | `LocationService.cs` | `ShouldEmitLocationChanged` |
| Delta Sync | `DatabaseService.cs` + `PoiRepository.cs` | `?lastSync=` + `WHERE UpdatedAt >` |
| Pruning | `DatabaseService.cs` | `WHERE BasePoiId NOT IN ActiveBasePoiIds` |
| SignalR Throttle | `AnalyticsController.cs` | `if (now - _last < 1s) return` |
| Heatmap clustering | `AnalyticsController.cs` | Snap-to-POI + grid 44m + `Take(500)` |

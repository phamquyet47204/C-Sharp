# Chứng minh: Xử lý Trùng, Độ ưu tiên và Hàng đợi

> Mỗi đoạn code bên dưới ghi rõ **file** và **số dòng** thực tế trong source.

---

## 1. Xử lý Vùng Chồng lấn (Overlapping Zones)

**Vấn đề**: Bán kính nhiều quán đan xen nhau. Khi người dùng đứng điểm giao thoa, nhiều POI cùng thỏa `distance <= radius`. Hệ thống phải xử lý tất cả, không phải chỉ lấy quán đầu tiên tìm thấy.

**File**: `VinhKhanh.Mobile/Services/GeofenceEngine.cs`

```
Dòng 245: var insideCandidates = new List<POI>();
Dòng 246: var outsidePois      = new List<POI>();
Dòng 248: foreach (var poi in _cachedPois)            // duyệt toàn bộ POI trong RAM cache
Dòng 259:     if (distanceMeters <= poi.Radius)
Dòng 261:         insideCandidates.Add(poi);           // ← CÓ THỂ nhiều quán cùng lúc
Dòng 264:         outsidePois.Add(poi);
Dòng 270: HandleExitedPois(outsidePois);
Dòng 273: HandleInsidePoisWithPriorityAndDebounce(insideCandidates, now);
```

**Cơ chế**: `ProcessLocationAsync` (dòng 235–279) duyệt qua **toàn bộ** `_cachedPois` (RAM cache). Kết quả là `insideCandidates` có thể chứa 2–N quán cùng lúc. Hệ thống KHÔNG dừng lại ở quán đầu tiên mà chuyển toàn bộ danh sách sang bước xét Priority.

---

## 2. Độ Ưu tiên (Priority) & Preemption

### 2a. Chọn quán ưu tiên cao nhất

**File**: `VinhKhanh.Mobile/Services/GeofenceEngine.cs`

```
Dòng 307: private void HandleInsidePoisWithPriorityAndDebounce(List<POI> insideCandidates, ...)

// Bước 1 — Lọc Debounce: chỉ POI >= 2 lần liên tiếp trong vùng mới xét
Dòng 325: var readyToEnter = insideCandidates
Dòng 326:     .Where(p => _insideStableCounters.GetValueOrDefault(p.Id, 0) >= EnterDebounceThreshold)
Dòng 327:     .Where(p => !_activePoiIds.Contains(p.Id))       // chưa đang phát
Dòng 328:     .Where(p => !_cooldownUntilUtc... || until <= now) // chưa trong cooldown
Dòng 329:     .ToList();

// Bước 2 — CHỌN 1 POI duy nhất theo Priority cao nhất
Dòng 339: var selectedPoi = readyToEnter
Dòng 340:     .OrderByDescending(p => p.Priority)   // ← Premium=100 > thường=0
Dòng 341:     .ThenBy(p => p.Id)                    // ← tie-breaker ổn định
Dòng 342:     .First();
```

### 2b. Preemption — ngắt quán thường khi quán Premium vào vùng

**File**: `VinhKhanh.Mobile/Services/GeofenceEngine.cs`

```
// Bước 3 — PREEMPTION: tìm các POI đang active có Priority thấp hơn
Dòng 347: var lowerPriorityActives = _activePoiIds
Dòng 348:     .Select(id => _poiMap.GetValueOrDefault(id))
Dòng 351:     .Where(p => p.Id != selectedPoi.Id && p.Priority < selectedPoi.Priority)
Dòng 352:     .ToList();

Dòng 354: foreach (var lowerPoi in lowerPriorityActives)
Dòng 356:     if (_activePoiIds.Remove(lowerPoi.Id))
Dòng 359:         OnPoiExited?.Invoke(lowerPoi);     // ← buộc Exit quán thường

// Bước 4 — Kích hoạt quán được chọn
Dòng 364: _activePoiIds.Add(selectedPoi.Id);
Dòng 366: OnPoiEntered?.Invoke(selectedPoi);         // ← trigger NarrationService
```

### 2c. Nơi Priority được gán

**File**: `VinhKhanh.Admin/Controllers/AdminController.cs`

```
Dòng 172: [HttpPost("users/{userId}/toggle-premium")]
Dòng 173: public async Task<IActionResult> TogglePremium(string userId)

Dòng 178: poi.IsPremium = !poi.IsPremium;
Dòng 179: poi.Priority = poi.IsPremium ? 100 : 0;  // ← Premium=100, thường=0
Dòng 183: return Ok(new { success = true, isPremium = poi.IsPremium, priority = poi.Priority });
```

> **Kết quả**: Khi admin bật Premium cho một quán, `Priority` được set = 100. Khi người dùng bước vào vùng quán Premium, engine so sánh Priority và tự động ngắt quán thường (Priority=0) đang phát để chuyển sang quán Premium ngay lập tức.

---

## 3. Hàng đợi Âm thanh Độc quyền (Exclusive Audio Queue)

### 3a. AudioQueueManager — lớp quản lý hàng đợi

**File**: `VinhKhanh.Mobile/Services/AudioQueueManager.cs`

```
Dòng 10: private readonly SemaphoreSlim _queueLock = new(1, 1); // chỉ 1 slot đồng thời
Dòng 11: private CancellationTokenSource? _currentCts;

Dòng 13: public async Task RunExclusiveAsync(Func<CancellationToken, Task> work)
Dòng 15:     _currentCts?.Cancel();         // ← HỦY ngay luồng đang chạy
Dòng 17:     var localCts = new CancellationTokenSource();
Dòng 18:     _currentCts = localCts;

Dòng 20:     await _queueLock.WaitAsync();  // ← CHỜ slot trống (SemaphoreSlim(1,1))
Dòng 23:         localCts.Token.ThrowIfCancellationRequested();
Dòng 24:         await work(localCts.Token); // ← chạy công việc thực tế

Dòng 26:     catch (OperationCanceledException)
Dòng 28:         // bị cancel bởi request mới → bình thường

Dòng 38:     _queueLock.Release();          // ← GIẢI PHÓNG slot cho request tiếp theo

Dòng 42: public void CancelCurrent()
Dòng 44:     _currentCts?.Cancel();
```

### 3b. NarrationService — hai hàm phát đều qua cùng một cổng

**File**: `VinhKhanh.Mobile/Services/NarrationService.cs`

```
// Hàm phát TTS
Dòng 52:  public async Task SpeakAsync(string text, string lang)
Dòng 60:      await RunExclusiveNarrationAsync(async ct => { ... });

// Hàm phát MP3 từ app bundle
Dòng 101: public async Task PlayAudioAsync(string filePath)
Dòng 108:     await RunExclusiveNarrationAsync(async ct => { ... });

// Cổng độc quyền — cả hai đều phải đi qua đây
Dòng 168: private async Task RunExclusiveNarrationAsync(Func<CancellationToken, Task> work)
Dòng 175:     await BeginAudioDuckingAsync();  // giảm âm app khác
Dòng 178:     await Task.Delay(120, ct);       // chờ Ducking có hiệu lực
Dòng 180:     await work(ct);                  // phát âm thanh
Dòng 185:     EndAudioDucking();               // khôi phục âm lượng (finally block)

// Timeout bảo vệ cho MP3 — không treo vô hạn
Dòng 370: var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(12), ct));
```

> **Kết quả**: Dù gọi `SpeakAsync` hay `PlayAudioAsync`, cả hai đều phải qua `RunExclusiveNarrationAsync` → `AudioQueueManager.RunExclusiveAsync`. Yêu cầu mới sẽ **hủy ngay** (`_currentCts?.Cancel()` dòng 15) yêu cầu cũ và **chờ slot** (`SemaphoreSlim(1,1)` dòng 20) trước khi phát — đảm bảo tại mọi thời điểm chỉ có 1 giọng đọc vang lên.

---

## Tóm tắt ánh xạ Code → Cơ chế

| Cơ chế | File | Dòng | Kỹ thuật |
|---|---|---|---|
| **Gom POI trùng vùng** | `GeofenceEngine.cs` | 245–273 | `List<POI> insideCandidates` trong `foreach` |
| **Chống nhiễu GPS (Debounce)** | `GeofenceEngine.cs` | 30, 326 | `_insideStableCounters`, `EnterDebounceThreshold = 2` |
| **Chọn quán ưu tiên cao** | `GeofenceEngine.cs` | 339–342 | `.OrderByDescending(Priority).ThenBy(Id).First()` |
| **Ngắt quán thấp (Preemption)** | `GeofenceEngine.cs` | 347–359 | `lowerPriorityActives`, `OnPoiExited?.Invoke` |
| **Gán Priority = 100** | `AdminController.cs` | 179 | `poi.Priority = poi.IsPremium ? 100 : 0` |
| **Hàng đợi 1 slot** | `AudioQueueManager.cs` | 10, 20, 38 | `SemaphoreSlim(1,1).WaitAsync()/.Release()` |
| **Hủy luồng cũ** | `AudioQueueManager.cs` | 11, 15, 17 | `CancellationTokenSource._currentCts?.Cancel()` |
| **Cổng độc quyền TTS** | `NarrationService.cs` | 52, 60 | `SpeakAsync` → `RunExclusiveNarrationAsync` |
| **Cổng độc quyền MP3** | `NarrationService.cs` | 101, 108 | `PlayAudioAsync` → `RunExclusiveNarrationAsync` |
| **Audio Ducking** | `NarrationService.cs` | 175, 178, 185 | `BeginAudioDuckingAsync`, `Task.Delay(120)`, `EndAudioDucking` |
| **Timeout MP3** | `NarrationService.cs` | 370 | `Task.WhenAny(..., Task.Delay(12s))` |

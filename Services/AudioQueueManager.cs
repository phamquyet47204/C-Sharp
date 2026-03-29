using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VinhKhanhFoodStreet.Services;

public class AudioQueueManager : IAudioQueueManager
{
    private readonly SemaphoreSlim _queueLock = new(1, 1);
    private CancellationTokenSource? _currentCts;

    public async Task RunExclusiveAsync(Func<CancellationToken, Task> work)
    {
        _currentCts?.Cancel();

        var localCts = new CancellationTokenSource();
        _currentCts = localCts;

        await _queueLock.WaitAsync();
        try
        {
            localCts.Token.ThrowIfCancellationRequested();
            await work(localCts.Token);
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[AudioQueueManager] Current audio task canceled by newer request");
        }
        finally
        {
            if (ReferenceEquals(_currentCts, localCts))
            {
                _currentCts = null;
            }

            localCts.Dispose();
            _queueLock.Release();
        }
    }

    public void CancelCurrent()
    {
        _currentCts?.Cancel();
    }
}

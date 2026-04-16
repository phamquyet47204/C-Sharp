using System;
using System.Threading;
using System.Threading.Tasks;

namespace VinhKhanhFoodStreet.Services;

public interface IAudioQueueManager
{
    Task RunExclusiveAsync(Func<CancellationToken, Task> work);
    void CancelCurrent();
}

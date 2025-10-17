/* MIT License
 * https://github.com/PolarGoose/ShowWhatProcessLocksFile/blob/main/LICENSE
 */

using System;
using System.Threading;
using System.Threading.Tasks;

namespace LocalProjectRefresh.LockFinding.Utils;

internal sealed class WorkerThreadWithDeadLockDetection(TimeSpan deadLockTimeout, Action<Watchdog> action)
{
    // Returns:
    // * true in case the action finished successfully
    // * false in case of a deadlock
    public bool Run()
    {
        var deadlockDetected = new TaskCompletionSource<bool>();
        using var watchdog = new Watchdog(() => deadlockDetected.SetResult(true), deadLockTimeout);
        var workerTask = Task.Factory.StartNew(() => action(watchdog), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var completedTask = Task.WaitAny(workerTask, deadlockDetected.Task);
        return completedTask == 0;
    }
}

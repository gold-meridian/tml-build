/* MIT License
 * https://github.com/PolarGoose/ShowWhatProcessLocksFile/blob/main/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using LocalProjectRefresh.LockFinding.Interop;
using LocalProjectRefresh.LockFinding.Utils;
using Microsoft.Win32.SafeHandles;

namespace LocalProjectRefresh.LockFinding;

public record struct ProcessInfo(
    int Pid
);

public static class LockFinder
{
    public static IEnumerable<ProcessInfo> FindWhatProcessesLockPath(string path)
    {
        path = PathUtils.ToCanonicalPath(path);
        var currentProcess = WinApi.GetCurrentProcess();
        var result = new List<ProcessInfo>();

        var processes = NtDll.QuerySystemHandleInformation().GroupBy(h => h.UniqueProcessId).Select(processAndHandles => (processAndHandles.Key, processAndHandles.ToArray())).ToArray();
        var currentProcessIndex = 0;
        var currentHandleIndex = 0;
        SafeProcessHandle? currentOpenedProcess = null;
        var currentLockedFiles = new List<string>();
        SafeFileHandle? currentDupHandle = null;

        while (currentProcessIndex < processes.Length)
        {
            new WorkerThreadWithDeadLockDetection(
                TimeSpan.FromMilliseconds(50),
                watchdog =>
                {
                    while (currentProcessIndex < processes.Length)
                    {
                        var (pid, handles) = processes[currentProcessIndex];

                        if (currentOpenedProcess is null)
                        {
                            currentOpenedProcess = ProcessUtils.OpenProcessToDuplicateHandle(pid);
                            if (currentOpenedProcess is null)
                            {
                                currentProcessIndex++;
                                continue;
                            }

                            currentLockedFiles = [];
                            currentHandleIndex = 0;
                        }

                        while (currentHandleIndex < handles.Length)
                        {
                            currentDupHandle?.Dispose();
                            var h = handles[currentHandleIndex];
                            currentHandleIndex++;

                            currentDupHandle = WinApi.DuplicateHandle(currentProcess, currentOpenedProcess, h);
                            if (currentDupHandle.IsInvalid)
                            {
                                continue;
                            }

                            watchdog.Arm();
                            var lockedFileName = WinApi.GetFinalPathNameByHandle(currentDupHandle);
                            watchdog.Disarm();
                            if (lockedFileName is null)
                            {
                                continue;
                            }

                            lockedFileName = PathUtils.AddTrailingSeparatorIfItIsAFolder(lockedFileName);
                            if (lockedFileName.StartsWith(path, StringComparison.InvariantCultureIgnoreCase))
                            {
                                currentLockedFiles.Add(lockedFileName);
                            }
                        }

                        var moduleNames = ProcessUtils.GetProcessModules(currentOpenedProcess)
                                                      .Where(name => name.StartsWith(path, StringComparison.InvariantCultureIgnoreCase)).ToList();

                        if (currentLockedFiles.Any() || moduleNames.Any())
                        {
                            var processInfo = new ProcessInfo
                            {
                                Pid = (int)pid.ToUInt64(),
                                // LockedFileFullNames = currentLockedFiles.Concat(moduleNames).Distinct().OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(),
                                // DomainAndUserName = ProcessUtils.GetOwnerDomainAndUserName(currentOpenedProcess),
                                // ProcessExecutableFullName = ProcessUtils.GetProcessExeFullName(currentOpenedProcess),
                            };

                            /*
                            if (processInfo.ProcessExecutableFullName != null)
                            {
                                processInfo.ProcessName = Path.GetFileName(processInfo.ProcessExecutableFullName);
                            }
                            */

                            result.Add(processInfo);
                        }

                        currentDupHandle?.Dispose();
                        currentOpenedProcess.Dispose();
                        currentOpenedProcess = null;
                        currentProcessIndex++;
                    }
                }
            ).Run();
        }

        return result;
    }
}

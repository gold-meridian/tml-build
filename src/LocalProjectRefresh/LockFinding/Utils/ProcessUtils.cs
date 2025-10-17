/* MIT License
 * https://github.com/PolarGoose/ShowWhatProcessLocksFile/blob/main/LICENSE
 */

using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using LocalProjectRefresh.LockFinding.Interop;
using Microsoft.Win32.SafeHandles;

namespace LocalProjectRefresh.LockFinding.Utils;

public static class ProcessUtils
{
    public static string? GetOwnerDomainAndUserName(SafeProcessHandle openedProcess)
    {
        if (!WinApi.OpenProcessToken(openedProcess, WinApi.TokenAccessRights.TOKEN_QUERY, out var tokenHandle))
        {
            return null;
        }

        using (tokenHandle)
        {
            using var wi = new WindowsIdentity(tokenHandle.DangerousGetHandle());
            return wi.Name;
        }
    }

    public static string? GetProcessExeFullName(SafeProcessHandle processHandle)
    {
        for (var capacity = 1024;; capacity *= 2)
        {
            var buffer = new StringBuilder(capacity);
            var size = capacity;

            if (!WinApi.QueryFullProcessImageName(processHandle, 0, buffer, ref size))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != WinApi.ERROR_INSUFFICIENT_BUFFER)
                {
                    return null;
                }

                continue;
            }

            return buffer.ToString(0, size);
        }
    }

    public static SafeProcessHandle? OpenProcessToDuplicateHandle(nuint pid)
    {
        var p = WinApi.OpenProcess(
            WinApi.ProcessAccessRights.PROCESS_DUP_HANDLE |
            WinApi.ProcessAccessRights.PROCESS_QUERY_INFORMATION |
            WinApi.ProcessAccessRights.PROCESS_VM_READ,
            false,
            pid
        );
        return p.IsInvalid ? null : p;
    }

    public static IEnumerable<string> GetProcessModules(SafeProcessHandle openedProc)
    {
        for (var arraySize = 1024;; arraySize *= 2)
        {
            var moduleHandles = new nint[arraySize];
            if (!WinApi.EnumProcessModules(openedProc, moduleHandles, nint.Size * moduleHandles.Length, out var bytesNeeded))
            {
                return [];
            }

            var numberOfModules = bytesNeeded / nint.Size;

            if (arraySize < numberOfModules)
            {
                continue;
            }

            return GetModuleNames(openedProc, moduleHandles.Take(numberOfModules));
        }
    }

    private static IEnumerable<string> GetModuleNames(SafeProcessHandle openedProc, IEnumerable<nint> moduleHandles)
    {
        foreach (var moduleHandle in moduleHandles)
        {
            var name = GetModuleName(openedProc, moduleHandle);
            if (name != null)
            {
                yield return name;
            }
        }
    }

    private static string? GetModuleName(SafeProcessHandle openedProc, nint moduleHandle)
    {
        for (var moduleNameLength = 1024;; moduleNameLength *= 2)
        {
            var sb = new StringBuilder(moduleNameLength);
            var resultLen = WinApi.GetModuleFileNameEx(openedProc, moduleHandle, sb, sb.Capacity);

            if (resultLen == 0)
            {
                return null;
            }

            if (resultLen == sb.Capacity)
            {
                continue;
            }

            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using ConstiBoost.Localization;

namespace ConstiBoost.WinSys;

public enum ProcessProtection
{
    /// <summary>Normal user application – may be closed after confirmation.</summary>
    None,

    /// <summary>Closing is possible but has visible side effects (e.g. explorer.exe). Requires an extra warning.</summary>
    Caution,

    /// <summary>Critical Windows or security component – ConstiBoost will never close it.</summary>
    Critical,
}

/// <summary>Safety list for process actions. Critical processes are never terminated.</summary>
public static class ProcessSafety
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "idle", "registry", "secure system", "memory compression", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "lsaiso", "svchost", "fontdrvhost", "dwm", "sihost", "ctfmon", "conhost", "audiodg",
        "spoolsv", "taskhostw", "runtimebroker", "searchhost", "searchindexer", "startmenuexperiencehost",
        "shellexperiencehost", "textinputhost", "securityhealthservice", "securityhealthsystray", "msmpeng",
        "nissrv", "mpdefendercoreservice", "smartscreen", "wudfhost", "dllhost", "lsm", "wmiprvse", "dashost",
        "applicationframehost", "systemsettings", "widgets", "useroobebroker", "atiesrxx", "atieclxx", "amdfendrsr",
        "amdrsserv", "rtkauduservice64", "audiosrv", "easyanticheat", "easyanticheat_eos", "beservice", "battleye",
        "crashpad_handler", "securityhealthhost", "ngciso", "wlanext", "unsecapp",
    };

    private static readonly HashSet<string> Caution = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "onedrive", "steam", "epicgameslauncher", "epicwebhelper",
    };

    public static ProcessProtection Classify(string processName, string? path)
    {
        string name = Path.GetFileNameWithoutExtension(processName);
        if (Critical.Contains(name))
        {
            return ProcessProtection.Critical;
        }

        if (!string.IsNullOrEmpty(path))
        {
            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (path.StartsWith(Path.Combine(windowsDir, "System32"), StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(Path.Combine(windowsDir, "SysWOW64"), StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(Path.Combine(windowsDir, "SystemApps"), StringComparison.OrdinalIgnoreCase))
            {
                return Caution.Contains(name) ? ProcessProtection.Caution : ProcessProtection.Critical;
            }
        }

        return Caution.Contains(name) ? ProcessProtection.Caution : ProcessProtection.None;
    }

    public static bool IsOwnProcess(int pid) => pid == Environment.ProcessId;

    /// <summary>Processes without an accessible path, in session 0, or critical are never touched.</summary>
    public static bool CanTouch(Process process, out string reason)
    {
        try
        {
            if (IsOwnProcess(process.Id))
            {
                reason = L.T("This is ConstiBoost itself.");
                return false;
            }

            if (process.SessionId == 0)
            {
                reason = L.T("System service process (session 0).");
                return false;
            }

            string? path = TryGetPath(process);
            if (Classify(process.ProcessName, path) == ProcessProtection.Critical)
            {
                reason = L.T("Protected Windows/security process.");
                return false;
            }

            reason = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            reason = Core.Safe.Describe(ex);
            return false;
        }
    }

    public static string? TryGetPath(Process process) => TryGetPath(process.Id);

    /// <summary>Reads the image path with minimal access rights (works for most user processes without admin).</summary>
    public static string? TryGetPath(int pid)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var sb = new System.Text.StringBuilder(1024);
            uint size = (uint)sb.Capacity;
            return NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(handle);
            }
        }
    }
}

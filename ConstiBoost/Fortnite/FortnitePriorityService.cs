using System;
using System.Diagnostics;
using ConstiBoost.Core;
using ConstiBoost.Logging;

using ConstiBoost.Localization;

namespace ConstiBoost.Fortnite;

public sealed record FortniteProcessInfo(int Pid, ProcessPriorityClass? Priority, long WorkingSet, DateTime? StartTime);

/// <summary>
/// Optional, cautious priority change for the running Fortnite process (Above normal, never Realtime/High).
/// The change only lives as long as the process; Windows resets it when Fortnite exits.
/// Easy Anti-Cheat may block access – this is expected and handled.
/// </summary>
public static class FortnitePriorityService
{
    private static ProcessPriorityClass? _original;
    private static int? _changedPid;

    public static FortniteProcessInfo? GetRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName(FortniteLocator.ProcessName);
            if (processes.Length == 0)
            {
                return null;
            }

            var p = processes[0];
            ProcessPriorityClass? priority = Safe.Try<ProcessPriorityClass?>(() => p.PriorityClass, null);
            long ram = Safe.Try(() => p.WorkingSet64, 0L);
            DateTime? start = Safe.Try<DateTime?>(() => p.StartTime, null);
            var info = new FortniteProcessInfo(p.Id, priority, ram, start);
            foreach (var proc in processes)
            {
                proc.Dispose();
            }

            if (_changedPid.HasValue && _changedPid != info.Pid)
            {
                _changedPid = null; // Fortnite restarted, Windows already reset the priority
                _original = null;
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsChangedByConstiBoost => _changedPid.HasValue;

    public static OperationResult SetAboveNormal()
    {
        var running = GetRunning();
        if (running is null)
        {
            return OperationResult.Fail(L.T("Fortnite is not running. Start a match first."));
        }

        try
        {
            using var process = Process.GetProcessById(running.Pid);
            var before = process.PriorityClass;
            if (before != ProcessPriorityClass.Normal)
            {
                return OperationResult.Fail(L.F("Priority is already {0}. ConstiBoost only changes it from Normal.", before));
            }

            process.PriorityClass = ProcessPriorityClass.AboveNormal;
            _original = before;
            _changedPid = running.Pid;
            ActionLog.Success("Fortnite process priority", before.ToString(), "AboveNormal");
            return OperationResult.Ok(L.T("Fortnite priority set to Above normal (resets when Fortnite closes)."));
        }
        catch (Exception ex)
        {
            string reason = Safe.Describe(ex);
            ActionLog.Failed("Fortnite process priority", "Normal", "AboveNormal", reason);
            return OperationResult.Fail(L.T("Priority couldn't be changed – Fortnite's anti-cheat protects the process. This is normal and safe."), reason);
        }
    }

    public static OperationResult Reset()
    {
        var running = GetRunning();
        if (running is null || _changedPid != running.Pid)
        {
            _changedPid = null;
            _original = null;
            return OperationResult.Ok(L.T("Nothing to reset – Fortnite's priority is back to its default."));
        }

        try
        {
            using var process = Process.GetProcessById(running.Pid);
            var target = _original ?? ProcessPriorityClass.Normal;
            process.PriorityClass = target;
            ActionLog.Success("Fortnite process priority", "AboveNormal", target.ToString());
            _changedPid = null;
            _original = null;
            return OperationResult.Ok(L.T("Fortnite priority reset to Normal."));
        }
        catch (Exception ex)
        {
            ActionLog.Failed("Fortnite process priority", "AboveNormal", "Normal", Safe.Describe(ex));
            return OperationResult.Fail(L.T("Priority couldn't be reset: ") + Safe.Describe(ex));
        }
    }
}

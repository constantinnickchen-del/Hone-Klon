using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ConstiBoost.WinSys;

public sealed record PowerScheme(Guid Id, string Name);

/// <summary>Power plans (powrprof.dll) and the Windows "Power mode" overlay.</summary>
public static class PowerManager
{
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid PowerSaver = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    public static readonly Guid UltimatePerformance = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    // Power mode overlays used by Settings > System > Power & battery > Power mode.
    public static readonly Guid OverlayBestEfficiency = new("961cc777-2547-4f9d-8174-7d86181b8a7a");
    public static readonly Guid OverlayBalanced = Guid.Empty;
    public static readonly Guid OverlayBetterPerformance = new("3af9b8d9-7c97-431d-ad78-34a8bfea439f");
    public static readonly Guid OverlayBestPerformance = new("ded574b5-45a0-4f42-8737-46345c09c238");

    // Documented power setting: Processor power management > Maximum processor state (percent).
    public static readonly Guid SubProcessor = new("54533251-82be-4824-96c1-47b60b740d00");
    public static readonly Guid ProcessorMaxState = new("bc5038f7-23e0-4960-96da-33abaf5935ec");

    /// <summary>Reads "Maximum processor state" (AC, DC) of a scheme, or null.</summary>
    public static (uint ac, uint dc)? GetProcessorMaxState(Guid scheme)
    {
        try
        {
            Guid sub = SubProcessor, setting = ProcessorMaxState;
            if (NativeMethods.PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint ac) != 0 ||
                NativeMethods.PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out uint dc) != 0)
            {
                return null;
            }

            return (ac, dc);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes "Maximum processor state" and re-applies the scheme. Only 50–100 % is accepted.
    /// This never disables thermal protection – Windows and the CPU keep all safety limits.
    /// </summary>
    public static void SetProcessorMaxState(Guid scheme, uint ac, uint dc)
    {
        if (ac is < 50 or > 100 || dc is < 50 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ac), "Only 50–100 % is allowed.");
        }

        Guid sub = SubProcessor, setting = ProcessorMaxState;
        uint status = NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, ac);
        if (status == 0)
        {
            status = NativeMethods.PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, dc);
        }

        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status);
        }

        if (GetActiveSchemeId() == scheme)
        {
            SetActiveScheme(scheme); // apply immediately
        }
    }

    public static Guid? GetActiveSchemeId()
    {
        try
        {
            if (NativeMethods.PowerGetActiveScheme(IntPtr.Zero, out var ptr) != 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStructure<Guid>(ptr);
            }
            finally
            {
                NativeMethods.LocalFree(ptr);
            }
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<PowerScheme> GetSchemes()
    {
        var result = new List<PowerScheme>();
        try
        {
            for (uint index = 0; index < 64; index++)
            {
                uint size = 16;
                var buffer = new byte[16];
                uint status = NativeMethods.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, NativeMethods.ACCESS_SCHEME, index, buffer, ref size);
                if (status != 0)
                {
                    break; // ERROR_NO_MORE_ITEMS
                }

                var id = new Guid(buffer);
                result.Add(new PowerScheme(id, GetFriendlyName(id)));
            }
        }
        catch
        {
            // Return whatever was enumerated.
        }

        return result;
    }

    public static string GetFriendlyName(Guid id)
    {
        try
        {
            uint size = 0;
            NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, null, ref size);
            if (size == 0)
            {
                return KnownName(id);
            }

            var buffer = new byte[size];
            if (NativeMethods.PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, buffer, ref size) == 0)
            {
                string name = Encoding.Unicode.GetString(buffer).TrimEnd('\0');
                return string.IsNullOrWhiteSpace(name) ? KnownName(id) : name;
            }
        }
        catch
        {
            // Fall through.
        }

        return KnownName(id);
    }

    public static string KnownName(Guid id)
    {
        if (id == Balanced) return "Balanced";
        if (id == HighPerformance) return "High performance";
        if (id == PowerSaver) return "Power saver";
        if (id == UltimatePerformance) return "Ultimate Performance";
        return id.ToString();
    }

    public static void SetActiveScheme(Guid id)
    {
        uint status = NativeMethods.PowerSetActiveScheme(IntPtr.Zero, ref id);
        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status);
        }
    }

    /// <summary>Returns the current power mode overlay, or null if the API is not available.</summary>
    public static Guid? GetOverlay()
    {
        try
        {
            return NativeMethods.PowerGetEffectiveOverlayScheme(out var guid) == 0 ? guid : null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static void SetOverlay(Guid overlay)
    {
        uint status = NativeMethods.PowerSetActiveOverlayScheme(ref overlay);
        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status);
        }
    }

    public static string OverlayName(Guid? overlay)
    {
        if (overlay is null) return Core.Formatting.Unavailable;
        if (overlay == OverlayBestPerformance) return "Best performance";
        if (overlay == OverlayBetterPerformance) return "Better performance";
        if (overlay == OverlayBalanced) return "Balanced";
        if (overlay == OverlayBestEfficiency) return "Best power efficiency";
        return "Custom";
    }
}

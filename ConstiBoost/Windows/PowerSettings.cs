using System;
using System.Collections.Generic;
using System.Linq;

namespace ConstiBoost.WinSys;

/// <summary>One allow-listed power plan setting ConstiBoost may change (AC = plugged in only; battery values stay untouched).</summary>
public sealed record PowerSettingDef(string Key, Guid Subgroup, Guid Setting, string Name, uint Min, uint Max);

/// <summary>
/// Generic read/write of allow-listed power plan settings of the active plan. Only the AC (plugged-in) value
/// is written, so battery life is not affected. Thermal protection is never touched by any of these settings.
/// </summary>
public static class PowerSettings
{
    public static readonly PowerSettingDef BoostMode = new("boost", PowerManager.SubProcessor, new("be337238-0d82-4146-a960-4f3749d470c7"), "Processor performance boost mode", 0, 6);
    public static readonly PowerSettingDef EnergyPreference = new("epp", PowerManager.SubProcessor, new("36687f9e-e3a5-4dbf-b1dc-15eb381c6863"), "Processor energy performance preference", 0, 100);
    public static readonly PowerSettingDef CoreParkingMin = new("cpmincores", PowerManager.SubProcessor, new("0cc5b647-c1df-4637-891a-dec35c318583"), "Processor performance core parking min cores", 0, 100);
    public static readonly PowerSettingDef CoolingPolicy = new("syscoolpol", PowerManager.SubProcessor, new("94d3a615-a899-4ac5-ae2b-e4d8f634367f"), "System cooling policy", 0, 1);
    public static readonly PowerSettingDef SwitchableGraphics = new("dyngfx", new("e276e160-7cb0-43c6-b20b-73f5dce39954"), new("a1662ab2-9d34-4e53-ba8b-2639b9e20857"), "Switchable Dynamic Graphics", 0, 3);
    public static readonly PowerSettingDef AmdPowerPlay = new("atipowerplay", new("f693fb01-e858-4f00-b20f-f30e12ac06d6"), new("191f65b5-d45c-4a4f-8aae-1ab8bfd980e6"), "AMD graphics power (PowerPlay)", 0, 2);
    public static readonly PowerSettingDef WifiPower = new("wifi", new("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1"), new("12bbebe6-58d6-4636-95bb-3217ef867c1a"), "Wireless adapter power saving mode", 0, 3);
    public static readonly PowerSettingDef PcieAspm = new("aspm", new("501a4d13-42af-4429-9fd1-a8218c268e20"), new("ee12f906-d277-404b-b6da-e5fa1a576df5"), "PCI Express link state power management", 0, 2);

    public static IReadOnlyList<PowerSettingDef> All { get; } = new[] { BoostMode, EnergyPreference, CoreParkingMin, CoolingPolicy, SwitchableGraphics, AmdPowerPlay, WifiPower, PcieAspm };

    public static PowerSettingDef? Find(Guid sub, Guid setting) => All.FirstOrDefault(d => d.Subgroup == sub && d.Setting == setting);

    /// <summary>AC value of the active plan, or null when the setting does not exist on this PC.</summary>
    public static uint? ReadAc(PowerSettingDef def, Guid? scheme = null)
    {
        try
        {
            Guid s = scheme ?? PowerManager.GetActiveSchemeId() ?? Guid.Empty;
            if (s == Guid.Empty)
            {
                return null;
            }

            Guid sub = def.Subgroup, setting = def.Setting;
            return NativeMethods.PowerReadACValueIndex(IntPtr.Zero, ref s, ref sub, ref setting, out uint value) == 0 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Writes the AC value (allow-listed settings and ranges only) and re-applies the plan.</summary>
    public static void WriteAc(Guid scheme, Guid sub, Guid setting, uint value)
    {
        var def = Find(sub, setting) ?? throw new UnauthorizedAccessException("Power setting is not on the ConstiBoost allowlist.");
        if (value < def.Min || value > def.Max)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{def.Name}: only {def.Min}–{def.Max} is allowed.");
        }

        uint status = NativeMethods.PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, value);
        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status);
        }

        if (PowerManager.GetActiveSchemeId() == scheme)
        {
            PowerManager.SetActiveScheme(scheme);
        }
    }
}

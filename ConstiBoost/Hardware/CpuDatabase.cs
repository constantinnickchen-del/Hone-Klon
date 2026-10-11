using System.Text.RegularExpressions;

namespace ConstiBoost.Hardware;

/// <summary>
/// Maps CPU model names (as reported by Windows) to their architecture name.
/// Only used for labelling – core/thread counts and clocks always come from Windows.
/// </summary>
public static class CpuDatabase
{
    private static readonly (Regex pattern, string architecture)[] Known =
    {
        (new Regex(@"Ryzen\s+[3579]\s+5[357]00U", RegexOptions.IgnoreCase), "Zen 2 (Lucienne)"),
        (new Regex(@"Ryzen\s+[3579]\s+5[3-9][0-9]5U", RegexOptions.IgnoreCase), "Zen 3 (Barcelo)"),
        (new Regex(@"Ryzen\s+[3579]\s+5[4689]00[UH]", RegexOptions.IgnoreCase), "Zen 3 (Cezanne)"),
        (new Regex(@"Ryzen\s+[3579]\s+4\d00[UH]", RegexOptions.IgnoreCase), "Zen 2 (Renoir)"),
        (new Regex(@"Ryzen\s+[3579]\s+7[3]\d0U", RegexOptions.IgnoreCase), "Zen 2 (Mendocino)"),
        (new Regex(@"Ryzen\s+[3579]\s+7[3-5]35[UH]", RegexOptions.IgnoreCase), "Zen 3+ (Rembrandt-R)"),
        (new Regex(@"Ryzen\s+[3579]\s+7[3-5]30U", RegexOptions.IgnoreCase), "Zen 3 (Barcelo-R)"),
        (new Regex(@"Ryzen\s+[3579]\s+6\d00[UH]", RegexOptions.IgnoreCase), "Zen 3+ (Rembrandt)"),
        (new Regex(@"Ryzen\s+[3579]\s+7\d40[UH]", RegexOptions.IgnoreCase), "Zen 4 (Phoenix)"),
        (new Regex(@"Ryzen\s+[3579]\s+8\d40[UH]S?", RegexOptions.IgnoreCase), "Zen 4 (Hawk Point)"),
        (new Regex(@"Ryzen\s+[3579]\s+3\d00U", RegexOptions.IgnoreCase), "Zen+ (Picasso)"),
    };

    private static readonly Regex ModelRegex = new(@"Ryzen\s+[3579]\s+\d{4}[A-Z]{0,2}|Core\(TM\)\s+\S+|Core\s+Ultra\s+\d\s+\d{3}[A-Z]?", RegexOptions.IgnoreCase);

    public static string? Architecture(string cpuName)
    {
        foreach (var (pattern, architecture) in Known)
        {
            if (pattern.IsMatch(cpuName))
            {
                return architecture;
            }
        }

        return null;
    }

    /// <summary>Short model name, e.g. "AMD Ryzen 7 5700U" from "AMD Ryzen 7 5700U with Radeon Graphics".</summary>
    public static string ShortName(string cpuName)
    {
        var match = ModelRegex.Match(cpuName);
        if (!match.Success)
        {
            return cpuName;
        }

        string vendor = cpuName.Contains("AMD", System.StringComparison.OrdinalIgnoreCase) ? "AMD " : cpuName.Contains("Intel", System.StringComparison.OrdinalIgnoreCase) ? "Intel " : string.Empty;
        return vendor + match.Value;
    }
}

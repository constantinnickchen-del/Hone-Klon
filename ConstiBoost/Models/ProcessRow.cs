using ConstiBoost.Core;
using ConstiBoost.WinSys;
using ConstiBoost.Localization;

namespace ConstiBoost.Models;

public sealed class ProcessRow : ObservableObject
{
    private double _cpu;
    private long _ram;
    private double? _gpu;
    private string _status = "Running";

    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Path { get; init; }
    public ProcessProtection Protection { get; init; }

    public double Cpu
    {
        get => _cpu;
        set
        {
            if (SetProperty(ref _cpu, value))
            {
                OnPropertyChanged(nameof(CpuText));
            }
        }
    }

    public long Ram
    {
        get => _ram;
        set
        {
            if (SetProperty(ref _ram, value))
            {
                OnPropertyChanged(nameof(RamText));
            }
        }
    }

    public double? Gpu
    {
        get => _gpu;
        set
        {
            if (SetProperty(ref _gpu, value))
            {
                OnPropertyChanged(nameof(GpuText));
                OnPropertyChanged(nameof(GpuSort));
            }
        }
    }

    /// <summary>Shown in the app language; the stored value stays English ("Running", "Service", …).</summary>
    public string Status
    {
        get => L.T(_status);
        set => SetProperty(ref _status, value);
    }

    public string CpuText => _cpu.ToString("0.0") + " %";
    public string RamText => Formatting.Bytes(_ram);
    public string GpuText => _gpu.HasValue ? _gpu.Value.ToString("0.0") + " %" : "–";
    public double GpuSort => _gpu ?? -1;

    public string ProtectionText => Protection switch
    {
        ProcessProtection.Critical => L.T("Protected"),
        ProcessProtection.Caution => L.T("Caution"),
        _ => string.Empty,
    };

    public bool CanClose => Protection != ProcessProtection.Critical;
}

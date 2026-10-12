using System;
using System.Collections.Generic;
using System.Linq;
using ConstiBoost.Hardware;
using ConstiBoost.Models;
using ConstiBoost.WinSys;

using ConstiBoost.Localization;

namespace ConstiBoost.Games;

public sealed record FpsEstimate(string Setting, string Resolution, int Low, int High, bool AboveRefresh, bool IsCurrent)
{
    public string RangeText => $"≈ {Low}–{High} FPS";
}

public sealed record EstimateContext(
    GpuClass? Gpu,
    bool SingleChannelRam,
    long RamBytes,
    int Threads,
    bool OnBattery,
    bool PowerSaving,
    bool CoolProfile,
    double? RecentTemperature,
    int? RefreshRate,
    int NativeWidth,
    int NativeHeight);

/// <summary>
/// Transparent FPS estimate: reference FPS × GPU class × preset × resolution × RAM channels × power × heat.
/// Every factor is shown to the user. Real FPS depend on map, scene, drivers and game updates.
/// </summary>
public static class FpsEstimator
{
    public static EstimateContext BuildContext(HardwareInfo hw, double? temperature)
    {
        var gpu = GpuDatabase.Classify(hw);
        var scheme = PowerManager.GetActiveSchemeId();
        var overlay = scheme == PowerManager.Balanced ? PowerManager.GetOverlay() : null;
        var primary = hw.Gpus.FirstOrDefault();
        return new EstimateContext(
            gpu,
            hw.RamModules == 1,
            hw.TotalRamBytes ?? 0,
            hw.CpuThreads ?? 8,
            hw.HasBattery && SystemInfo.GetPowerStatus().onAc == false,
            scheme == PowerManager.PowerSaver || overlay == PowerManager.OverlayBestEfficiency,
            Services.GamingModeService.Instance.ActiveProfile == GamingProfile.Cool,
            temperature,
            primary?.RefreshRate,
            primary?.HorizontalResolution ?? 1920,
            primary?.VerticalResolution ?? 1080);
    }

    public static List<(string name, string value)> Factors(EstimateContext c)
    {
        var list = new List<(string, string)>
        {
            ("Graphics", c.Gpu is null ? L.T("Unknown GPU – no estimate") : $"{c.Gpu.Name} (performance class {c.Gpu.Index:0.00}× Vega 8)"),
        };
        if (c.Gpu?.Integrated == true)
        {
            list.Add(("Memory", c.SingleChannelRam ? L.T("Single-channel RAM: about −25 % for integrated graphics") : L.T("Dual-channel RAM (good for integrated graphics)")));
        }

        if (c.OnBattery) list.Add(("Power", L.T("On battery: about −15 %")));
        if (c.PowerSaving) list.Add(("Power mode", L.T("Power saving: about −20 %")));
        if (c.CoolProfile) list.Add(("Gaming Mode", L.T("COOL profile (CPU boost off): about −15 %")));
        if (c.RecentTemperature >= 90) list.Add(("Heat", $"{c.RecentTemperature:0} °C measured – possible throttling: about −10 %"));
        if (c.RefreshRate is int hz) list.Add(("Display", $"{hz} Hz – FPS above {hz} are not visible on this screen"));
        return list;
    }

    public static FpsEstimate? Estimate(GameProfile game, GamePreset preset, int renderWidth, int renderHeight, double resolutionScale, EstimateContext c, bool isCurrent = false)
    {
        if (c.Gpu is null)
        {
            return null;
        }

        double gpuFactor = Math.Pow(c.Gpu.Index, 0.85);
        double pixels = renderWidth * renderHeight * resolutionScale * resolutionScale;
        double resFactor = Math.Pow(1920.0 * 1080.0 / Math.Max(pixels, 320 * 240), 0.75);
        double ram = c.Gpu.Integrated && c.SingleChannelRam ? 0.75 : 1.0;
        if (c.Gpu.Integrated && c.RamBytes is > 0 and < 7L * 1024 * 1024 * 1024)
        {
            ram *= 0.9;
        }

        double power = (c.OnBattery ? 0.85 : 1.0) * (c.PowerSaving ? 0.8 : 1.0) * (c.CoolProfile ? 0.85 : 1.0) * (c.RecentTemperature >= 90 ? 0.9 : 1.0);
        double cpuCap = game.CpuCap * Math.Pow(Math.Max(c.Threads, 2) / 16.0, 0.3) * (c.CoolProfile ? 0.8 : 1.0) * (c.OnBattery ? 0.85 : 1.0);

        double factor = gpuFactor * preset.Factor * resFactor * ram * power;
        int low = (int)Math.Round(Math.Min(game.LowFps * factor, cpuCap * 0.85));
        int high = (int)Math.Round(Math.Min(game.HighFps * factor, cpuCap));
        low = Math.Max(1, low);
        high = Math.Max(low, high);
        string res = resolutionScale < 0.999 ? $"{renderWidth}×{renderHeight} @ {resolutionScale * 100:0} %" : $"{renderWidth}×{renderHeight}";
        return new FpsEstimate(preset.Name, res, low, high, c.RefreshRate is int hz && low > hz, isCurrent);
    }
}

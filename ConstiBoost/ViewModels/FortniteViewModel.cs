using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Fortnite;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class PresetViewModel : ObservableObject
{
    private ImpactLevel _impact;
    private bool _isSelected;

    public PresetViewModel(FortnitePreset preset) => Preset = preset;

    public FortnitePreset Preset { get; }
    public string Name => Preset.Name;
    public string Subtitle => Preset.Subtitle;

    public ImpactLevel Impact
    {
        get => _impact;
        set
        {
            if (SetProperty(ref _impact, value))
            {
                OnPropertyChanged(nameof(ImpactText));
            }
        }
    }

    public string ImpactText => Impact.ToString().ToUpperInvariant();

    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
}

public sealed class FortniteViewModel : PageViewModel
{
    private readonly DispatcherTimer _processTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private FortniteInstall? _install;
    private FortniteConfigSnapshot _config = new();
    private PresetViewModel? _selectedPreset;
    private int _resolution = 100;
    private bool _disableVSync = true;
    private bool _disableMotionBlur = true;
    private bool _capFps;
    private int _fpsCap = 60;
    private bool _fullscreen = true;
    private bool _isRunning;
    private string _processText = "Fortnite is not running";
    private string _shaderCacheText = "–";
    private bool _isLoading;
    private bool _isIntegrated;
    private bool _isLowEnd;
    private string _reflexText = string.Empty;
    private string _deviceRecommendation = string.Empty;

    public FortniteViewModel()
    {
        Presets = new ObservableCollection<PresetViewModel>(FortnitePreset.All.Select(p => new PresetViewModel(p)));
        SelectPresetCommand = new RelayCommand(p => SelectPreset(p as PresetViewModel));
        ApplyPresetCommand = new AsyncRelayCommand(ApplyPresetAsync, () => SelectedPreset != null && ConfigExists);
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        RaisePriorityCommand = new RelayCommand(() => Report(FortnitePriorityService.SetAboveNormal()), () => IsRunning);
        ResetPriorityCommand = new RelayCommand(() => Report(FortnitePriorityService.Reset()), () => IsRunning);
        ClearShaderCacheCommand = new AsyncRelayCommand(ClearShaderCacheAsync);
        CloseAppsCommand = new AsyncRelayCommand(CloseAppsAsync);
        LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync);
        LaunchCommand = new RelayCommand(Launch);
        _processTimer.Tick += (_, _) => UpdateProcess();
    }

    public override string Key => "fortnite";
    public override string Title => Localization.L.T("Fortnite");
    public override string Icon => "";

    public ObservableCollection<PresetViewModel> Presets { get; }
    public ObservableCollection<AppGroup> BackgroundApps { get; } = new();
    public ObservableCollection<string> MissingKeys { get; } = new();

    public GamingModeViewModel GamingMode => GamingModeViewModel.Shared;

    public ICommand SelectPresetCommand { get; }
    public ICommand ApplyPresetCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand RaisePriorityCommand { get; }
    public ICommand ResetPriorityCommand { get; }
    public ICommand ClearShaderCacheCommand { get; }
    public ICommand CloseAppsCommand { get; }
    public ICommand LoadAppsCommand { get; }
    public ICommand LaunchCommand { get; }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool IsInstalled => _install != null;
    public string InstallPath => _install?.InstallPath ?? "Not found";
    public string InstallSource => _install is null ? "Fortnite was not found in the Epic Games Launcher manifests or common install folders." : "Detected via " + _install.Source;
    public string Version => _install?.Version ?? "Unknown";
    public bool ConfigExists => _config.Exists;
    public string ConfigPath => FortniteConfigService.ConfigPath;
    public string ConfigStatus => _config.Exists ? "Settings file found" : "Settings file not found – start Fortnite once so it creates its settings.";

    public string CurrentRenderingMode => _config.RenderingMode;
    public string Current3DResolution => _config.Get("sg.ResolutionQuality").Split('.')[0] + (_config.Values.ContainsKey("sg.ResolutionQuality") ? " %" : string.Empty);
    public string CurrentShadows => Level(_config.Get("sg.ShadowQuality"));
    public string CurrentViewDistance => Level(_config.Get("sg.ViewDistanceQuality"));
    public string CurrentTextures => Level(_config.Get("sg.TextureQuality"));
    public string CurrentEffects => Level(_config.Get("sg.EffectsQuality"));
    public string CurrentVSync => _config.Get("bUseVSync") is "–" ? "–" : (_config.Get("bUseVSync").Equals("True", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF");
    public string CurrentMotionBlur => _config.Get("bMotionBlur") is "–" ? "Not in file (set in-game)" : (_config.Get("bMotionBlur").Equals("True", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF");
    public string CurrentFrameLimit => _config.Get("FrameRateLimit") is "–" ? "–" : (_config.Get("FrameRateLimit").StartsWith("0") ? "Unlimited" : _config.Get("FrameRateLimit").Split('.')[0] + " FPS");
    public string CurrentWindowMode => _config.Get("FullscreenMode") switch { "0" => "Fullscreen", "1" => "Windowed Fullscreen", "2" => "Windowed", _ => "–" };

    public bool IsIntegrated { get => _isIntegrated; private set => SetProperty(ref _isIntegrated, value); }
    public bool IsLowEnd { get => _isLowEnd; private set => SetProperty(ref _isLowEnd, value); }
    public string ReflexText { get => _reflexText; private set => SetProperty(ref _reflexText, value); }
    public string DeviceRecommendation { get => _deviceRecommendation; private set => SetProperty(ref _deviceRecommendation, value); }

    public PresetViewModel? SelectedPreset { get => _selectedPreset; private set => SetProperty(ref _selectedPreset, value); }

    public int Resolution
    {
        get => _resolution;
        set
        {
            if (SetProperty(ref _resolution, Math.Clamp(value, 50, 100)))
            {
                OnPropertyChanged(nameof(ResolutionHint));
                UpdateImpacts();
            }
        }
    }

    public string ResolutionHint => Resolution switch
    {
        >= 95 => "Sharpest image, highest GPU load.",
        >= 80 => "Slightly softer image, noticeably less GPU load on integrated graphics.",
        >= 65 => "Visibly softer image, much less GPU load – good for weak iGPUs.",
        _ => "Blurry image, lowest GPU load. Only if nothing else helps.",
    };

    public bool DisableVSync { get => _disableVSync; set => SetProperty(ref _disableVSync, value); }
    public bool DisableMotionBlur { get => _disableMotionBlur; set => SetProperty(ref _disableMotionBlur, value); }
    public bool CapFps { get => _capFps; set => SetProperty(ref _capFps, value); }
    public int FpsCap { get => _fpsCap; private set => SetProperty(ref _fpsCap, value); }
    public bool Fullscreen { get => _fullscreen; set => SetProperty(ref _fullscreen, value); }

    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public string ProcessText { get => _processText; private set => SetProperty(ref _processText, value); }
    public string ShaderCacheText { get => _shaderCacheText; private set => SetProperty(ref _shaderCacheText, value); }

    public override void OnNavigatedTo()
    {
        _ = LoadAsync();
        UpdateProcess();
        _processTimer.Start();
    }

    public override void OnNavigatedFrom()
    {
        _processTimer.Stop();
    }

    private static string Level(string raw) => raw switch
    {
        "0" => "Low / Off",
        "1" => "Medium",
        "2" => "High",
        "3" => "Epic",
        "4" => "Cinematic",
        _ => raw,
    };

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var scan = ScanService.Instance.Last ?? await ScanService.Instance.ScanAsync();
            (_install, _config, long shaderBytes) = await Task.Run(() =>
                (FortniteLocator.Locate(), FortniteConfigService.Read(), CleanerService.ShaderCachePaths().Sum(p => CleanerService.Measure(p, 0).bytes)));
            ShaderCacheText = Formatting.Bytes(shaderBytes);

            IsIntegrated = !scan.Profile.HasDedicatedGpu;
            IsLowEnd = scan.Profile.IsLowPowerIntegrated;
            int refresh = scan.Hardware.Gpus.FirstOrDefault()?.RefreshRate ?? 60;
            FpsCap = refresh is >= 30 and <= 360 ? refresh : 60;
            CapFps = IsLowEnd;

            bool nvidia = scan.Hardware.Gpus.Any(g => g.Vendor == "NVIDIA" && !g.IsIntegrated);
            ReflexText = nvidia
                ? "Available on your NVIDIA GPU: set 'NVIDIA Reflex Low Latency' to On in Fortnite (Settings > Video > Advanced Graphics). Reflex is only available in DirectX 11/12 modes."
                : "Unavailable on this system – NVIDIA Reflex requires a supported NVIDIA graphics card. ConstiBoost does not enable it.";

            DeviceRecommendation = IsLowEnd
                ? $"{scan.Profile.Title}: use PERFORMANCE MODE with LOW settings, 3D resolution 70–85 % (depending on how sharp you want it), VSync OFF, Motion Blur OFF, Shadows OFF and an FPS cap at your display's {FpsCap} Hz for stable frame times and lower temperatures."
                : scan.Profile.HasDedicatedGpu
                    ? "Dedicated GPU detected: BALANCED or QUALITY usually give the best mix. Use MAX FPS for competitive play."
                    : "Use MAX FPS or BALANCED depending on how smooth the game runs.";

            if (SelectedPreset is null)
            {
                SelectPreset(Presets.First(p => p.Preset.Id == (IsLowEnd ? "maxfps" : scan.Profile.HasDedicatedGpu ? "balanced" : "maxfps")));
            }
            else
            {
                UpdateImpacts();
            }

            foreach (string name in new[]
                     {
                         nameof(IsInstalled), nameof(InstallPath), nameof(InstallSource), nameof(Version), nameof(ConfigExists), nameof(ConfigStatus),
                         nameof(CurrentRenderingMode), nameof(Current3DResolution), nameof(CurrentShadows), nameof(CurrentViewDistance),
                         nameof(CurrentTextures), nameof(CurrentEffects), nameof(CurrentVSync), nameof(CurrentMotionBlur), nameof(CurrentFrameLimit),
                         nameof(CurrentWindowMode),
                     })
            {
                OnPropertyChanged(name);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectPreset(PresetViewModel? preset)
    {
        if (preset is null)
        {
            return;
        }

        foreach (var p in Presets)
        {
            p.IsSelected = ReferenceEquals(p, preset);
        }

        SelectedPreset = preset;
        Resolution = IsIntegrated ? preset.Preset.ResolutionIntegrated : preset.Preset.ResolutionDedicated;
        UpdateImpacts();
    }

    private void UpdateImpacts()
    {
        foreach (var p in Presets)
        {
            int res = ReferenceEquals(p, SelectedPreset) ? Resolution : (IsIntegrated ? p.Preset.ResolutionIntegrated : p.Preset.ResolutionDedicated);
            p.Impact = FortniteConfigService.EstimateImpact(p.Preset, _config, res);
        }
    }

    private async Task ApplyPresetAsync()
    {
        var preset = SelectedPreset?.Preset;
        if (preset is null)
        {
            return;
        }

        if (FortniteConfigService.IsFortniteRunning())
        {
            DialogService.Info("Close Fortnite first", "Fortnite overwrites its settings file when it exits. Close Fortnite completely, then apply the preset again.");
            return;
        }

        var details = preset.Display.Select(d => $"{d.Label}: {d.Value}").ToList();
        details.Add($"3D Resolution: {Resolution} %");
        if (CapFps)
        {
            details.Add($"Frame rate limit: {FpsCap} FPS");
        }

        if (Fullscreen)
        {
            details.Add("Window mode: Fullscreen");
        }

        details.Add($"Estimated impact: {SelectedPreset!.ImpactText} (no FPS promise – depends on map, scene and hardware)");
        details.Add("GameUserSettings.ini is backed up first.");
        if (!DialogService.Confirm($"Apply {preset.Name}?", "ConstiBoost will update Fortnite's own settings file:", details, "APPLY PRESET"))
        {
            return;
        }

        var backup = BackupSession.Create($"Fortnite preset {preset.Name}");
        var options = new PresetOptions(Resolution, DisableVSync, DisableMotionBlur, CapFps ? FpsCap : null, Fullscreen);
        var (result, missing) = await Task.Run(() => FortniteConfigService.Apply(preset, options, backup));
        MissingKeys.Clear();
        foreach (string key in missing)
        {
            MissingKeys.Add(key);
        }

        Report(result);
        if (result.Success && preset.RenderingMode == "Performance" && CurrentRenderingMode != "Performance")
        {
            DialogService.Info(
                "One step in Fortnite",
                "ConstiBoost does not change Fortnite's rendering mode itself. In Fortnite open Settings > Video > Advanced Graphics and set 'Rendering Mode' to PERFORMANCE, then restart Fortnite.",
                MissingKeys.Count > 0 ? MissingKeys.Select(k => $"Not in your settings file (set in-game): {k}") : null);
        }

        MainViewModel.Instance?.Backups.Reload();
        await LoadAsync();
    }

    private void UpdateProcess()
    {
        var info = FortnitePriorityService.GetRunning();
        IsRunning = info != null;
        ProcessText = info is null
            ? "Fortnite is not running"
            : $"Running · PID {info.Pid} · Priority {(info.Priority?.ToString() ?? "protected by anti-cheat")} · RAM {Formatting.Bytes(info.WorkingSet)}"
              + (FortnitePriorityService.IsChangedByConstiBoost ? " · changed by ConstiBoost" : string.Empty);
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task ClearShaderCacheAsync()
    {
        if (FortniteConfigService.IsFortniteRunning())
        {
            DialogService.Info("Close Fortnite first", "Shader caches are in use while Fortnite is running.");
            return;
        }

        if (!DialogService.Confirm("Clear shader caches?",
                "DirectX and GPU driver shader caches will be deleted. They are rebuilt automatically – the first match afterwards can stutter while shaders compile again. Recommended after GPU driver updates or permanent stutter.",
                CleanerService.ShaderCachePaths().Where(System.IO.Directory.Exists), "CLEAR"))
        {
            return;
        }

        long freed = await Task.Run(() => CleanerService.ShaderCachePaths().Sum(p => CleanerService.DeleteContents(p).FreedBytes));
        Logging.ActionLog.Success("Clear shader caches (Fortnite page)", ShaderCacheText, Formatting.Bytes(freed) + " freed");
        NotificationService.Instance.Show($"Shader caches cleared: {Formatting.Bytes(freed)} freed.", NotificationKind.Success);
        await LoadAsync();
    }

    private async Task LoadAppsAsync()
    {
        var groups = await Task.Run(BackgroundAppsService.GetCandidates);
        BackgroundApps.Clear();
        foreach (var g in groups)
        {
            g.IsSelected = g.IsKnownBackgroundApp && !BackgroundAppsService.BrowserProcesses.Contains(g.ProcessName);
            BackgroundApps.Add(g);
        }
    }

    private async Task CloseAppsAsync()
    {
        var selected = BackgroundApps.Where(a => a.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        if (!DialogService.Confirm("Close apps?", "These apps will be closed. Unsaved work in normal apps is protected – they are only asked to close.", selected.Select(s => s.DisplayName), "CLOSE"))
        {
            return;
        }

        int ok = 0;
        foreach (var group in selected)
        {
            var (closed, message) = await BackgroundAppsService.CloseGroupAsync(group);
            if (closed)
            {
                ok++;
            }
            else
            {
                NotificationService.Instance.Show(message, NotificationKind.Warning);
            }
        }

        NotificationService.Instance.Show($"{ok} app(s) closed.", NotificationKind.Success);
        await LoadAppsAsync();
    }

    private void Launch()
    {
        try
        {
            // Official Epic Games Launcher URI scheme (uses the app id from the launcher manifest when known).
            string appId = _install?.LauncherAppId is { } id ? Uri.EscapeDataString(id) : "Fortnite";
            Process.Start(new ProcessStartInfo($"com.epicgames.launcher://apps/{appId}?action=launch&silent=true") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            NotificationService.Instance.Show("Fortnite couldn't be started via the Epic Games Launcher: " + ex.Message, NotificationKind.Error);
        }
    }

    private static void Report(OperationResult result)
    {
        NotificationService.Instance.Show(
            result.Message + (result.TechnicalDetails is null ? string.Empty : $" ({result.TechnicalDetails})"),
            result.Success ? NotificationKind.Success : NotificationKind.Warning);
    }
}

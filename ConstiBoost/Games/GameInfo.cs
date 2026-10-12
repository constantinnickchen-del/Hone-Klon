using System;
using System.Collections.Generic;
using System.Linq;
using ConstiBoost.Core;

namespace ConstiBoost.Games;

public enum GameSource
{
    Steam,
    Epic,
    Installed, // Windows "Apps & features" entry (GOG, EA, Ubisoft, Riot, Battle.net, standalone…)
    Store,     // Microsoft Store app package
    Xbox,      // Xbox app / Game Pass (GDK, XboxGames folder)
    Minecraft, // Minecraft: Java Edition (Minecraft Launcher)
}

/// <summary>One installed game. Everything here is read from launcher files or the Windows registry.</summary>
public sealed class GameInfo : ObservableObject
{
    private bool _isExpanded;
    private DateTime? _lastPlayed;
    private string _lastPlayedSource = string.Empty;
    private long? _sizeBytes;
    private bool _isRunning;
    private bool _isFocused;

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public GameSource Source { get; init; }
    public string SourceText => Source switch
    {
        GameSource.Steam => "Steam",
        GameSource.Epic => "Epic Games",
        GameSource.Store => "Microsoft Store",
        GameSource.Xbox => "Xbox app",
        GameSource.Minecraft => "Minecraft Launcher",
        _ => string.IsNullOrWhiteSpace(Publisher) ? "Installed" : Publisher,
    };

    public string? Publisher { get; init; }
    public string InstallPath { get; init; } = string.Empty;
    public string? MainExecutable { get; init; }

    public string? SteamAppId { get; init; }
    public string? EpicAppName { get; init; }
    public string? EpicLaunchId { get; init; }
    public string? UninstallString { get; init; }
    public string? EpicLaunchExecutable { get; init; }

    /// <summary>Store/Xbox package (for launch via shell:AppsFolder and uninstall via Remove-AppxPackage).</summary>
    public string? PackageFullName { get; init; }
    public string? AppUserModelId { get; init; }
    public string? LaunchUri { get; init; }

    /// <summary>Folders the game's processes run from (install folder + e.g. the Java runtime for Minecraft).</summary>
    public List<string> ExtraProcessRoots { get; init; } = new();

    public IEnumerable<string> ProcessRoots => new[] { InstallPath }.Concat(ExtraProcessRoots).Where(p => !string.IsNullOrWhiteSpace(p));

    /// <summary>Key into the FPS estimation database (null = no estimate data).</summary>
    public string? ProfileKey { get; init; }

    public bool IsFortnite => string.Equals(EpicAppName, "Fortnite", StringComparison.OrdinalIgnoreCase) ||
                              Name.Equals("Fortnite", StringComparison.OrdinalIgnoreCase);

    public string Initials => Name.Length == 0 ? "?" : Name.Length == 1 ? Name.ToUpperInvariant() : string.Concat(Name[0], char.ToUpperInvariant(Name.Length > 1 ? Name[1] : ' ')).Trim();

    public long? SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(SizeText));
            }
        }
    }

    public string SizeText => _sizeBytes is > 0 ? Formatting.Bytes(_sizeBytes.Value) : "Size unknown";

    public DateTime? LastPlayed
    {
        get => _lastPlayed;
        set
        {
            if (SetProperty(ref _lastPlayed, value))
            {
                OnPropertyChanged(nameof(LastPlayedText));
            }
        }
    }

    public string LastPlayedSource
    {
        get => _lastPlayedSource;
        set
        {
            if (SetProperty(ref _lastPlayedSource, value))
            {
                OnPropertyChanged(nameof(LastPlayedText));
            }
        }
    }

    public string LastPlayedText
    {
        get
        {
            if (_lastPlayed is not DateTime d)
            {
                return "Last played: unknown";
            }

            var age = DateTime.Now - d;
            string relative = age.TotalMinutes < 60 ? "just now"
                : age.TotalHours < 24 ? $"{(int)age.TotalHours} h ago"
                : age.TotalDays < 2 ? "yesterday"
                : age.TotalDays < 60 ? $"{(int)age.TotalDays} days ago"
                : d.ToString("dd.MM.yyyy");
            return $"Last played: {relative}";
        }
    }

    public bool IsRunning { get => _isRunning; set => SetProperty(ref _isRunning, value); }
    public bool IsFocused { get => _isFocused; set => SetProperty(ref _isFocused, value); }
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    public List<string> ExecutableNames { get; init; } = new();
}

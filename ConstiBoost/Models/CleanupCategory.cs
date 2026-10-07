using System.Collections.Generic;
using ConstiBoost.Core;

namespace ConstiBoost.Models;

public sealed class CleanupCategory : ObservableObject
{
    private bool _isSelected = true;
    private long _sizeBytes;
    private int _fileCount;
    private bool _isScanned;
    private string _note = string.Empty;

    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "\uE74D";
    public List<string> Paths { get; init; } = new();
    public List<string> AdminPaths { get; init; } = new();

    /// <summary>Files newer than this are skipped (protects running installers/apps).</summary>
    public double MinAgeHours { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public long SizeBytes
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

    public int FileCount
    {
        get => _fileCount;
        set => SetProperty(ref _fileCount, value);
    }

    public bool IsScanned
    {
        get => _isScanned;
        set
        {
            if (SetProperty(ref _isScanned, value))
            {
                OnPropertyChanged(nameof(SizeText));
            }
        }
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public string SizeText => IsScanned ? Formatting.Bytes(SizeBytes) : "–";
}

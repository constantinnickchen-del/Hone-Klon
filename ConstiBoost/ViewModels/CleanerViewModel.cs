using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class CleanerViewModel : PageViewModel
{
    private bool _isBusy;
    private string _statusText = "Scan to see how much space can be freed.";
    private string _browserWarning = string.Empty;
    private bool _scannedOnce;

    public CleanerViewModel()
    {
        Categories = new ObservableCollection<CleanupCategory>(CleanerService.GetCategories());
        foreach (var c in Categories)
        {
            c.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CleanupCategory.IsSelected))
                {
                    OnPropertyChanged(nameof(EstimateText));
                }
            };
        }

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsBusy && Categories.Any(c => c.IsSelected && c.IsScanned));
    }

    public override string Key => "cleaner";
    public override string Title => "Cleaner";
    public override string Icon => "";

    public ObservableCollection<CleanupCategory> Categories { get; }
    public ObservableCollection<string> Results { get; } = new();

    public ICommand ScanCommand { get; }
    public ICommand CleanCommand { get; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string BrowserWarning { get => _browserWarning; private set => SetProperty(ref _browserWarning, value); }

    public string EstimateText
    {
        get
        {
            long bytes = Categories.Where(c => c.IsSelected && c.IsScanned).Sum(c => c.SizeBytes);
            return Categories.Any(c => c.IsScanned)
                ? $"This will remove approximately {Formatting.Megabytes(bytes)}."
                : "Scan first to see the size.";
        }
    }

    public override void OnNavigatedTo()
    {
        if (!_scannedOnce && !IsBusy)
        {
            _ = ScanAsync();
        }
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "Scanning…";
        try
        {
            var fresh = CleanerService.GetCategories().ToDictionary(c => c.Id);
            foreach (var category in Categories)
            {
                var paths = fresh.TryGetValue(category.Id, out var f) ? f.Paths.Concat(f.AdminPaths).ToList() : category.Paths.Concat(category.AdminPaths).ToList();
                var (bytes, files) = await Task.Run(() =>
                {
                    long b = 0;
                    int n = 0;
                    foreach (string p in paths)
                    {
                        var m = CleanerService.Measure(p, category.MinAgeHours);
                        b += m.bytes;
                        n += m.files;
                    }

                    return (b, n);
                });
                category.SizeBytes = bytes;
                category.FileCount = files;
                category.IsScanned = true;
                category.Note = category.AdminPaths.Count > 0 && !WinSys.SystemInfo.IsAdministrator
                    ? "Some folders need administrator permission (UAC) – sizes of protected files may be incomplete."
                    : string.Empty;
            }

            var browsers = CleanerService.RunningBrowsers();
            BrowserWarning = browsers.Count > 0
                ? $"Browser running ({string.Join(", ", browsers)}): close it before cleaning the browser cache, otherwise files in use are skipped."
                : string.Empty;
            StatusText = $"Scan finished at {DateTime.Now:HH:mm}.";
            _scannedOnce = true;
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(EstimateText));
        }
    }

    private async Task CleanAsync()
    {
        var selected = Categories.Where(c => c.IsSelected && c.IsScanned).ToList();
        var details = selected.Select(c => $"{c.Name}: {c.SizeText}").ToList();
        details.Add("Never touched: Documents, Pictures, Desktop, Downloads, game saves, cookies and passwords.");
        if (!DialogService.Confirm("Clean now?", EstimateText, details, "CLEAN"))
        {
            return;
        }

        IsBusy = true;
        Results.Clear();
        try
        {
            var adminPaths = new List<string>();
            double adminMinAge = 0;
            foreach (var category in selected)
            {
                StatusText = $"Cleaning {category.Name}…";
                var result = await Task.Run(() =>
                {
                    long freed = 0;
                    int deleted = 0;
                    int skipped = 0;
                    foreach (string path in category.Paths)
                    {
                        var r = CleanerService.DeleteContents(path, category.MinAgeHours);
                        freed += r.FreedBytes;
                        deleted += r.DeletedFiles;
                        skipped += r.SkippedFiles;
                    }

                    return new CleanResult(freed, deleted, skipped);
                });

                adminPaths.AddRange(category.AdminPaths.Where(System.IO.Directory.Exists));
                adminMinAge = Math.Max(adminMinAge, category.MinAgeHours);
                Results.Add($"✓ {category.Name}: {Formatting.Bytes(result.FreedBytes)} removed ({result.DeletedFiles} files, {result.SkippedFiles} skipped/in use)");
                ActionLog.Success("Cleaner: " + category.Name, category.SizeText, $"{Formatting.Bytes(result.FreedBytes)} freed");
            }

            if (adminPaths.Count > 0)
            {
                StatusText = "Cleaning protected folders (administrator permission)…";
                var elevated = await ElevationService.RunAsync(new ElevatedTask
                {
                    Operation = ElevatedOperations.CleanPaths,
                    Paths = adminPaths,
                    MinAgeHours = adminMinAge,
                });
                if (elevated.Success)
                {
                    Results.Add($"✓ Protected folders: {Formatting.Bytes(elevated.FreedBytes)} removed ({elevated.DeletedFiles} files, {elevated.SkippedFiles} skipped)");
                    ActionLog.Success("Cleaner: protected folders", null, $"{Formatting.Bytes(elevated.FreedBytes)} freed");
                }
                else
                {
                    Results.Add("✕ Protected folders skipped: " + elevated.Message);
                    ActionLog.Failed("Cleaner: protected folders", null, null, elevated.Message);
                }
            }

            NotificationService.Instance.Show("Cleaning finished.", NotificationKind.Success);
        }
        catch (Exception ex)
        {
            AppLog.Error("Cleaner failed", ex);
            Results.Add("✕ Cleaning couldn't be completed. Details were written to the log.");
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync();
    }
}

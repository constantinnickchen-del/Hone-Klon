using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class StartupViewModel : PageViewModel
{
    private BackupSession? _session;
    private bool _isLoading;

    public StartupViewModel()
    {
        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        ToggleCommand = new AsyncRelayCommand(p => ToggleAsync(p as StartupEntry));
        OpenTaskManagerCommand = new RelayCommand(() => Safe.Try(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe", "/0 /startup") { UseShellExecute = true }), "Open Task Manager"));
    }

    public override string Key => "startup";
    public override string Title => "Startup";
    public override string Icon => "";

    public ObservableCollection<StartupEntry> Entries { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand ToggleCommand { get; }
    public ICommand OpenTaskManagerCommand { get; }

    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }

    public string SummaryText => $"{Entries.Count(e => e.IsEnabled)} of {Entries.Count} startup apps enabled · {Entries.Count(e => e.IsProtected)} protected";

    public override void OnNavigatedTo() => _ = LoadAsync();

    public override void OnNavigatedFrom() => _session = null;

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await Task.Run(StartupService.GetEntries);
            Entries.Clear();
            foreach (var e in list)
            {
                Entries.Add(e);
            }

            OnPropertyChanged(nameof(SummaryText));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleAsync(StartupEntry? entry)
    {
        if (entry is null || entry.IsBusy)
        {
            return;
        }

        // The checkbox already flipped IsEnabled through binding – that is the requested state.
        bool requested = entry.IsEnabled;
        entry.IsEnabled = !requested;
        if (entry.IsProtected)
        {
            NotificationService.Instance.Show($"{entry.Name} is protected: {entry.ProtectionReason}", NotificationKind.Warning);
            return;
        }

        entry.IsBusy = true;
        try
        {
            _session ??= BackupSession.Create("Startup apps");
            var result = await StartupService.SetEnabledAsync(entry, requested, _session);
            NotificationService.Instance.Show(result.Message, result.Success ? NotificationKind.Success : NotificationKind.Warning);
            MainViewModel.Instance?.Backups.Reload();
        }
        finally
        {
            entry.IsBusy = false;
            OnPropertyChanged(nameof(SummaryText));
        }
    }
}

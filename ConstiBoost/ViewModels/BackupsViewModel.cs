using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Backup;
using ConstiBoost.Core;
using ConstiBoost.Localization;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class BackupItem
{
    public BackupItem(BackupSession session) => Session = session;

    public BackupSession Session { get; }
    public string Id => Session.Manifest.Id;
    public string Title => L.T(Session.Manifest.Title);
    public string DateText => Session.Manifest.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss");
    public int Count => Session.Manifest.Entries.Count;
    public string CountText => L.F("{0} saved value(s)", Count);
    public bool IsRestored => Session.Manifest.RestoredAt.HasValue;
    public string RestoredText => Session.Manifest.RestoredAt is DateTime d ? L.F("Restored {0:dd.MM.yyyy HH:mm}", d) : string.Empty;
    public string[] Entries => Session.Manifest.Entries.Select(e => e.Summary).ToArray();
}

public sealed class BackupsViewModel : PageViewModel
{
    private bool _isBusy;

    public BackupsViewModel()
    {
        RestoreCommand = new AsyncRelayCommand(p => RestoreAsync(p as BackupItem), _ => !IsBusy);
        DeleteCommand = new RelayCommand(p => Delete(p as BackupItem));
        RestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync, () => !IsBusy);
        OpenFolderCommand = new RelayCommand(() => Open(AppPaths.Backups));
        OpenLogsCommand = new RelayCommand(() => Open(AppPaths.Logs));
        OpenSystemRestoreCommand = new RelayCommand(() => Safe.Try(() => Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true }), "Open System Restore"));
    }

    public override string Key => "backups";
    public override string Title => Localization.L.T("Backups");
    public override string Icon => "";

    public ObservableCollection<BackupItem> Items { get; } = new();
    public ObservableCollection<ActionLogEntry> Log => ActionLog.Entries;

    public ICommand RestoreCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RestorePointCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenSystemRestoreCommand { get; }

    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public string BackupFolder => AppPaths.Backups;

    public override void OnNavigatedTo() => Reload();

    public void Reload()
    {
        Items.Clear();
        foreach (var session in BackupService.Instance.GetBackups())
        {
            Items.Add(new BackupItem(session));
        }
    }

    private async Task RestoreAsync(BackupItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (GamingModeService.Instance.IsActive)
        {
            DialogService.Info("Stop Gaming Mode first", "Gaming Mode has its own temporary changes. Stop it before restoring a backup.");
            return;
        }

        if (!DialogService.Confirm("Restore backup?", L.F("All values saved in \"{0}\" ({1}) will be written back:", item.Title, item.DateText), item.Entries, "RESTORE"))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var report = await BackupService.Instance.RestoreAsync(item.Session);
            if (report.Failed == 0)
            {
                NotificationService.Instance.Show(L.F("Backup restored ({0} value(s)).", report.Restored), NotificationKind.Success);
            }
            else
            {
                DialogService.Info("Restore partially failed", L.F("{0} restored, {1} failed.", report.Restored, report.Failed), report.Messages);
            }

            await ScanService.Instance.ScanAsync();
        }
        finally
        {
            IsBusy = false;
            Reload();
        }
    }

    private void Delete(BackupItem? item)
    {
        if (item is null || !DialogService.Confirm("Delete backup?", L.F("\"{0}\" ({1}) will be deleted. You can no longer restore these values.", item.Title, item.DateText), null, "DELETE", danger: true))
        {
            return;
        }

        BackupService.Instance.Delete(item.Session);
        Reload();
    }

    private async Task CreateRestorePointAsync()
    {
        IsBusy = true;
        try
        {
            var result = await RestorePointService.CreateAsync("ConstiBoost manual restore point");
            if (result.Success)
            {
                NotificationService.Instance.Show("Windows restore point created.", NotificationKind.Success);
            }
            else
            {
                DialogService.Info("Restore point not created", result.Message);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Open(string folder) =>
        Safe.Try(() => Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }), "Open folder");
}

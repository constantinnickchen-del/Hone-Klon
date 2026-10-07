using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Hardware;
using ConstiBoost.Models;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

public sealed class NetworkViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private (long received, long sent)? _lastCounters;
    private DateTime _lastSample;
    private AdapterInfo? _primary;
    private string _liveDown = "–";
    private string _liveUp = "–";
    private string _speedDown = "–";
    private string _speedUp = "–";
    private string _speedStatus = string.Empty;
    private bool _isTesting;
    private CancellationTokenSource? _cts;

    public NetworkViewModel()
    {
        _timer.Tick += (_, _) => SampleThroughput();
        RefreshCommand = new RelayCommand(LoadAdapters);
        PingCommand = new AsyncRelayCommand(PingAsync, () => !IsTesting);
        DnsCommand = new AsyncRelayCommand(DnsAsync, () => !IsTesting);
        SpeedTestCommand = new AsyncRelayCommand(SpeedTestAsync, () => !IsTesting);
    }

    public ObservableCollection<AdapterInfo> Adapters { get; } = new();
    public ObservableCollection<PingResult> Pings { get; } = new();
    public ObservableCollection<DnsResult> DnsResults { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand PingCommand { get; }
    public ICommand DnsCommand { get; }
    public ICommand SpeedTestCommand { get; }

    public string LiveDown { get => _liveDown; private set => SetProperty(ref _liveDown, value); }
    public string LiveUp { get => _liveUp; private set => SetProperty(ref _liveUp, value); }
    public string SpeedDown { get => _speedDown; private set => SetProperty(ref _speedDown, value); }
    public string SpeedUp { get => _speedUp; private set => SetProperty(ref _speedUp, value); }
    public string SpeedStatus { get => _speedStatus; private set => SetProperty(ref _speedStatus, value); }

    public bool IsTesting
    {
        get => _isTesting;
        private set
        {
            if (SetProperty(ref _isTesting, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string PrimaryName => _primary is null ? "No active connection" : $"{_primary.Name} · {_primary.ConnectionType}";
    public string PrimaryDetails => _primary is null ? string.Empty : $"{_primary.Description} · Link {_primary.LinkSpeed} · IP {_primary.IPv4}";

    public void Activate()
    {
        LoadAdapters();
        _timer.Start();
    }

    public void Deactivate()
    {
        _timer.Stop();
        _cts?.Cancel();
    }

    private void LoadAdapters()
    {
        Adapters.Clear();
        foreach (var a in NetworkService.GetAdapters())
        {
            Adapters.Add(a);
        }

        _primary = Adapters.FirstOrDefault(a => !string.IsNullOrEmpty(a.Gateway)) ?? Adapters.FirstOrDefault();
        _lastCounters = null;
        OnPropertyChanged(nameof(PrimaryName));
        OnPropertyChanged(nameof(PrimaryDetails));
    }

    private void SampleThroughput()
    {
        if (_primary is null)
        {
            return;
        }

        var counters = NetworkService.GetCounters(_primary.Id);
        var now = DateTime.UtcNow;
        if (counters.HasValue && _lastCounters.HasValue)
        {
            double seconds = (now - _lastSample).TotalSeconds;
            if (seconds > 0)
            {
                LiveDown = NetworkService.FormatBits(Math.Max(0, counters.Value.received - _lastCounters.Value.received) * 8 / seconds);
                LiveUp = NetworkService.FormatBits(Math.Max(0, counters.Value.sent - _lastCounters.Value.sent) * 8 / seconds);
            }
        }

        _lastCounters = counters;
        _lastSample = now;
    }

    private async Task PingAsync()
    {
        IsTesting = true;
        try
        {
            Pings.Clear();
            if (_primary is { Gateway.Length: > 0 })
            {
                Pings.Add(await NetworkService.PingAsync(_primary.Gateway));
            }

            Pings.Add(await NetworkService.PingAsync("1.1.1.1"));
            Pings.Add(await NetworkService.PingAsync("8.8.8.8"));
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task DnsAsync()
    {
        IsTesting = true;
        try
        {
            DnsResults.Clear();
            if (_primary != null)
            {
                foreach (var dns in _primary.DnsServers.Take(2))
                {
                    DnsResults.Add(await NetworkService.TestDnsAsync(dns, "Current DNS"));
                }
            }

            DnsResults.Add(await NetworkService.TestDnsAsync(IPAddress.Parse("1.1.1.1"), "Cloudflare"));
            DnsResults.Add(await NetworkService.TestDnsAsync(IPAddress.Parse("8.8.8.8"), "Google"));
            DnsResults.Add(await NetworkService.TestDnsAsync(IPAddress.Parse("9.9.9.9"), "Quad9"));
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task SpeedTestAsync()
    {
        if (!DialogService.Confirm("Run speed test?",
                $"The test downloads ~25 MB and uploads ~8 MB of random test data from/to {NetworkService.SpeedTestHost} (Cloudflare). No personal or system data is sent. Mobile data may be used.",
                null, "START TEST"))
        {
            return;
        }

        IsTesting = true;
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            SpeedStatus = "Testing…";
            var (down, up, error) = await NetworkService.SpeedTestAsync(_cts.Token);
            SpeedDown = down.HasValue ? NetworkService.FormatBits(down.Value) : "–";
            SpeedUp = up.HasValue ? NetworkService.FormatBits(up.Value) : "–";
            SpeedStatus = error ?? $"Measured at {DateTime.Now:HH:mm}. Results vary with Wi-Fi signal and network load.";
            if (error != null)
            {
                NotificationService.Instance.Show(error, NotificationKind.Warning);
            }
        }
        finally
        {
            IsTesting = false;
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using ConstiBoost.Core;
using ConstiBoost.Localization;
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
        ServerPingCommand = new AsyncRelayCommand(ServerPingAsync, () => !IsTesting);
        BufferbloatCommand = new AsyncRelayCommand(BufferbloatAsync, () => !IsTesting);
    }

    public ObservableCollection<AdapterInfo> Adapters { get; } = new();
    public ObservableCollection<PingResult> Pings { get; } = new();
    public ObservableCollection<DnsResult> DnsResults { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand PingCommand { get; }
    public ICommand DnsCommand { get; }
    public ICommand SpeedTestCommand { get; }
    public ICommand ServerPingCommand { get; }
    public ICommand BufferbloatCommand { get; }

    public ObservableCollection<PingResult> ServerPings { get; } = new();

    private WifiInfo? _wifi;
    private string _bloatGrade = "–";
    private string _bloatText = string.Empty;

    public WifiInfo? Wifi { get => _wifi; private set { SetProperty(ref _wifi, value); OnPropertyChanged(nameof(HasWifi)); } }
    public bool HasWifi => _wifi != null;
    public string BloatGrade { get => _bloatGrade; private set => SetProperty(ref _bloatGrade, value); }
    public string BloatText { get => _bloatText; private set => SetProperty(ref _bloatText, value); }

    private async Task ServerPingAsync()
    {
        IsTesting = true;
        try
        {
            ServerPings.Clear();
            foreach (var (region, host) in GamerNetworkService.FortniteServers)
            {
                var r = await NetworkService.PingAsync(host, 4);
                ServerPings.Add(r with { Target = $"Fortnite {region}" });
            }
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task BufferbloatAsync()
    {
        if (!DialogService.Confirm("Test latency under load?", L.F("ConstiBoost pings 1.1.1.1, then downloads up to 50 MB of random test data from {0} while pinging again. This shows whether your ping spikes when the connection is busy (bufferbloat). No personal data is sent.", NetworkService.SpeedTestHost), null, "START TEST"))
        {
            return;
        }

        IsTesting = true;
        try
        {
            BloatText = L.T("Testing… (about 10 seconds)");
            var r = await GamerNetworkService.TestBufferbloatAsync(CancellationToken.None);
            BloatGrade = r.Grade;
            BloatText = L.F("Idle ping {0} ms · under load {1} ms. ", r.IdleMs?.ToString("0") ?? "–", r.LoadedMs?.ToString("0") ?? "–") + L.T(r.Explanation);
        }
        finally
        {
            IsTesting = false;
        }
    }

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

    public string PrimaryName => _primary is null ? L.T("No active connection") : $"{_primary.Name} · {_primary.ConnectionType}";
    public string PrimaryDetails => _primary is null ? string.Empty : L.F("{0} · Link {1} · IP {2}", _primary.Description, _primary.LinkSpeed, _primary.IPv4);

    public void Activate()
    {
        LoadAdapters();
        _ = Task.Run(GamerNetworkService.ReadWifi).ContinueWith(t => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Wifi = t.Result));
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
                    DnsResults.Add(await NetworkService.TestDnsAsync(dns, L.T("Current DNS")));
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
                L.F("The test downloads ~25 MB and uploads ~8 MB of random test data from/to {0} (Cloudflare). No personal or system data is sent. Mobile data may be used.", NetworkService.SpeedTestHost),
                null, "START TEST"))
        {
            return;
        }

        IsTesting = true;
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            SpeedStatus = L.T("Testing…");
            var (down, up, error) = await NetworkService.SpeedTestAsync(_cts.Token);
            SpeedDown = down.HasValue ? NetworkService.FormatBits(down.Value) : "–";
            SpeedUp = up.HasValue ? NetworkService.FormatBits(up.Value) : "–";
            SpeedStatus = error != null ? L.T(error) : L.F("Measured at {0:HH:mm}. Results vary with Wi-Fi signal and network load.", DateTime.Now);
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ConstiBoost.Hardware;

public sealed record WifiInfo(string Ssid, int? SignalPercent, string RadioType, string Channel, string Band, string ReceiveRate, string TransmitRate, List<string> Tips);

public sealed record BufferbloatResult(double? IdleMs, double? LoadedMs, string Grade, string Explanation);

/// <summary>Gaming network diagnostics: game data center ping, latency under load (bufferbloat) and Wi-Fi quality. Read-only.</summary>
public static class GamerNetworkService
{
    /// <summary>Epic's public ping endpoints per Fortnite data center region.</summary>
    public static readonly (string region, string host)[] FortniteServers =
    {
        ("Europe", "ping-eu.ds.on.epicgames.com"), ("NA-East", "ping-nae.ds.on.epicgames.com"), ("NA-Central", "ping-nac.ds.on.epicgames.com"),
        ("NA-West", "ping-naw.ds.on.epicgames.com"), ("Brazil", "ping-br.ds.on.epicgames.com"), ("Middle East", "ping-me.ds.on.epicgames.com"),
        ("Asia", "ping-asia.ds.on.epicgames.com"), ("Oceania", "ping-oce.ds.on.epicgames.com"),
    };

    public static async Task<BufferbloatResult> TestBufferbloatAsync(CancellationToken token)
    {
        var idle = await PingSeriesAsync("1.1.1.1", 10, token);
        var loaded = new List<long>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var download = Task.Run(async () =>
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                using var response = await http.GetAsync($"https://{NetworkService.SpeedTestHost}/__down?bytes=50000000", HttpCompletionOption.ResponseHeadersRead, cts.Token);
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                var buffer = new byte[81920];
                while (await stream.ReadAsync(buffer, cts.Token) > 0)
                {
                }
            }
            catch
            {
                // cancelled or failed – handled by the ping result
            }
        }, cts.Token);

        await Task.Delay(1500, token);
        loaded.AddRange(await PingSeriesAsync("1.1.1.1", 12, token));
        cts.Cancel();
        await Task.WhenAny(download, Task.Delay(2000, token));

        double? idleAvg = idle.Count > 0 ? idle.Average() : null;
        double? loadedAvg = loaded.Count > 0 ? loaded.Average() : null;
        if (idleAvg is null || loadedAvg is null)
        {
            return new BufferbloatResult(idleAvg, loadedAvg, "–", "Ping did not respond – the test could not be completed.");
        }

        double extra = Math.Max(0, loadedAvg.Value - idleAvg.Value);
        string grade = extra < 5 ? "A+" : extra < 30 ? "A" : extra < 60 ? "B" : extra < 200 ? "C" : extra < 400 ? "D" : "F";
        string text = extra < 30
            ? $"Latency rises only {extra:0} ms under load – downloads in the background barely affect your game."
            : $"Latency rises by {extra:0} ms while the connection is busy. Downloads, streams or cloud sync during a match cause lag spikes. Fix: pause downloads while gaming, or enable 'SQM/QoS' on your router if it offers it.";
        return new BufferbloatResult(idleAvg, loadedAvg, grade, text);
    }

    private static async Task<List<long>> PingSeriesAsync(string host, int count, CancellationToken token)
    {
        var list = new List<long>();
        using var ping = new Ping();
        for (int i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(host, 1500);
                if (reply.Status == IPStatus.Success)
                {
                    list.Add(reply.RoundtripTime);
                }
            }
            catch
            {
                // lost
            }

            await Task.Delay(250, token);
        }

        return list;
    }

    /// <summary>Wi-Fi details from "netsh wlan show interfaces" (English and German Windows).</summary>
    public static WifiInfo? ReadWifi()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var psi = new ProcessStartInfo("netsh", "wlan show interfaces")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
            };
            using var p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            string? Get(string pattern)
            {
                var m = Regex.Match(output, @"^\s*(" + pattern + @")\s*:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[2].Value : null;
            }

            string? ssid = Get("SSID");
            if (ssid is null)
            {
                return null; // no Wi-Fi connection
            }

            int? signal = int.TryParse(Regex.Match(Get("Signal") ?? string.Empty, @"\d+").Value, out int s) ? s : null;
            string radio = Get("Radio type|Funktyp") ?? "–";
            string channel = Get("Channel|Kanal") ?? "–";
            string band = Get("Band") ?? (int.TryParse(channel, out int ch) ? (ch <= 14 ? "2.4 GHz" : "5 GHz") : "–");
            string rx = Get("Receive rate \\(Mbps\\)|Empfangsrate \\(MBit/s\\)|Receive rate") ?? "–";
            string tx = Get("Transmit rate \\(Mbps\\)|.bertragungsrate \\(MBit/s\\)|Transmit rate") ?? "–";

            var tips = new List<string>();
            if (signal is < 50) tips.Add($"Weak signal ({signal} %): move closer to the router or use a LAN cable – weak Wi-Fi causes packet loss and lag spikes.");
            else if (signal is < 70) tips.Add($"Medium signal ({signal} %): a LAN cable or a better position gives more stable ping.");
            if (band.Contains("2.4", StringComparison.Ordinal) || (int.TryParse(channel, out int c) && c <= 14)) tips.Add("You are on 2.4 GHz – if your router offers 5 GHz, use it for gaming (less interference).");
            if (radio.Contains("802.11n", StringComparison.OrdinalIgnoreCase) && !radio.Contains("ac", StringComparison.OrdinalIgnoreCase)) tips.Add("Wi-Fi 4 (802.11n) connection – newer standards (ac/ax) are usually more stable.");
            if (tips.Count == 0) tips.Add("Wi-Fi looks good. For competitive play a LAN cable is still the most stable option.");
            return new WifiInfo(ssid, signal, radio, channel, band, rx, tx, tips);
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Wi-Fi info unavailable: " + ex.Message);
            return null;
        }
    }
}

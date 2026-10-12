using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using ConstiBoost.Localization;

namespace ConstiBoost.Hardware;

public sealed class AdapterInfo
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ConnectionType { get; init; } = string.Empty;
    public string LinkSpeed { get; init; } = string.Empty;
    public string IPv4 { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public IReadOnlyList<IPAddress> DnsServers { get; init; } = Array.Empty<IPAddress>();
    public string DnsText => DnsServers.Count == 0 ? "Unavailable" : string.Join(", ", DnsServers.Select(d => d.ToString()));
    public string Id { get; init; } = string.Empty;
}

public sealed record PingResult(string Target, double? AverageMs, double? JitterMs, int Lost, int Sent)
{
    public string AverageText => AverageMs.HasValue ? $"{AverageMs:0} ms" : "Timeout";
    public string JitterText => JitterMs.HasValue ? $"{JitterMs:0.0} ms" : "–";
    public string LossText => $"{Lost}/{Sent} lost";
}

public sealed record DnsResult(string Server, string Label, double? MedianMs)
{
    public string Text => MedianMs.HasValue ? $"{MedianMs:0} ms" : "No response";
}

/// <summary>
/// Read-only network information. ConstiBoost never changes network settings automatically and
/// does not claim that registry tweaks lower ping.
/// </summary>
public static class NetworkService
{
    public const string SpeedTestHost = "speed.cloudflare.com";

    public static IReadOnlyList<AdapterInfo> GetAdapters()
    {
        var list = new List<AdapterInfo>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var props = nic.GetIPProperties();
                string ipv4 = props.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "–";
                string gateway = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? string.Empty;
                list.Add(new AdapterInfo
                {
                    Id = nic.Id,
                    Name = nic.Name,
                    Description = nic.Description,
                    ConnectionType = nic.NetworkInterfaceType switch
                    {
                        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
                        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT => "Ethernet (LAN)",
                        NetworkInterfaceType.Ppp => "PPP",
                        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "Mobile broadband",
                        _ => nic.NetworkInterfaceType.ToString(),
                    },
                    LinkSpeed = nic.Speed > 0 ? FormatBits(nic.Speed) : "Unavailable",
                    IPv4 = ipv4,
                    Gateway = gateway,
                    DnsServers = props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork).ToList(),
                });
            }
        }
        catch (Exception ex)
        {
            Logging.AppLog.Warn("Network adapters could not be read: " + ex.Message);
        }

        // Adapters with a gateway (= internet connection) first.
        return list.OrderBy(a => string.IsNullOrEmpty(a.Gateway)).ToList();
    }

    public static string FormatBits(double bitsPerSecond)
    {
        if (bitsPerSecond >= 1_000_000_000) return $"{bitsPerSecond / 1_000_000_000:0.#} Gbit/s";
        if (bitsPerSecond >= 1_000_000) return $"{bitsPerSecond / 1_000_000:0.#} Mbit/s";
        if (bitsPerSecond >= 1_000) return $"{bitsPerSecond / 1_000:0.#} kbit/s";
        return $"{bitsPerSecond:0} bit/s";
    }

    public static async Task<PingResult> PingAsync(string target, int count = 5)
    {
        var times = new List<long>();
        int lost = 0;
        using var ping = new Ping();
        for (int i = 0; i < count; i++)
        {
            try
            {
                var reply = await ping.SendPingAsync(target, 1500);
                if (reply.Status == IPStatus.Success)
                {
                    times.Add(reply.RoundtripTime);
                }
                else
                {
                    lost++;
                }
            }
            catch
            {
                lost++;
            }

            await Task.Delay(150);
        }

        if (times.Count == 0)
        {
            return new PingResult(target, null, null, lost, count);
        }

        double avg = times.Average();
        double jitter = times.Count > 1 ? times.Zip(times.Skip(1), (a, b) => Math.Abs(a - b)).Average() : 0;
        return new PingResult(target, avg, jitter, lost, count);
    }

    /// <summary>Bytes received/sent for an adapter – used for live throughput (no external server).</summary>
    public static (long received, long sent)? GetCounters(string adapterId)
    {
        try
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == adapterId);
            if (nic is null)
            {
                return null;
            }

            var stats = nic.GetIPStatistics();
            return (stats.BytesReceived, stats.BytesSent);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// User-started speed test against speed.cloudflare.com. Only random test bytes are transferred,
    /// no personal or system data.
    /// </summary>
    public static async Task<(double? downloadBps, double? uploadBps, string? error)> SpeedTestAsync(CancellationToken token)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        double? down = null;
        double? up = null;
        try
        {
            const int downloadBytes = 25_000_000;
            var sw = Stopwatch.StartNew();
            using (var response = await http.GetAsync($"https://{SpeedTestHost}/__down?bytes={downloadBytes}", HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0)
                {
                    total += read;
                }

                sw.Stop();
                down = total * 8 / sw.Elapsed.TotalSeconds;
            }

            const int uploadBytes = 8_000_000;
            byte[] payload = RandomNumberGenerator.GetBytes(uploadBytes);
            sw.Restart();
            using (var content = new ByteArrayContent(payload))
            using (var response = await http.PostAsync($"https://{SpeedTestHost}/__up", content, token))
            {
                response.EnsureSuccessStatusCode();
                sw.Stop();
                up = uploadBytes * 8 / sw.Elapsed.TotalSeconds;
            }

            return (down, up, null);
        }
        catch (OperationCanceledException)
        {
            return (down, up, L.T("Speed test cancelled or timed out."));
        }
        catch (Exception ex)
        {
            return (down, up, L.T("Speed test failed: ") + ex.Message);
        }
    }

    /// <summary>Measures DNS response time with a raw UDP query. Does not change any DNS setting.</summary>
    public static async Task<DnsResult> TestDnsAsync(IPAddress server, string label, string hostname = "www.epicgames.com")
    {
        var times = new List<double>();
        for (int attempt = 0; attempt < 4; attempt++)
        {
            try
            {
                using var udp = new UdpClient(server.AddressFamily);
                udp.Client.ReceiveTimeout = 2000;
                byte[] query = BuildQuery(hostname, (ushort)Random.Shared.Next(1, ushort.MaxValue));
                var sw = Stopwatch.StartNew();
                await udp.SendAsync(query, query.Length, new IPEndPoint(server, 53));
                var receiveTask = udp.ReceiveAsync();
                if (await Task.WhenAny(receiveTask, Task.Delay(2000)) == receiveTask)
                {
                    sw.Stop();
                    var result = receiveTask.Result;
                    if (result.Buffer.Length >= 12 && result.Buffer[0] == query[0] && result.Buffer[1] == query[1])
                    {
                        times.Add(sw.Elapsed.TotalMilliseconds);
                    }
                }
            }
            catch
            {
                // counted as no response
            }

            await Task.Delay(100);
        }

        if (times.Count == 0)
        {
            return new DnsResult(server.ToString(), label, null);
        }

        times.Sort();
        return new DnsResult(server.ToString(), label, times[times.Count / 2]);
    }

    private static byte[] BuildQuery(string hostname, ushort id)
    {
        var bytes = new List<byte>
        {
            (byte)(id >> 8), (byte)id, // ID
            0x01, 0x00,                // standard query, recursion desired
            0x00, 0x01,                // QDCOUNT
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        foreach (string label in hostname.Split('.'))
        {
            bytes.Add((byte)label.Length);
            bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(label));
        }

        bytes.Add(0);
        bytes.AddRange(new byte[] { 0x00, 0x01, 0x00, 0x01 }); // type A, class IN
        return bytes.ToArray();
    }
}

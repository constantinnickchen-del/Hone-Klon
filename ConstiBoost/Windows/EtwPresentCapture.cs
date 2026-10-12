using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace ConstiBoost.WinSys;

/// <summary>
/// Real-time ETW consumer for DirectX "Present" events (Microsoft-Windows-DXGI and Microsoft-Windows-D3D9) –
/// the same data source Intel PresentMon uses. Every Present call of every process is timestamped by Windows,
/// so FPS and frame times are measured, not estimated. Requires administrator rights (ETW session).
/// Covers DirectX 9/10/11/12 games; OpenGL/Vulkan games that do not present through DXGI are not counted.
/// </summary>
public sealed class EtwPresentCapture : IDisposable
{
    public const string SessionName = "ConstiBoost-FrameCapture";

    private static readonly Guid DxgiProvider = new("ca11c036-0102-4a2d-a6ad-f03cfed5d3c9");
    private static readonly Guid D3D9Provider = new("783aca0a-790e-4d7f-8451-aa850511c6b9");
    private const ushort DxgiPresentStart = 42;
    private const ushort DxgiPresentMpoStart = 55;
    private const ushort D3D9PresentStart = 1;

    private const uint WNODE_FLAG_TRACED_GUID = 0x00020000;
    private const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
    private const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
    private const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
    private const uint EVENT_TRACE_CONTROL_STOP = 1;
    private const uint EVENT_CONTROL_CODE_ENABLE_PROVIDER = 1;
    private const int PropertiesSize = 120;
    private const int LogFileSize = 448;

    private delegate void EventRecordCallback(IntPtr eventRecord);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint StartTraceW(out ulong sessionHandle, string sessionName, IntPtr properties);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ControlTraceW(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

    [DllImport("advapi32.dll")]
    private static extern uint EnableTraceEx2(ulong traceHandle, ref Guid providerId, uint controlCode, byte level, ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern ulong OpenTraceW(IntPtr logfile);

    [DllImport("advapi32.dll")]
    private static extern uint ProcessTrace([In] ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

    [DllImport("advapi32.dll")]
    private static extern uint CloseTrace(ulong traceHandle);

    private readonly ConcurrentQueue<(int pid, long qpc)> _presents = new();
    private readonly EventRecordCallback _callback;
    private IntPtr _properties;
    private IntPtr _logfile;
    private IntPtr _loggerName;
    private ulong _session;
    private ulong _trace;
    private Thread? _thread;

    public EtwPresentCapture()
    {
        _callback = OnEvent;
    }

    /// <summary>Timestamp frequency of the reported QPC values.</summary>
    public static long Frequency => System.Diagnostics.Stopwatch.Frequency;

    public void Start()
    {
        _properties = AllocProperties();
        uint status = StartTraceW(out _session, SessionName, _properties);
        if (status == 183) // ERROR_ALREADY_EXISTS – left over from a crash
        {
            ControlTraceW(0, SessionName, _properties, EVENT_TRACE_CONTROL_STOP);
            Marshal.FreeHGlobal(_properties);
            _properties = AllocProperties();
            status = StartTraceW(out _session, SessionName, _properties);
        }

        if (status != 0)
        {
            throw new System.ComponentModel.Win32Exception((int)status, "ETW session could not be started");
        }

        foreach (var provider in new[] { DxgiProvider, D3D9Provider })
        {
            Guid id = provider;
            EnableTraceEx2(_session, ref id, EVENT_CONTROL_CODE_ENABLE_PROVIDER, 5, ulong.MaxValue, 0, 0, IntPtr.Zero);
        }

        _logfile = Marshal.AllocHGlobal(LogFileSize);
        for (int i = 0; i < LogFileSize; i += 8)
        {
            Marshal.WriteInt64(_logfile, i, 0);
        }

        _loggerName = Marshal.StringToHGlobalUni(SessionName);
        Marshal.WriteIntPtr(_logfile, 8, _loggerName);
        Marshal.WriteInt32(_logfile, 28, unchecked((int)(PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD)));
        Marshal.WriteIntPtr(_logfile, 424, Marshal.GetFunctionPointerForDelegate(_callback));
        _trace = OpenTraceW(_logfile);
        if (_trace == ulong.MaxValue || _trace == 0x00000000FFFFFFFF)
        {
            Stop();
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "ETW trace could not be opened");
        }

        _thread = new Thread(() => ProcessTrace(new[] { _trace }, 1, IntPtr.Zero, IntPtr.Zero))
        {
            IsBackground = true,
            Name = "ConstiBoost ETW",
        };
        _thread.Start();
    }

    /// <summary>Takes all presents recorded since the last call.</summary>
    public List<(int pid, long qpc)> Drain()
    {
        var list = new List<(int, long)>();
        while (_presents.TryDequeue(out var item))
        {
            list.Add(item);
        }

        return list;
    }

    private void OnEvent(IntPtr record)
    {
        try
        {
            // EVENT_RECORD.EventHeader: ProcessId @12, TimeStamp @16, ProviderId @24, EventDescriptor.Id @40
            var provider = Marshal.PtrToStructure<Guid>(record + 24);
            ushort id = (ushort)Marshal.ReadInt16(record, 40);
            bool present = (provider == DxgiProvider && (id == DxgiPresentStart || id == DxgiPresentMpoStart)) ||
                           (provider == D3D9Provider && id == D3D9PresentStart);
            if (!present)
            {
                return;
            }

            int pid = Marshal.ReadInt32(record, 12);
            long qpc = Marshal.ReadInt64(record, 16);
            if (_presents.Count < 2_000_000)
            {
                _presents.Enqueue((pid, qpc));
            }
        }
        catch
        {
            // never throw into native code
        }
    }

    private static IntPtr AllocProperties()
    {
        int total = PropertiesSize + 1024;
        IntPtr p = Marshal.AllocHGlobal(total);
        for (int i = 0; i < total; i += 4)
        {
            Marshal.WriteInt32(p, i, 0);
        }

        Marshal.WriteInt32(p, 0, total);                       // Wnode.BufferSize
        Marshal.WriteInt32(p, 40, 1);                          // Wnode.ClientContext = QPC
        Marshal.WriteInt32(p, 44, unchecked((int)WNODE_FLAG_TRACED_GUID)); // Wnode.Flags
        Marshal.WriteInt32(p, 48, 64);                         // BufferSize (KB)
        Marshal.WriteInt32(p, 52, 16);                         // MinimumBuffers
        Marshal.WriteInt32(p, 56, 64);                         // MaximumBuffers
        Marshal.WriteInt32(p, 64, unchecked((int)EVENT_TRACE_REAL_TIME_MODE)); // LogFileMode
        Marshal.WriteInt32(p, 68, 1);                          // FlushTimer (s)
        Marshal.WriteInt32(p, 116, PropertiesSize);            // LoggerNameOffset
        return p;
    }

    public void Stop()
    {
        try
        {
            if (_properties != IntPtr.Zero)
            {
                ControlTraceW(_session, SessionName, _properties, EVENT_TRACE_CONTROL_STOP);
            }
        }
        catch
        {
            // ignore
        }

        if (_trace != 0 && _trace != ulong.MaxValue)
        {
            CloseTrace(_trace);
            _trace = 0;
        }

        _thread?.Join(2000);
        _thread = null;
    }

    public void Dispose()
    {
        Stop();
        if (_properties != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_properties);
            _properties = IntPtr.Zero;
        }

        if (_logfile != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_logfile);
            _logfile = IntPtr.Zero;
        }

        if (_loggerName != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_loggerName);
            _loggerName = IntPtr.Zero;
        }
    }
}

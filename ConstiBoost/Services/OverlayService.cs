using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ConstiBoost.Games;
using ConstiBoost.Localization;

namespace ConstiBoost.Services;

/// <summary>
/// Small always-on-top, click-through overlay with real values (no injection into games – anti-cheat safe).
/// Visible over games in borderless/windowed mode; exclusive fullscreen can hide it.
/// </summary>
public sealed class OverlayService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int newStyle);

    private readonly Queue<(DateTime time, float ms)> _recent = new();
    private Window? _window;
    private TextBlock? _fps;
    private TextBlock? _details;
    private TextBlock? _flash;
    private DispatcherTimer? _flashTimer;
    private bool _captureAcquired;
    private int? _pid;
    private string _processName = string.Empty;
    private double _lastFps;

    public static OverlayService Instance { get; } = new();

    public bool IsVisible => _window?.IsVisible == true;

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void Show()
    {
        if (_window == null)
        {
            Build();
        }

        Position();
        _window!.Show();
        MonitorService.Instance.Sampled += OnSample;
        MonitorService.Instance.Subscribe();
        if (SettingsService.Instance.Current.OverlayShowsFps && !_captureAcquired)
        {
            _captureAcquired = FpsCaptureService.Instance.Acquire();
            if (_captureAcquired)
            {
                FpsCaptureService.Instance.LineReceived += OnFrames;
            }
            else
            {
                _fps!.Text = L.T("FPS n/a");
            }
        }
    }

    public void Hide()
    {
        if (_window == null || !_window.IsVisible)
        {
            return;
        }

        _window.Hide();
        MonitorService.Instance.Sampled -= OnSample;
        MonitorService.Instance.Unsubscribe();
        if (_captureAcquired)
        {
            FpsCaptureService.Instance.LineReceived -= OnFrames;
            FpsCaptureService.Instance.Release();
            _captureAcquired = false;
        }
    }

    /// <summary>Shows a short message on the overlay (e.g. after a hotkey).</summary>
    public void Flash(string message)
    {
        if (!IsVisible || _flash == null)
        {
            return;
        }

        _flash.Text = message;
        _flash.Visibility = Visibility.Visible;
        _flashTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _flashTimer.Tick -= HideFlash;
        _flashTimer.Tick += HideFlash;
        _flashTimer.Start();
    }

    private void HideFlash(object? sender, EventArgs e)
    {
        _flashTimer?.Stop();
        if (_flash != null)
        {
            _flash.Visibility = Visibility.Collapsed;
        }
    }

    private void Build()
    {
        _fps = new TextBlock { Text = "FPS …", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0xE5, 0x9D)) };
        _details = new TextBlock { FontSize = 12, Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 0) };
        _flash = new TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC2, 0xFF)), TextWrapping = TextWrapping.Wrap, MaxWidth = 260, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 4, 0, 0) };
        var panel = new StackPanel();
        panel.Children.Add(_fps);
        panel.Children.Add(_details);
        panel.Children.Add(_flash);
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            FontFamily = new FontFamily("Segoe UI"),
            Content = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xA6, 0x0B, 0x0C, 0x0F)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 8, 12, 8),
                Child = panel,
            },
        };
        _window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(_window).Handle;
            SetWindowLong(handle, GWL_EXSTYLE, GetWindowLong(handle, GWL_EXSTYLE) | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        };
        _window.SizeChanged += (_, _) => Position();
    }

    private void Position()
    {
        if (_window == null)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        string corner = SettingsService.Instance.Current.OverlayCorner;
        double w = double.IsNaN(_window.ActualWidth) || _window.ActualWidth == 0 ? 180 : _window.ActualWidth;
        double h = double.IsNaN(_window.ActualHeight) || _window.ActualHeight == 0 ? 60 : _window.ActualHeight;
        _window.Left = corner.EndsWith("Right", StringComparison.Ordinal) ? area.Right - w - 16 : area.Left + 16;
        _window.Top = corner.StartsWith("Bottom", StringComparison.Ordinal) ? area.Bottom - h - 16 : area.Top + 16;
    }

    private void OnFrames(FrameLine line)
    {
        // Prefer the focused game, otherwise the process that renders the most frames.
        FrameSecond? target = null;
        if (GameFocusService.Instance.State is { } focus)
        {
            var roots = (focus.ProcessRoots.Count > 0 ? focus.ProcessRoots : new List<string> { focus.InstallPath })
                .Select(r => GameLibraryService.NormalizePath(r) + "\\").ToList();
            target = line.P.Where(p => WinSys.ProcessSafety.TryGetPath(p.Pid) is string path && roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(p => p.Frames).FirstOrDefault();
        }

        target ??= line.P.Where(p => p.Frames >= 5).OrderByDescending(p => p.Frames).FirstOrDefault();
        if (target is null)
        {
            _lastFps = 0;
            return;
        }

        if (_pid != target.Pid)
        {
            _pid = target.Pid;
            _processName = target.Name;
            _recent.Clear();
        }

        var now = DateTime.UtcNow;
        foreach (float ms in target.FrameTimesMs)
        {
            _recent.Enqueue((now, ms));
        }

        while (_recent.Count > 0 && now - _recent.Peek().time > TimeSpan.FromSeconds(10))
        {
            _recent.Dequeue();
        }

        _lastFps = target.Frames;
    }

    private void OnSample(MonitorSnapshot s)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_fps == null || _details == null)
            {
                return;
            }

            if (_captureAcquired)
            {
                var times = _recent.Select(r => (double)r.ms).OrderBy(t => t).ToList();
                double low = times.Count > 20 ? 1000.0 / times[(int)(times.Count * 0.99) - 1] : 0;
                _fps.Text = _lastFps > 0 ? $"{_lastFps:0} FPS" + (low > 0 ? $"   1% {low:0}" : string.Empty) : "FPS –";
            }

            string temp = s.Sensors.BestTemperature is double t ? $"{t:0}°C" : "–°C";
            string gpu = s.GpuPercent.HasValue ? $"{s.GpuPercent:0}%" : "–";
            _details.Text = $"CPU {s.CpuPercent:0}%  {temp}   GPU {gpu}   RAM {s.RamPercent:0}%" + (string.IsNullOrEmpty(_processName) || !_captureAcquired ? string.Empty : $"\n{_processName}");
        });
    }
}

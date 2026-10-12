using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ConstiBoost.Hardware;
using ConstiBoost.Logging;
using ConstiBoost.Models;

namespace ConstiBoost.Services;

/// <summary>"My PC can do more" share card: a PNG with real scan values, the top bottleneck and the best upgrade.</summary>
public static class ReportCardService
{
    public static void CreateAndShow()
    {
        try
        {
            var scan = ScanService.Instance.Last;
            if (scan is null)
            {
                NotificationService.Instance.Show("Run a scan first (Dashboard → RESCAN).", NotificationKind.Warning);
                return;
            }

            var hw = scan.Hardware;
            var drives = ViewModels.MainViewModel.Instance?.Games.Games.Select(g => Path.GetPathRoot(g.InstallPath) ?? string.Empty).Where(d => d.Length > 0).Distinct().ToList() ?? new();
            var insight = HardwareInsightsService.Analyze(hw, drives).FirstOrDefault(i => i.Status is CheckStatus.Bad or CheckStatus.Warning);
            var upgrade = HardwareInsightsService.UpgradeAdvice(hw, drives).FirstOrDefault();
            var bestFps = FpsHistory.Load().OrderByDescending(r => r.Time).FirstOrDefault();
            var accent = Application.Current?.TryFindResource("AccentBrush") as Brush ?? Brushes.DeepSkyBlue;

            var root = new Border
            {
                Width = 1200,
                Height = 630,
                Background = new LinearGradientBrush(Color.FromRgb(0x0B, 0x0C, 0x0F), Color.FromRgb(0x16, 0x17, 0x1C), 45),
                Padding = new Thickness(56),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.Child = grid;

            var left = new StackPanel();
            left.Children.Add(Text("⚡ ConstiBoost PRO", 26, accent, FontWeights.Bold));
            left.Children.Add(Text("MY PC REPORT", 16, Brushes.Gray, FontWeights.SemiBold, 4));
            left.Children.Add(Text($"{hw.Manufacturer} {hw.Model}", 30, Brushes.White, FontWeights.Bold, 28));
            left.Children.Add(Text(CpuDatabase.ShortName(hw.CpuName), 22, Brushes.Gainsboro, FontWeights.Normal, 14));
            left.Children.Add(Text(GpuDatabase.Classify(hw)?.Name ?? hw.PrimaryGpuName, 22, Brushes.Gainsboro, FontWeights.Normal, 4));
            left.Children.Add(Text($"{Math.Round((hw.TotalRamBytes ?? 0) / 1073741824d)} GB RAM · {hw.RamModules?.ToString() ?? "?"} module(s) · {hw.WindowsName}", 20, Brushes.Gainsboro, FontWeights.Normal, 4));
            if (insight != null)
            {
                left.Children.Add(Text("BIGGEST BOTTLENECK", 14, Brushes.Gray, FontWeights.SemiBold, 30));
                left.Children.Add(Text($"{insight.Title} ({insight.Current})", 20, Brushes.Orange, FontWeights.SemiBold, 4));
            }

            if (upgrade != null)
            {
                left.Children.Add(Text("BEST NEXT STEP", 14, Brushes.Gray, FontWeights.SemiBold, 18));
                left.Children.Add(Text(upgrade.Title, 20, Brushes.LightGreen, FontWeights.SemiBold, 4));
            }

            grid.Children.Add(left);

            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            right.Children.Add(Text("GAMING READINESS", 16, Brushes.Gray, FontWeights.SemiBold, 0, HorizontalAlignment.Center));
            right.Children.Add(Text($"{scan.Score}", 150, accent, FontWeights.Black, 0, HorizontalAlignment.Center));
            right.Children.Add(Text("/ 100", 26, Brushes.Gray, FontWeights.SemiBold, -20, HorizontalAlignment.Center));
            if (bestFps != null)
            {
                right.Children.Add(Text($"{bestFps.GameName}: {bestFps.AverageFps:0} FPS", 26, Brushes.White, FontWeights.Bold, 30, HorizontalAlignment.Center));
                right.Children.Add(Text($"measured · 1 % low {bestFps.Low1:0} · {bestFps.Setting}", 16, Brushes.Gray, FontWeights.Normal, 4, HorizontalAlignment.Center));
            }

            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            root.Measure(new Size(1200, 630));
            root.Arrange(new Rect(0, 0, 1200, 630));
            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1200, 630, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "ConstiBoost");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"ConstiBoost-Report-{DateTime.Now:yyyyMMdd-HHmm}.png");
            using (var stream = File.Create(file))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }

            try
            {
                Clipboard.SetImage(bitmap);
            }
            catch
            {
                // clipboard busy – file is still saved
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
            ActionLog.Success("PC report created", null, file);
            NotificationService.Instance.Show("📸 PC report saved to Pictures\\ConstiBoost and copied to the clipboard – ready to share.", NotificationKind.Success);
        }
        catch (Exception ex)
        {
            AppLog.Error("Report card failed", ex);
            NotificationService.Instance.Show("The report couldn't be created. Details were written to the log.", NotificationKind.Error);
        }
    }

    private static TextBlock Text(string text, double size, Brush color, FontWeight weight, double top = 0, HorizontalAlignment align = HorizontalAlignment.Left) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = color,
        FontWeight = weight,
        Margin = new Thickness(0, top, 0, 0),
        HorizontalAlignment = align,
        TextWrapping = TextWrapping.Wrap,
        FontFamily = new FontFamily("Segoe UI"),
    };
}

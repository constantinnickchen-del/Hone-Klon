using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ConstiBoost.Logging;
using ConstiBoost.Models;
using ConstiBoost.Services;
using ConstiBoost.ViewModels;
using ConstiBoost.Views;

namespace ConstiBoost;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Elevated helper mode: perform one allow-listed admin task without UI, then exit.
        if (e.Args.Length >= 3 && e.Args[0] == ElevationService.Argument)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int code = ElevatedTaskRunner.RunFromCommandLine(e.Args[1], e.Args[2]);
            Shutdown(code);
            return;
        }

        _singleInstance = new Mutex(true, @"Local\ConstiBoost.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("ConstiBoost is already running.", "ConstiBoost", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => AppLog.Error("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        base.OnStartup(e);
        AppLog.Info("ConstiBoost started");

        SettingsService.Instance.Load();
        ThemeService.Apply(SettingsService.Instance.Current.Theme, SettingsService.Instance.Current.AccentColor);
        GamingModeService.Instance.LoadPersistedState();

        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        bool minimized = e.Args.Contains("--minimized");
        if (minimized)
        {
            window.WindowState = WindowState.Minimized;
        }

        window.Show();
        viewModel.Start(minimized);
        if (GamingModeService.Instance.IsActive)
        {
            NotificationService.Instance.Show("Gaming Mode is still active from your last session. Stop it on the Dashboard to restore your settings.", NotificationKind.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            AppLog.Info("ConstiBoost exited");
            _singleInstance?.Dispose();
        }
        catch
        {
            // ignore
        }

        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("UI exception", e.Exception);
        NotificationService.Instance.Show("Something went wrong, but ConstiBoost kept running. Details were written to the log.", NotificationKind.Error);
        e.Handled = true;
    }
}

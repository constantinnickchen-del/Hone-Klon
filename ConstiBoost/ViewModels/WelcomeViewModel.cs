using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ConstiBoost.Core;
using ConstiBoost.Models;
using ConstiBoost.Optimizers;
using ConstiBoost.Services;

namespace ConstiBoost.ViewModels;

/// <summary>First start: WELCOME → SCAN MY PC → "Your PC is ready." with score and recommendations.</summary>
public sealed class WelcomeViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private int _step;
    private string _progressText = string.Empty;
    private int _score;
    private string _deviceText = string.Empty;
    private bool _isLowEnd;

    public WelcomeViewModel(MainViewModel main)
    {
        _main = main;
        ScanCommand = new AsyncRelayCommand(ScanAsync);
        FinishCommand = new RelayCommand(() => Finish(false));
        OptimizeCommand = new RelayCommand(() => Finish(true));
    }

    public ICommand ScanCommand { get; }
    public ICommand FinishCommand { get; }
    public ICommand OptimizeCommand { get; }

    /// <summary>0 = welcome, 1 = scanning, 2 = result.</summary>
    public int Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(IsWelcome));
                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(IsResult));
            }
        }
    }

    public bool IsWelcome => Step == 0;
    public bool IsScanning => Step == 1;
    public bool IsResult => Step == 2;

    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }
    public int Score { get => _score; private set => SetProperty(ref _score, value); }
    public string DeviceText { get => _deviceText; private set => SetProperty(ref _deviceText, value); }
    public bool IsLowEnd { get => _isLowEnd; private set => SetProperty(ref _isLowEnd, value); }

    public ObservableCollection<string> Recommendations { get; } = new();

    private async Task ScanAsync()
    {
        Step = 1;
        var progress = new Progress<string>(t => ProgressText = t);
        var result = await ScanService.Instance.ScanAsync(progress);
        ProgressText = "Looking for optimizations…";

        var context = new OptimizationContext { Hardware = result.Hardware, Profile = result.Profile };
        var optimizations = OptimizationCatalog.CreateAll();
        await Task.Run(() =>
        {
            foreach (var o in optimizations)
            {
                o.Refresh(context);
            }
        });

        Recommendations.Clear();
        foreach (var o in optimizations.Where(o => o.IsAvailable && o.NeedsChange && !o.IsUserChoice && o.PreselectWhenNeeded(context)))
        {
            Recommendations.Add($"{o.Name}: {o.CurrentState} → {o.RecommendedState}");
        }

        if (Recommendations.Count == 0)
        {
            Recommendations.Add("No important changes needed – your PC is already well configured.");
        }

        Score = result.Score;
        IsLowEnd = result.Profile.IsLowPowerIntegrated;
        DeviceText = result.Profile.Description;
        Step = 2;
    }

    private void Finish(bool optimize)
    {
        SettingsService.Instance.Current.FirstRunCompleted = true;
        SettingsService.Instance.Save();
        _main.ShowWelcome = false;
        if (optimize)
        {
            _main.Optimizer.StartOneClick();
        }
    }
}

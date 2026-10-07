using ConstiBoost.Core;

namespace ConstiBoost.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    public abstract string Key { get; }
    public abstract string Title { get; }
    public abstract string Icon { get; }

    private bool _isNavigationChecked;

    public bool IsNavigationChecked
    {
        get => _isNavigationChecked;
        set => SetProperty(ref _isNavigationChecked, value);
    }

    public virtual void OnNavigatedTo()
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }
}

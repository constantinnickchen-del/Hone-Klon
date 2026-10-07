using System.Collections.Generic;
using System.Windows;

namespace ConstiBoost.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, IReadOnlyList<string> details, string confirmText, bool danger, bool showCancel)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        DetailsList.ItemsSource = details;
        DetailsScroller.Visibility = details.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.Content = confirmText;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        if (danger)
        {
            ConfirmButton.Style = (Style)FindResource("DangerButton");
        }

        if (Owner == null && Application.Current?.MainWindow is { IsVisible: true } main && !ReferenceEquals(main, this))
        {
            Owner = main;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

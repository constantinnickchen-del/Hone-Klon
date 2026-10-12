using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;

namespace ConstiBoost.Localization;

/// <summary>
/// Translates fixed texts written in XAML (TextBlock / Run text, button and check box captions, headers, tool tips)
/// when an element is loaded. Data-bound texts are skipped – they are translated in the view models with L.T / L.F.
/// </summary>
public static class AutoTranslate
{
    public static void Register()
    {
        if (!L.IsGerman)
        {
            return;
        }

        EventManager.RegisterClassHandler(typeof(TextBlock), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnTextBlockLoaded));
        EventManager.RegisterClassHandler(typeof(ContentControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContentLoaded));
        EventManager.RegisterClassHandler(typeof(HeaderedContentControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContentLoaded));
        EventManager.RegisterClassHandler(typeof(HeaderedItemsControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnContentLoaded));
        EventManager.RegisterClassHandler(typeof(DataGrid), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnDataGridLoaded));
    }

    private static void OnTextBlockLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock tb || BindingOperations.IsDataBound(tb, TextBlock.TextProperty))
        {
            TranslateToolTip(sender as FrameworkElement);
            return;
        }

        if (tb.Inlines.Count == 0)
        {
            return;
        }

        foreach (var inline in tb.Inlines)
        {
            TranslateInline(inline);
        }

        TranslateToolTip(tb);
    }

    private static void OnDataGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            foreach (var column in grid.Columns)
            {
                if (column.Header is string header)
                {
                    column.Header = L.T(header);
                }
            }
        }
    }

    private static void TranslateInline(Inline inline)
    {
        switch (inline)
        {
            case Run run when !BindingOperations.IsDataBound(run, Run.TextProperty):
                string translated = L.T(run.Text);
                if (!ReferenceEquals(translated, run.Text) && translated != run.Text)
                {
                    run.Text = translated;
                }

                break;
            case Span span:
                foreach (var child in span.Inlines)
                {
                    TranslateInline(child);
                }

                break;
        }
    }

    private static void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ContentControl cc && cc.Content is string text && !BindingOperations.IsDataBound(cc, ContentControl.ContentProperty))
        {
            string translated = L.T(text);
            if (translated != text)
            {
                cc.Content = translated;
            }
        }

        if (sender is HeaderedContentControl hc && hc.Header is string header && !BindingOperations.IsDataBound(hc, HeaderedContentControl.HeaderProperty))
        {
            hc.Header = L.T(header);
        }

        if (sender is HeaderedItemsControl hi && hi.Header is string h2 && !BindingOperations.IsDataBound(hi, HeaderedItemsControl.HeaderProperty))
        {
            hi.Header = L.T(h2);
        }

        if (sender is Window w && !BindingOperations.IsDataBound(w, Window.TitleProperty))
        {
            w.Title = L.T(w.Title);
        }

        TranslateToolTip(sender as FrameworkElement);
    }

    private static void TranslateToolTip(FrameworkElement? element)
    {
        if (element?.ToolTip is string tip && !BindingOperations.IsDataBound(element, FrameworkElement.ToolTipProperty))
        {
            element.ToolTip = L.T(tip);
        }
    }
}

/// <summary>
/// Translates a bound value: <c>{Binding X, Converter={StaticResource Tr}}</c> translates the text itself,
/// with <c>ConverterParameter='Downside: {0}'</c> the parameter is the (translated) format string.
/// </summary>
public sealed class TranslateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter is string format ? L.F(format, value) : L.T(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

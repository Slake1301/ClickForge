using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge.UI;

public sealed class IntervalLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        UiText.Get("{0} мс", value);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MatchingOptionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class OptionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        MouseButton.Left => UiText.Get("Левая"), MouseButton.Right => UiText.Get("Правая"), MouseButton.Middle => UiText.Get("Средняя"),
        ClickType.Single => UiText.Get("Одиночный клик"), ClickType.Double => UiText.Get("Двойной клик"), ClickType.Triple => UiText.Get("Тройной клик"),
        ClickType.Hold => UiText.Get("Удержание"), ClickType.Release => UiText.Get("Отпускание"),
        PositionMode.CurrentCursor => UiText.Get("Следовать за курсором"), PositionMode.Fixed => UiText.Get("Фиксированные координаты"),
        RepeatMode.UntilStopped => UiText.Get("До остановки"), RepeatMode.Count => UiText.Get("По количеству повторений"), RepeatMode.Duration => UiText.Get("По времени"),
        _ => value?.ToString() ?? ""
    };
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class SectionVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int i && int.TryParse(parameter?.ToString(), out var p) && i == p ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class StateBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => new SolidColorBrush(value is AutomationState.Running ? Color.FromRgb(34, 197, 94) : value is AutomationState.Paused ? Color.FromRgb(250, 180, 40) : Color.FromRgb(107, 114, 128));
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

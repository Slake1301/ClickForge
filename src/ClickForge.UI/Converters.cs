using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClickForge.Core;

namespace ClickForge.UI;

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
        MouseButton.Left => "Левая", MouseButton.Right => "Правая", MouseButton.Middle => "Средняя",
        ClickType.Single => "Одиночный клик", ClickType.Double => "Двойной клик", ClickType.Triple => "Тройной клик",
        ClickType.Hold => "Удержание", ClickType.Release => "Отпускание",
        PositionMode.CurrentCursor => "Следовать за курсором", PositionMode.Fixed => "Фиксированные координаты",
        RepeatMode.UntilStopped => "До остановки", RepeatMode.Count => "По количеству повторений", RepeatMode.Duration => "По времени",
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

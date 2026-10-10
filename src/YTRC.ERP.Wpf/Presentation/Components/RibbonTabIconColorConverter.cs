using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

public sealed class RibbonTabIconColorConverter : IValueConverter
{
    private static readonly SolidColorBrush AppIconBrush = new(Color.FromRgb(15, 118, 110));

    static RibbonTabIconColorConverter() => AppIconBrush.Freeze();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var brush = AppIconBrush;
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

using System.Globalization;
using System.Windows.Data;

namespace YTRC.ERP.Wpf;

public sealed class RibbonCommandIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => RibbonFullColorIconFactory.ForCommandTag(value?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

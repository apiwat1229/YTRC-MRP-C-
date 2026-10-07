using System.Globalization;
using System.Windows.Data;

namespace YTRC.ERP.Wpf;

public sealed class RibbonTabIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "Home" => "\uE80F",
            "Purchasing" => "\uE7BF",
            "Receiving" => "\uE80C",
            "Production" => "\uEBB8",
            "QA" => "\uE73E",
            "Warehouse" => "\uE8B7",
            "Maintenance" => "\uE90F",
            "IT" => "\uE7F4",
            "Settings" => "\uE713",
            _ => string.Empty
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

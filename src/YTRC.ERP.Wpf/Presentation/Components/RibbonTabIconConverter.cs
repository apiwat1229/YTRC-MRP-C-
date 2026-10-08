using System.Globalization;
using System.Windows.Data;
using FluentIcons.Common;

namespace YTRC.ERP.Wpf;

public sealed class RibbonTabIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() switch
        {
            "Home" => Icon.Home,
            "Purchasing" => Icon.Cart,
            "Receiving" => Icon.Box,
            "Production" => Icon.BuildingFactory,
            "QA" => Icon.CheckmarkCircle,
            "Warehouse" => Icon.Box,
            "Maintenance" => Icon.Settings,
            "IT" => Icon.Headset,
            "Settings" => Icon.Settings,
            _ => Binding.DoNothing
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

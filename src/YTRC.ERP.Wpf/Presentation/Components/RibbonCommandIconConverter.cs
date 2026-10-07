using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

public sealed class RibbonCommandIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var glyph = value?.ToString() switch
        {
            "Dashboard" => "\uE9D2",
            "Refresh" => "\uE72C",
            "New" => "\uE710",
            "Reports" => "\uE9D2",
            "Export" => "\uEDE1",
            "Search" => "\uE721",
            "Inventory" => "\uE8B7",
            "Settings:System Config" => "\uE713",
            "Settings:Users" => "\uE77B",
            "Settings:Roles" => "\uE8D7",
            "Settings:Staff" => "\uE77B",
            "Settings:Scales" => "\uE95F",
            "Settings:Printer" => "\uE749",
            "SignOut" => "\uE8AC",
            _ => string.Empty
        };

        return glyph.Length == 0 ? null : new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

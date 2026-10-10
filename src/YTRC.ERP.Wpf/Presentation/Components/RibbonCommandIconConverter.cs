using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public sealed class RibbonCommandIconConverter : IValueConverter
{
    private static readonly SolidColorBrush AppIconBrush = CreateAppIconBrush();
    private static readonly ImageSource PurchaseOrderIcon = CreatePurchaseOrderIcon();
    private static readonly ImageSource ReportIcon = CreateReportIcon();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var tag = value?.ToString();
        if (tag is "Purchasing:Order" or "PurchasingAction:Order:CreateOrder" or "PurchasingAction:Order:ManageOrders")
            return PurchaseOrderIcon;
        if (tag == "Purchasing:Marking Preparation")
            return ReportIcon;

        var iconName = tag switch
        {
            "Dashboard" => nameof(Icon.Dashboard),
            "Refresh" => nameof(Icon.ArrowSync),
            "New" => nameof(Icon.Add),
            "Reports" => nameof(Icon.DocumentBulletList),
            "Export" => nameof(Icon.ArrowExport),
            "Search" => nameof(Icon.Search),
            "Inventory" => nameof(Icon.BoxMultiple),
            "Purchasing:Cuplump" => nameof(Icon.CalendarDay),
            "Purchasing:USS" => nameof(Icon.CalendarDay),
            "Purchasing:Supplier" => nameof(Icon.People),
            "Purchasing:Rubber Type" => nameof(Icon.Tag),
            "Purchasing:Order" => nameof(Icon.Cart),
            "PurchasingAction:Cuplump:CreateBooking" => nameof(Icon.ClipboardTaskAdd),
            "PurchasingAction:Cuplump:ManageBookings" => nameof(Icon.ClipboardTask),
            "PurchasingAction:Cuplump:DailySummary" => nameof(Icon.CalendarDay),
            "PurchasingAction:USS:CreateBooking" => nameof(Icon.ClipboardTaskAdd),
            "PurchasingAction:USS:ManageBookings" => nameof(Icon.ClipboardTask),
            "PurchasingAction:USS:DailySummary" => nameof(Icon.CalendarDay),
            "PurchasingAction:Supplier:AddSupplier" => nameof(Icon.PersonAdd),
            "PurchasingAction:Supplier:ManageSuppliers" => nameof(Icon.People),
            "PurchasingAction:Supplier:SuppliersSummary" => nameof(Icon.PeopleSearch),
            "PurchasingAction:Rubber Type:AddRubberType" => nameof(Icon.TagAdd),
            "PurchasingAction:Rubber Type:ManageRubberTypes" => nameof(Icon.TagMultiple),
            "PurchasingAction:Rubber Type:TypesSummary" => nameof(Icon.Tag),
            "PurchasingAction:Order:CreateOrder" => nameof(Icon.Cart),
            "PurchasingAction:Order:ManageOrders" => nameof(Icon.Cart),
            "PurchasingAction:Order:OrdersSummary" => nameof(Icon.DocumentBulletList),
            "Settings:System Config" => nameof(Icon.Settings),
            "Settings:Users" => nameof(Icon.People),
            "Settings:Roles" => nameof(Icon.Shield),
            "Settings:Staff" => nameof(Icon.People),
            "Settings:Scales" => nameof(Icon.Scales),
            "Settings:Printer" => nameof(Icon.Print),
            "SignOut" => nameof(Icon.SignOut),
            "Help" => nameof(Icon.Question),
            "AutoFitColumns" => nameof(Icon.Table),
            _ => null
        };

        if (iconName is null) return null;
        if (FluentIcon.Converter.ConvertFromInvariantString(iconName) is not FluentIcon icon) return null;

        var size = string.Equals(parameter?.ToString(), "Large", StringComparison.OrdinalIgnoreCase) ? 32 : 20;
        icon.IconSize = size == 32 ? IconSize.Size32 : IconSize.Size20;
        icon.IconVariant = IconVariant.Regular;
        icon.FontSize = size;
        icon.Width = size;
        icon.Height = size;
        icon.Foreground = AppIconBrush;
        icon.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
        icon.VerticalAlignment = System.Windows.VerticalAlignment.Center;
        return icon;
    }

    private static SolidColorBrush CreateAppIconBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(15, 118, 110));
        brush.Freeze();
        return brush;
    }

    private static DrawingImage CreatePurchaseOrderIcon()
    {
        var pen = new Pen(AppIconBrush, 2.2)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse("M3,6 H7 L10,13")));
        drawing.Children.Add(new GeometryDrawing(null, pen,
            Geometry.Parse("M9,12 H28 L24.5,22 H12 Z")));
        drawing.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse("M12,25 H23")));
        drawing.Children.Add(new GeometryDrawing(AppIconBrush, null,
            new EllipseGeometry(new Point(13, 26), 2.2, 2.2)));
        drawing.Children.Add(new GeometryDrawing(AppIconBrush, null,
            new EllipseGeometry(new Point(23, 26), 2.2, 2.2)));

        var image = new DrawingImage(drawing);
        if (image.CanFreeze) image.Freeze();
        return image;
    }

    private static DrawingImage CreateReportIcon()
    {
        var pen = new Pen(AppIconBrush, 2.1)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(null, pen,
            Geometry.Parse("M8,3 H20 L27,10 V29 H8 Z")));
        drawing.Children.Add(new GeometryDrawing(null, pen,
            Geometry.Parse("M20,3 V10 H27")));
        drawing.Children.Add(new GeometryDrawing(null, pen,
            Geometry.Parse("M13,16 H22 M13,21 H22 M13,26 H20")));

        var image = new DrawingImage(drawing);
        if (image.CanFreeze) image.Freeze();
        return image;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}

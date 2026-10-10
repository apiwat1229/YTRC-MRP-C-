using System.Windows;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

internal static class RibbonFullColorIconFactory
{
    private static readonly SolidColorBrush Blue = Brush(37, 99, 235);
    private static readonly SolidColorBrush PaleBlue = Brush(219, 234, 254);
    private static readonly SolidColorBrush Teal = Brush(15, 118, 110);
    private static readonly SolidColorBrush PaleTeal = Brush(204, 251, 241);
    private static readonly SolidColorBrush Green = Brush(22, 163, 74);
    private static readonly SolidColorBrush PaleGreen = Brush(220, 252, 231);
    private static readonly SolidColorBrush Orange = Brush(245, 158, 11);
    private static readonly SolidColorBrush PaleOrange = Brush(254, 243, 199);
    private static readonly SolidColorBrush Purple = Brush(124, 58, 237);
    private static readonly SolidColorBrush PalePurple = Brush(237, 233, 254);
    private static readonly SolidColorBrush Red = Brush(220, 38, 38);
    private static readonly SolidColorBrush PaleRed = Brush(254, 226, 226);
    private static readonly SolidColorBrush White = Brush(255, 255, 255);
    private static readonly SolidColorBrush Ink = Brush(51, 65, 85);
    private static readonly SolidColorBrush Muted = Brush(148, 163, 184);

    private static readonly IReadOnlyDictionary<string, DrawingImage> Icons = new Dictionary<string, DrawingImage>(StringComparer.Ordinal)
    {
        ["home"] = BuildHome(),
        ["dashboard"] = BuildDashboard(),
        ["refresh"] = BuildRefresh(),
        ["add"] = BuildAdd(),
        ["document"] = BuildDocument(),
        ["export"] = BuildExport(),
        ["search"] = BuildSearch(),
        ["box"] = BuildBox(),
        ["factory"] = BuildFactory(),
        ["quality"] = BuildQuality(),
        ["scale"] = BuildScale(),
        ["chart"] = BuildChart(),
        ["calendar"] = BuildCalendar(),
        ["people"] = BuildPeople(),
        ["person-add"] = BuildPersonAdd(),
        ["people-search"] = BuildPeopleSearch(),
        ["tag"] = BuildTag(),
        ["tag-add"] = BuildTagAdd(),
        ["tags"] = BuildTags(),
        ["cart"] = BuildCart(),
        ["clipboard"] = BuildClipboard(),
        ["clipboard-add"] = BuildClipboardAdd(),
        ["shield"] = BuildShield(),
        ["settings"] = BuildSettings(),
        ["printer"] = BuildPrinter(),
        ["sign-out"] = BuildSignOut(),
        ["table"] = BuildTable(),
        ["help"] = BuildHelp(),
        ["headset"] = BuildHeadset()
    };

    public static ImageSource? ForCommandTag(string? tag)
    {
        var kind = tag switch
        {
            "Dashboard" or "PurchasingAction:Cuplump:Dashboard" or "PurchasingAction:USS:Dashboard" => "dashboard",
            "Refresh" => "refresh",
            "New" => "add",
            "Reports" or "Purchasing:Marking Preparation" or "PurchasingAction:Order:OrdersSummary" or "Settings:Permission Catalog" => "document",
            "Export" => "export",
            "Search" or "Settings:Access Health" => "search",
            "Inventory" => "box",
            "RRC:TruckScale" or "RRC:Cuplump:TruckScale" or "RRC:USS:TruckScale" or "Settings:Scales" => "scale",
            "RRC:QueueSummary" or "RRC:Cuplump:QueueSummary" or "RRC:USS:QueueSummary" => "chart",
            "Purchasing:Cuplump" or "Purchasing:USS" or "PurchasingAction:Cuplump:DailySummary" or "PurchasingAction:USS:DailySummary" => "calendar",
            "Purchasing:Supplier" or "PurchasingAction:Supplier:ManageSuppliers" or "Settings:Users" or "Settings:Staff" or "Settings:User Access" => "people",
            "PurchasingAction:Supplier:AddSupplier" => "person-add",
            "PurchasingAction:Supplier:SuppliersSummary" => "people-search",
            "Purchasing:Rubber Type" => "tag",
            "PurchasingAction:Rubber Type:AddRubberType" => "tag-add",
            "PurchasingAction:Rubber Type:ManageRubberTypes" => "tags",
            "Purchasing:Order" or "PurchasingAction:Order:CreateOrder" or "PurchasingAction:Order:ManageOrders" => "cart",
            "PurchasingAction:Cuplump:CreateBooking" or "PurchasingAction:USS:CreateBooking" => "clipboard-add",
            "PurchasingAction:Cuplump:ManageBookings" or "PurchasingAction:USS:ManageBookings" => "clipboard",
            "Settings:Roles" or "Settings:Security Overview" or "Settings:Role Access" => "shield",
            "Settings:System Config" => "settings",
            "Settings:Printer" => "printer",
            "SignOut" => "sign-out",
            "Help" => "help",
            "AutoFitColumns" => "table",
            _ => null
        };

        return kind is not null && Icons.TryGetValue(kind, out var image) ? image : null;
    }

    public static ImageSource? ForTab(string? tab) => tab switch
    {
        "Home" => Icons["home"],
        "Purchase" or "Purchasing" => Icons["cart"],
        "RRC" or "Receiving" => Icons["box"],
        "Production" => Icons["factory"],
        "QA" => Icons["quality"],
        "Warehouse" => Icons["box"],
        "Maintenance" or "Settings" => Icons["settings"],
        "IT" => Icons["headset"],
        _ => null
    };

    private static SolidColorBrush Brush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Pen Stroke(Brush brush, double thickness = 1.8) => new(brush, thickness)
    {
        StartLineCap = PenLineCap.Round,
        EndLineCap = PenLineCap.Round,
        LineJoin = PenLineJoin.Round
    };

    private static void Path(DrawingGroup group, string geometry, Brush? fill = null, Brush? stroke = null, double thickness = 1.8)
        => group.Children.Add(new GeometryDrawing(fill, stroke is null ? null : Stroke(stroke, thickness), Geometry.Parse(geometry)));

    private static void Rect(DrawingGroup group, double x, double y, double width, double height,
        Brush? fill = null, Brush? stroke = null, double radius = 2, double thickness = 1.8)
    {
        var geometry = new RectangleGeometry(new Rect(x, y, width, height), radius, radius);
        group.Children.Add(new GeometryDrawing(fill, stroke is null ? null : Stroke(stroke, thickness), geometry));
    }

    private static void Ellipse(DrawingGroup group, double x, double y, double width, double height,
        Brush? fill = null, Brush? stroke = null, double thickness = 1.8)
    {
        var geometry = new EllipseGeometry(new Rect(x, y, width, height));
        group.Children.Add(new GeometryDrawing(fill, stroke is null ? null : Stroke(stroke, thickness), geometry));
    }

    private static DrawingImage Image(DrawingGroup drawing)
    {
        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return image;
    }

    private static DrawingImage BuildHome()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,15 L16,4 L28,15 L24,15 L24,27 L8,27 L8,15 Z", PaleBlue, Blue, 1.5);
        Rect(drawing, 14, 19, 5, 8, Teal, radius: 1);
        Rect(drawing, 9.5, 16, 3, 3, Orange, radius: .7);
        return Image(drawing);
    }

    private static DrawingImage BuildDashboard()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 4, 12, 10, PaleBlue, Blue, 2, 1.4);
        Rect(drawing, 17, 4, 12, 7, PaleGreen, Green, 2, 1.4);
        Rect(drawing, 3, 16, 12, 12, PalePurple, Purple, 2, 1.4);
        Rect(drawing, 17, 13, 12, 15, PaleOrange, Orange, 2, 1.4);
        return Image(drawing);
    }

    private static DrawingImage BuildRefresh()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M25,13 A9,9 0 0 0 9,8 L6,11 M6,11 L7,5 M6,11 L12,10", null, Teal, 2.7);
        Path(drawing, "M7,19 A9,9 0 0 0 23,24 L26,21 M26,21 L25,27 M26,21 L20,22", null, Blue, 2.7);
        Ellipse(drawing, 13, 13, 6, 6, PaleGreen, Green, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildAdd()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, PaleGreen, Green, 2);
        Path(drawing, "M16,9 V23 M9,16 H23", null, Teal, 3);
        return Image(drawing);
    }

    private static DrawingImage BuildDocument()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M7,3 H20 L27,10 V29 H7 Z", PalePurple, Purple, 1.7);
        Path(drawing, "M20,3 V10 H27", PaleOrange, Orange, 1.7);
        Path(drawing, "M12,15 H22 M12,20 H23 M12,25 H19", null, Blue, 1.8);
        Ellipse(drawing, 9, 14.5, 1.7, 1.7, Green);
        Ellipse(drawing, 9, 19.5, 1.7, 1.7, Orange);
        Ellipse(drawing, 9, 24.5, 1.7, 1.7, Teal);
        return Image(drawing);
    }

    private static DrawingImage BuildExport()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M5,18 V27 H27 V18", null, Blue, 2);
        Path(drawing, "M16,22 V5 M10,11 L16,5 L22,11", null, Orange, 2.7);
        Path(drawing, "M9,21 H12", null, Green, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildSearch()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 4, 4, 17, 17, PaleBlue, Blue, 2.3);
        Path(drawing, "M18,18 L28,28", null, Orange, 3.2);
        Ellipse(drawing, 8, 8, 9, 9, PaleTeal, Teal, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildBox()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,9 L16,3 L28,9 V24 L16,30 L4,24 Z", PaleOrange, Orange, 1.7);
        Path(drawing, "M4,9 L16,15 L28,9 M16,15 V30", null, Blue, 1.8);
        Path(drawing, "M10,6 L22,12", null, Teal, 1.5);
        return Image(drawing);
    }

    private static DrawingImage BuildFactory()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,27 V14 L12,18 V13 L20,18 V9 L28,14 V27 Z", PaleBlue, Blue, 1.7);
        Rect(drawing, 7, 20, 3, 4, Orange, radius: .5);
        Rect(drawing, 14, 20, 3, 4, Green, radius: .5);
        Rect(drawing, 22, 20, 3, 4, Purple, radius: .5);
        Path(drawing, "M23,9 V4 H27 V14", null, Orange, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildQuality()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, PaleGreen, Green, 2);
        Ellipse(drawing, 8, 8, 16, 16, White, Teal, 1.2);
        Path(drawing, "M11,16 L14.5,20 L21.5,12", null, Blue, 2.5);
        Ellipse(drawing, 23.5, 6, 4, 4, Orange);
        return Image(drawing);
    }

    private static DrawingImage BuildScale()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 6, 26, 21, PaleBlue, Blue, 4, 1.8);
        Rect(drawing, 7, 9, 18, 8, PaleTeal, Teal, 3, 1.4);
        Path(drawing, "M16,13 L21,10", null, Orange, 2.3);
        Ellipse(drawing, 14.5, 11.5, 3, 3, Green);
        Path(drawing, "M8,22 H24", null, Blue, 1.5);
        return Image(drawing);
    }

    private static DrawingImage BuildChart()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,27 H29", null, Ink, 1.6);
        Rect(drawing, 6, 17, 5, 10, PaleBlue, Blue, 1, 1.4);
        Rect(drawing, 14, 10, 5, 17, PaleGreen, Green, 1, 1.4);
        Rect(drawing, 22, 4, 5, 23, PaleOrange, Orange, 1, 1.4);
        Path(drawing, "M7,14 L15,7 L21,9 L28,3", null, Purple, 1.7);
        return Image(drawing);
    }

    private static DrawingImage BuildCalendar()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 4, 5, 24, 23, White, Purple, 3, 1.7);
        Rect(drawing, 4, 5, 24, 7, PalePurple, Purple, 3, 1.4);
        Path(drawing, "M10,3 V9 M22,3 V9", null, Orange, 2.5);
        Rect(drawing, 9, 15, 4, 4, Blue, radius: 1);
        Rect(drawing, 17, 15, 4, 4, Green, radius: 1);
        Rect(drawing, 9, 21, 4, 4, Orange, radius: 1);
        Rect(drawing, 17, 21, 4, 4, PaleTeal, Teal, 1, 1);
        return Image(drawing);
    }

    private static DrawingImage BuildPeople()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 5, 5, 9, 9, PaleOrange, Orange, 1.3);
        Ellipse(drawing, 18, 5, 9, 9, PaleBlue, Blue, 1.3);
        Path(drawing, "M3,27 C3,20 6,17 10,17 C14,17 17,20 17,27 Z", PaleTeal, Teal, 1.4);
        Path(drawing, "M15,27 C15,20 18,17 22,17 C26,17 29,20 29,27 Z", PalePurple, Purple, 1.4);
        return Image(drawing);
    }

    private static DrawingImage BuildPersonAdd()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 5, 4, 12, 12, PaleBlue, Blue, 1.5);
        Path(drawing, "M2,29 C2,21 6,18 11,18 C16,18 20,21 20,29 Z", PaleTeal, Teal, 1.5);
        Ellipse(drawing, 19, 15, 12, 12, PaleGreen, Green, 1.4);
        Path(drawing, "M25,18 V24 M22,21 H28", null, Orange, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildPeopleSearch()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 4, 5, 9, 9, PaleOrange, Orange, 1.3);
        Ellipse(drawing, 15, 5, 9, 9, PaleBlue, Blue, 1.3);
        Path(drawing, "M2,25 C2,19 5,16 9,16 C13,16 16,19 16,25 Z", PaleTeal, Teal, 1.2);
        Path(drawing, "M12,25 C12,19 15,16 19,16 C20,16 21,16 22,17", PalePurple, Purple, 1.2);
        Ellipse(drawing, 19, 18, 8, 8, PaleBlue, Blue, 1.8);
        Path(drawing, "M26,25 L30,29", null, Orange, 2.4);
        return Image(drawing);
    }

    private static DrawingImage BuildTag()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,6 H18 L28,16 L17,27 L4,14 Z", PalePurple, Purple, 1.8);
        Ellipse(drawing, 9, 10, 4, 4, PaleOrange, Orange, 1.3);
        Path(drawing, "M13,14 L21,22", null, Blue, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildTagAdd()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,7 H16 L24,15 L14,25 L3,14 Z", PalePurple, Purple, 1.7);
        Ellipse(drawing, 7, 10, 3.5, 3.5, Orange);
        Ellipse(drawing, 18, 18, 12, 12, PaleGreen, Green, 1.3);
        Path(drawing, "M24,21 V27 M21,24 H27", null, Teal, 1.8);
        return Image(drawing);
    }

    private static DrawingImage BuildTags()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,8 H15 L21,14 L12,23 L3,14 Z", PaleBlue, Blue, 1.5);
        Ellipse(drawing, 6, 10, 3, 3, Orange);
        Path(drawing, "M11,5 H23 L29,11 L20,20", PalePurple, Purple, 1.5);
        Ellipse(drawing, 14, 7, 3, 3, Green);
        return Image(drawing);
    }

    private static DrawingImage BuildCart()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,5 H7 L10,12 M9,12 H28 L24.5,22 H12 Z", PaleBlue, Blue, 2);
        Path(drawing, "M12,26 H23", null, Teal, 2);
        Ellipse(drawing, 11, 24, 4.5, 4.5, Orange, Red, 1);
        Ellipse(drawing, 21, 24, 4.5, 4.5, Green, Teal, 1);
        return Image(drawing);
    }

    private static DrawingImage BuildClipboard()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 6, 5, 20, 24, PaleBlue, Blue, 3, 1.7);
        Rect(drawing, 11, 3, 10, 5, PaleOrange, Orange, 2, 1.5);
        Path(drawing, "M11,14 H21 M11,19 H21 M11,24 H18", null, Teal, 1.8);
        Ellipse(drawing, 8, 13, 2, 2, Green);
        Ellipse(drawing, 8, 18, 2, 2, Orange);
        Ellipse(drawing, 8, 23, 2, 2, Purple);
        return Image(drawing);
    }

    private static DrawingImage BuildClipboardAdd()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 4, 5, 19, 24, PaleBlue, Blue, 3, 1.6);
        Rect(drawing, 9, 3, 9, 5, PaleOrange, Orange, 2, 1.4);
        Path(drawing, "M8,14 H17 M8,19 H17 M8,24 H14", null, Teal, 1.6);
        Ellipse(drawing, 19, 18, 12, 12, PaleGreen, Green, 1.4);
        Path(drawing, "M25,21 V27 M22,24 H28", null, Orange, 1.8);
        return Image(drawing);
    }

    private static DrawingImage BuildShield()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M16,3 L27,7 V15 C27,22 22,27 16,30 C10,27 5,22 5,15 V7 Z", PalePurple, Purple, 1.8);
        Path(drawing, "M10,16 L14,20 L22,11", null, Green, 2.5);
        Ellipse(drawing, 22, 4, 5, 5, PaleOrange, Orange, 1);
        return Image(drawing);
    }

    private static DrawingImage BuildSettings()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 7, 7, 18, 18, PaleBlue, Blue, 2.5);
        Ellipse(drawing, 12, 12, 8, 8, PaleOrange, Orange, 2);
        Rect(drawing, 14, 2, 4, 7, Teal, radius: 1);
        Rect(drawing, 14, 23, 4, 7, Purple, radius: 1);
        Rect(drawing, 2, 14, 7, 4, Green, radius: 1);
        Rect(drawing, 23, 14, 7, 4, Orange, radius: 1);
        return Image(drawing);
    }

    private static DrawingImage BuildPrinter()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 8, 3, 16, 10, White, Purple, 1, 1.5);
        Rect(drawing, 4, 11, 24, 13, PaleBlue, Blue, 3, 1.7);
        Rect(drawing, 8, 20, 16, 9, PaleGreen, Green, 1, 1.5);
        Path(drawing, "M11,23 H21 M11,26 H19", null, Teal, 1.3);
        Ellipse(drawing, 23, 14, 2.5, 2.5, Orange);
        return Image(drawing);
    }

    private static DrawingImage BuildSignOut()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M6,4 H19 V28 H6 Z", PalePurple, Purple, 1.8);
        Ellipse(drawing, 15, 15, 2, 2, Orange);
        Path(drawing, "M14,16 H29 M23,10 L29,16 L23,22", null, Blue, 2.6);
        return Image(drawing);
    }

    private static DrawingImage BuildTable()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 5, 26, 22, White, Blue, 2, 1.7);
        Rect(drawing, 4, 6, 24, 6, PaleBlue, Blue, 1, 1.2);
        Path(drawing, "M4,17 H28 M12,6 V27 M20,6 V27", null, Teal, 1.5);
        Rect(drawing, 5, 18, 6, 8, PaleGreen, radius: 1);
        Rect(drawing, 13, 18, 6, 8, PaleOrange, radius: 1);
        return Image(drawing);
    }

    private static DrawingImage BuildHelp()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, PaleBlue, Blue, 2);
        Path(drawing, "M11,12 C11,8 14,7 17,7 C21,7 23,9 23,12 C23,15 21,16 18,18 C16,19 16,20 16,22", null, Purple, 2.4);
        Ellipse(drawing, 14.5, 24, 3, 3, Orange);
        return Image(drawing);
    }

    private static DrawingImage BuildHeadset()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M5,18 V15 C5,8 10,4 16,4 C22,4 27,8 27,15 V18", null, Blue, 2.4);
        Rect(drawing, 3, 15, 6, 10, PaleTeal, Teal, 2, 1.5);
        Rect(drawing, 23, 15, 6, 10, PaleOrange, Orange, 2, 1.5);
        Path(drawing, "M25,24 C24,28 21,29 16,29 H14", null, Purple, 1.8);
        Ellipse(drawing, 11, 27, 4, 4, Green);
        return Image(drawing);
    }
}

using System.Windows;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

internal static class RibbonFullColorIconFactory
{
    // Rich Gradient Brushes for Full-Color Fluent Iconography
    private static readonly LinearGradientBrush BlueGrad = Grad("#3B82F6", "#1D4ED8");
    private static readonly LinearGradientBrush RoyalGrad = Grad("#2563EB", "#1E40AF");
    private static readonly LinearGradientBrush CyanGrad = Grad("#06B6D4", "#0891B2");
    private static readonly LinearGradientBrush TealGrad = Grad("#14B8A6", "#0F766E");
    private static readonly LinearGradientBrush DarkTealGrad = Grad("#0F766E", "#115E59");
    private static readonly LinearGradientBrush EmeraldGrad = Grad("#34D399", "#059669");
    private static readonly LinearGradientBrush GreenGrad = Grad("#22C55E", "#15803D");
    private static readonly LinearGradientBrush AmberGrad = Grad("#FBBF24", "#D97706");
    private static readonly LinearGradientBrush OrangeGrad = Grad("#FB923C", "#EA580C");
    private static readonly LinearGradientBrush RedGrad = Grad("#F87171", "#DC2626");
    private static readonly LinearGradientBrush CrimsonGrad = Grad("#EF4444", "#B91C1C");
    private static readonly LinearGradientBrush PurpleGrad = Grad("#A855F7", "#7E22CE");
    private static readonly LinearGradientBrush VioletGrad = Grad("#818CF8", "#4F46E5");
    private static readonly LinearGradientBrush SlateGrad = Grad("#64748B", "#334155");
    private static readonly LinearGradientBrush DarkSlateGrad = Grad("#334155", "#0F172A");
    private static readonly LinearGradientBrush SilverGrad = Grad("#F8FAFC", "#CBD5E1", 0, 0, 0, 1);
    private static readonly LinearGradientBrush GlassGrad = Grad("#80FFFFFF", "#10FFFFFF", 0, 0, 1, 1);
    private static readonly LinearGradientBrush GoldGrad = Grad("#FDE047", "#CA8A04");

    // Pure Solid Colors for Accents
    private static readonly SolidColorBrush White = Solid("#FFFFFF");
    private static readonly SolidColorBrush Charcoal = Solid("#1E293B");
    private static readonly SolidColorBrush GlowGreen = Solid("#10B981");
    private static readonly SolidColorBrush GlowTeal = Solid("#0D9488");
    private static readonly SolidColorBrush GlowAmber = Solid("#F59E0B");
    private static readonly SolidColorBrush GlowCyan = Solid("#06B6D4");
    private static readonly SolidColorBrush Shadow = Solid("#25000000");

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
        ["staff"] = BuildStaff(),
        ["user-access"] = BuildUserAccess(),
        ["person-add"] = BuildPersonAdd(),
        ["people-search"] = BuildPeopleSearch(),
        ["tag"] = BuildTag(),
        ["tag-add"] = BuildTagAdd(),
        ["tags"] = BuildTags(),
        ["cart"] = BuildCart(),
        ["clipboard"] = BuildClipboard(),
        ["clipboard-add"] = BuildClipboardAdd(),
        ["shield"] = BuildShield(),
        ["security-overview"] = BuildSecurityOverview(),
        ["catalog"] = BuildPermissionCatalog(),
        ["health"] = BuildAccessHealth(),
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
            "Reports" or "Purchasing:Marking Preparation" or "PurchasingAction:Order:OrdersSummary" => "document",
            "Settings:Permission Catalog" => "catalog",
            "Export" => "export",
            "Search" => "search",
            "Settings:Access Health" => "health",
            "Inventory" => "box",
            "RRC:TruckScale" or "RRC:Cuplump:TruckScale" or "RRC:USS:TruckScale" or "Settings:Scales" => "scale",
            "RRC:QueueSummary" or "RRC:Cuplump:QueueSummary" or "RRC:USS:QueueSummary" => "chart",
            "RRC:Cuplump:Receiving" => "box",
            "Purchasing:Cuplump" or "Purchasing:USS" or "PurchasingAction:Cuplump:DailySummary" or "PurchasingAction:USS:DailySummary" => "calendar",
            "Purchasing:Supplier" or "PurchasingAction:Supplier:ManageSuppliers" or "Settings:Users" => "people",
            "Settings:Staff" => "staff",
            "Settings:User Access" => "user-access",
            "PurchasingAction:Supplier:AddSupplier" => "person-add",
            "PurchasingAction:Supplier:SuppliersSummary" => "people-search",
            "Purchasing:Rubber Type" => "tag",
            "PurchasingAction:Rubber Type:AddRubberType" => "tag-add",
            "PurchasingAction:Rubber Type:ManageRubberTypes" => "tags",
            "Purchasing:Order" or "PurchasingAction:Order:CreateOrder" or "PurchasingAction:Order:ManageOrders" => "cart",
            "PurchasingAction:Cuplump:CreateBooking" or "PurchasingAction:USS:CreateBooking" => "clipboard-add",
            "PurchasingAction:Cuplump:ManageBookings" or "PurchasingAction:USS:ManageBookings" => "clipboard",
            "Settings:Role Access" or "Settings:Roles" => "shield",
            "Settings:Security Overview" => "security-overview",
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
        "Maintenance" => Icons["settings"],
        "Settings" => Icons["settings"],
        "TruckScale" => Icons["scale"],
        "IT" => Icons["headset"],
        _ => null
    };

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        brush.Freeze();
        return brush;
    }

    private static LinearGradientBrush Grad(string startHex, string endHex, double x1 = 0, double y1 = 0, double x2 = 1, double y2 = 1)
    {
        var start = (Color)ColorConverter.ConvertFromString(startHex)!;
        var end = (Color)ColorConverter.ConvertFromString(endHex)!;
        var brush = new LinearGradientBrush(start, end, new Point(x1, y1), new Point(x2, y2));
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

    // --- High-Fidelity Full-Color Fluent Icon Builders ---

    private static DrawingImage BuildHome()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,15 L16,3 L28,15 L24,15 L24,28 L8,28 L8,15 Z", RoyalGrad, CyanGrad, 1.2);
        Path(drawing, "M16,4 L26,14 H22 V26 H10 V14 H6 Z", GlassGrad);
        Rect(drawing, 13, 17, 6, 11, DarkTealGrad, GoldGrad, 1.2, 0.8);
        Ellipse(drawing, 17.5, 23, 1.5, 1.5, GoldGrad);
        Rect(drawing, 9, 15, 3.5, 3.5, GoldGrad, White, 0.6, 0.6);
        return Image(drawing);
    }

    private static DrawingImage BuildDashboard()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 3, 12, 11, BlueGrad, White, 2.5, 0.8);
        Path(drawing, "M5,10 L8,7 L11,9 L13,6", null, White, 1.4);
        Rect(drawing, 17, 3, 12, 8, EmeraldGrad, White, 2.5, 0.8);
        Ellipse(drawing, 23, 7, 3, 3, White);
        Rect(drawing, 3, 16, 12, 13, PurpleGrad, White, 2.5, 0.8);
        Rect(drawing, 5, 19, 8, 2, White, radius: 0.5);
        Rect(drawing, 5, 23, 6, 2, White, radius: 0.5);
        Rect(drawing, 17, 13, 12, 16, AmberGrad, White, 2.5, 0.8);
        Path(drawing, "M20,24 V20 M23,24 V17 M26,24 V19", null, White, 1.6);
        return Image(drawing);
    }

    private static DrawingImage BuildRefresh()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M25,13 A9.5,9.5 0 0 0 8,8 L5,11 M5,11 L6,5 M5,11 L11,10", null, TealGrad, 3.2);
        Path(drawing, "M7,19 A9.5,9.5 0 0 0 24,24 L27,21 M27,21 L26,27 M27,21 L21,22", null, BlueGrad, 3.2);
        Ellipse(drawing, 12.5, 12.5, 7, 7, EmeraldGrad, White, 1);
        return Image(drawing);
    }

    private static DrawingImage BuildAdd()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, EmeraldGrad, White, 1.5);
        Ellipse(drawing, 6, 5, 16, 10, GlassGrad);
        Path(drawing, "M16,8 V24 M8,16 H24", null, White, 3.5);
        return Image(drawing);
    }

    private static DrawingImage BuildDocument()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M6,2 H20 L27,9 V30 H6 Z", SilverGrad, SlateGrad, 1.2);
        Path(drawing, "M20,2 V9 H27 Z", AmberGrad, White, 0.8);
        Rect(drawing, 9, 6, 9, 3, RoyalGrad, radius: 1);
        Path(drawing, "M10,14 H23 M10,18 H23 M10,22 H18 M10,26 H21", null, BlueGrad, 1.5);
        Ellipse(drawing, 21, 24, 4, 4, EmeraldGrad, GoldGrad, 0.8);
        return Image(drawing);
    }

    private static DrawingImage BuildExport()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M5,17 V27 C5,28.5 6.5,29.5 8,29.5 H24 C25.5,29.5 27,28.5 27,27 V17", null, RoyalGrad, 2.5);
        Rect(drawing, 10, 22, 12, 5, SilverGrad, radius: 1);
        Path(drawing, "M16,21 V4 M9,11 L16,4 L23,11", null, AmberGrad, 3.5);
        Path(drawing, "M16,21 V4 M9,11 L16,4 L23,11", null, White, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildSearch()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 20, 20, GlassGrad, BlueGrad, 3);
        Ellipse(drawing, 6, 5, 12, 8, GlassGrad);
        Path(drawing, "M19,19 L29,29", null, AmberGrad, 4.5);
        Path(drawing, "M19,19 L29,29", null, White, 1.4);
        return Image(drawing);
    }

    private static DrawingImage BuildBox()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,9 L16,2 L28,9 V24 L16,31 L4,24 Z", AmberGrad, OrangeGrad, 1.2);
        Path(drawing, "M4,9 L16,16 L28,9 M16,16 V31", null, RoyalGrad, 2);
        Path(drawing, "M11,5.5 L21,12.5", null, CyanGrad, 2.2);
        Rect(drawing, 7, 18, 5, 4, White, radius: 0.5);
        return Image(drawing);
    }

    private static DrawingImage BuildFactory()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,28 V13 L11,17 V12 L19,17 V8 L27,13 V28 Z", RoyalGrad, CyanGrad, 1.2);
        Rect(drawing, 6, 21, 3.5, 4.5, GoldGrad, radius: 0.8);
        Rect(drawing, 14, 21, 3.5, 4.5, EmeraldGrad, radius: 0.8);
        Rect(drawing, 22, 21, 3.5, 4.5, AmberGrad, radius: 0.8);
        Path(drawing, "M22,8 V3 H26 V12", null, OrangeGrad, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildQuality()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, RoyalGrad, GoldGrad, 1.5);
        Ellipse(drawing, 7, 7, 18, 18, White, TealGrad, 1.2);
        Path(drawing, "M10,16 L14,20 L22,11", null, EmeraldGrad, 3.2);
        Path(drawing, "M10,16 L14,20 L22,11", null, White, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildChart()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,28 H29", null, DarkSlateGrad, 2);
        Rect(drawing, 5, 17, 5.5, 11, BlueGrad, White, 1.2, 0.6);
        Rect(drawing, 13, 9, 5.5, 19, EmeraldGrad, White, 1.2, 0.6);
        Rect(drawing, 21, 3, 5.5, 25, AmberGrad, White, 1.2, 0.6);
        Path(drawing, "M7,14 L15,6 L21,9 L27,2", null, CrimsonGrad, 2.2);
        Ellipse(drawing, 25.5, 1, 3, 3, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildCalendar()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 4, 4, 24, 25, White, SlateGrad, 3, 1.2);
        Rect(drawing, 4, 4, 24, 8, CrimsonGrad, null, 3);
        Path(drawing, "M9,2 V7 M23,2 V7", null, GoldGrad, 2.8);
        Rect(drawing, 8, 15, 4.5, 4.5, RoyalGrad, radius: 1);
        Rect(drawing, 15, 15, 4.5, 4.5, EmeraldGrad, radius: 1);
        Rect(drawing, 15, 22, 4.5, 4.5, AmberGrad, radius: 1);
        Rect(drawing, 8, 22, 4.5, 4.5, TealGrad, radius: 1);
        return Image(drawing);
    }

    private static DrawingImage BuildPersonAdd()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 7, 4, 11, 11, RoyalGrad, White, 1);
        Path(drawing, "M3,28 C3,20 6.5,17 13,17 C16.5,17 19.5,18.5 21,21", BlueGrad, White, 1.2);
        Ellipse(drawing, 18, 17, 12, 12, EmeraldGrad, White, 1.5);
        Path(drawing, "M24,20 V26 M21,23 H27", null, White, 2.2);
        return Image(drawing);
    }

    private static DrawingImage BuildPeopleSearch()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 5, 4, 10, 10, AmberGrad, White, 1);
        Path(drawing, "M2,27 C2,20 5.5,17 11,17 C14,17 17,18.5 18,21", RoyalGrad, White, 1.2);
        Ellipse(drawing, 16, 15, 10, 10, GlassGrad, CyanGrad, 2.2);
        Path(drawing, "M24,23 L29,28", null, OrangeGrad, 3.2);
        return Image(drawing);
    }

    private static DrawingImage BuildTag()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M4,6 H18 L28,16 L17,27 L4,14 Z", PurpleGrad, VioletGrad, 1.2);
        Ellipse(drawing, 8.5, 9.5, 4.5, 4.5, White, GoldGrad, 1);
        Ellipse(drawing, 9.5, 10.5, 2.5, 2.5, Charcoal);
        return Image(drawing);
    }

    private static DrawingImage BuildTagAdd()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,6 H16 L24,14 L14,24 L3,13 Z", PurpleGrad, VioletGrad, 1.2);
        Ellipse(drawing, 7.5, 9.5, 3.5, 3.5, GoldGrad);
        Ellipse(drawing, 18, 17, 12, 12, EmeraldGrad, White, 1.4);
        Path(drawing, "M24,20 V26 M21,23 H27", null, White, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildTags()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M3,8 H15 L21,14 L12,23 L3,14 Z", BlueGrad, RoyalGrad, 1.2);
        Ellipse(drawing, 6, 10, 3, 3, White);
        Path(drawing, "M10,4 H22 L29,11 L19,21", PurpleGrad, VioletGrad, 1.2);
        Ellipse(drawing, 13.5, 6.5, 3, 3, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildCart()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M2,4 H6 L9,11 M8.5,11 H28 L24.5,22 H11 Z", RoyalGrad, CyanGrad, 1.8);
        Rect(drawing, 12, 13, 6, 6, AmberGrad, radius: 1);
        Rect(drawing, 19, 13, 5, 6, EmeraldGrad, radius: 1);
        Ellipse(drawing, 10, 24, 4.5, 4.5, DarkSlateGrad, AmberGrad, 1.2);
        Ellipse(drawing, 21, 24, 4.5, 4.5, DarkSlateGrad, TealGrad, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildClipboard()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 5, 4, 22, 25, White, SlateGrad, 3, 1.2);
        Rect(drawing, 10, 2, 12, 5.5, DarkSlateGrad, SilverGrad, 2, 0.8);
        Ellipse(drawing, 16, 4.5, 2, 2, GoldGrad);
        Path(drawing, "M12,13 H22 M12,18 H22 M12,23 H18", null, SlateGrad, 1.5);
        Ellipse(drawing, 8.5, 12.5, 2, 2, EmeraldGrad);
        Ellipse(drawing, 8.5, 17.5, 2, 2, AmberGrad);
        Ellipse(drawing, 8.5, 22.5, 2, 2, BlueGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildClipboardAdd()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 4, 20, 25, White, SlateGrad, 3, 1.2);
        Rect(drawing, 8, 2, 10, 5, DarkSlateGrad, SilverGrad, 1.5, 0.8);
        Path(drawing, "M7,13 H17 M7,18 H17 M7,23 H13", null, SlateGrad, 1.4);
        Ellipse(drawing, 18, 17, 12, 12, EmeraldGrad, White, 1.4);
        Path(drawing, "M24,20 V26 M21,23 H27", null, White, 2);
        return Image(drawing);
    }

    private static DrawingImage BuildShield()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M16,3 C22,3 27.5,5 28.5,8 V16 C28.5,24 23,29.5 16,32 C9,29.5 3.5,24 3.5,16 V8 C4.5,5 10,3 16,3 Z", Shadow);
        Path(drawing, "M16,2 C22,2 27,4 28,7 V15 C28,23 22.5,28.5 16,31 C9.5,28.5 4,23 4,15 V7 C5,4 10,2 16,2 Z", RoyalGrad, CyanGrad, 1.2);
        Path(drawing, "M16,4 C20.5,4 25,5.5 25.5,8 V14.5 C25.5,21.5 21,26 16,28.5 C11,26 6.5,21.5 6.5,14.5 V8 C7,5.5 11.5,4 16,4 Z", TealGrad);
        Path(drawing, "M16,5 C12,5 8.5,6.2 8,8.5 V14 C8,18 10,21 16,23.5 C16,14 16,7 16,5 Z", GlassGrad);
        Ellipse(drawing, 13, 8.5, 6, 6, GoldGrad, White, 0.8);
        Ellipse(drawing, 14.5, 10, 3, 3, Charcoal);
        Rect(drawing, 14.8, 14, 2.4, 8.5, GoldGrad);
        Rect(drawing, 17.2, 18, 3, 1.8, GoldGrad);
        Rect(drawing, 17.2, 21, 2.4, 1.8, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildSecurityOverview()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M16,2 C22,2 27,4 28,7 V15 C28,23 22.5,28.5 16,31 C9.5,28.5 4,23 4,15 V7 C5,4 10,2 16,2 Z", VioletGrad, PurpleGrad, 1.2);
        Path(drawing, "M16,4 C20.5,4 25,5.5 25.5,8 V14.5 C25.5,21.5 21,26 16,28.5 C11,26 6.5,21.5 6.5,14.5 V8 C7,5.5 11.5,4 16,4 Z", RoyalGrad);
        Rect(drawing, 8, 18, 4, 7, CyanGrad, White, 1, 0.8);
        Rect(drawing, 14, 13, 4, 12, EmeraldGrad, White, 1, 0.8);
        Rect(drawing, 20, 8, 4, 17, AmberGrad, White, 1, 0.8);
        Path(drawing, "M8,17 L14,12 L20,7 L23,9", null, White, 1.8);
        return Image(drawing);
    }

    private static DrawingImage BuildPermissionCatalog()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 4, 4, 21, 25, RoyalGrad, SlateGrad, 2, 1.2);
        Rect(drawing, 4, 4, 5, 25, DarkSlateGrad, null, 2);
        Rect(drawing, 8, 5, 16, 23, White, SilverGrad, 1, 0.8);
        Rect(drawing, 24, 7, 4.5, 4, CrimsonGrad, null, 1);
        Rect(drawing, 24, 12.5, 4.5, 4, AmberGrad, null, 1);
        Rect(drawing, 24, 18, 4.5, 4, EmeraldGrad, null, 1);
        Rect(drawing, 24, 23.5, 4.5, 4, CyanGrad, null, 1);
        Path(drawing, "M11,9 H21 M11,13 H21 M11,17 H18 M11,21 H20", null, BlueGrad, 1.4);
        Ellipse(drawing, 18, 22, 3.5, 3.5, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildAccessHealth()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M14,2 C19,2 23,4 24,6.5 V13 C24,20 19,24.5 14,27 C9,24.5 4,20 4,13 V6.5 C5,4 9,2 14,2 Z", EmeraldGrad, TealGrad, 1);
        Path(drawing, "M5,13 H8 L10.5,8 L13.5,18 L16,10 L18,14 H23", null, White, 2);
        Ellipse(drawing, 14, 14, 13, 13, GlassGrad, CyanGrad, 2.5);
        Path(drawing, "M23,23 L30,30", null, AmberGrad, 3.5);
        Path(drawing, "M16,16 A4,4 0 0 1 20,18", null, White, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildPeople()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 5, 7, 7, 7, TealGrad, White, 0.8);
        Path(drawing, "M2,27 C2,21.5 5,19 9,19 C12.5,19 15,21.5 15,27 Z", DarkTealGrad);
        Ellipse(drawing, 20, 7, 7, 7, PurpleGrad, White, 0.8);
        Path(drawing, "M17,27 C17,21.5 19.5,19 23,19 C27,19 30,21.5 30,27 Z", VioletGrad);
        Path(drawing, "M7,28 C7,21 10.5,18 16,18 C21.5,18 25,21 25,28 Z", RoyalGrad, White, 1);
        Path(drawing, "M14,18 L16,21 L18,18", White, null);
        Ellipse(drawing, 11.5, 4, 9, 9, AmberGrad, White, 1);
        Ellipse(drawing, 19.5, 22.5, 4, 4, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildStaff()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M12,1 L16,5 L20,1", null, RoyalGrad, 2);
        Rect(drawing, 13, 4, 6, 3, SilverGrad, SlateGrad, 1, 0.8);
        Rect(drawing, 5, 6, 22, 25, White, BlueGrad, 2.5, 1.2);
        Rect(drawing, 5, 6, 22, 6, TealGrad, null, 2);
        Rect(drawing, 8, 14, 7, 8, AmberGrad, SlateGrad, 1, 0.6);
        Ellipse(drawing, 9.5, 15, 4, 4, RoyalGrad);
        Path(drawing, "M8.5,21.5 C8.5,19.5 9.5,18.5 11.5,18.5 C13.5,18.5 14.5,19.5 14.5,21.5 Z", RoyalGrad);
        Path(drawing, "M17,15 H24 M17,18 H23 M17,21 H21 M9,25 H23 M9,28 H19", null, SlateGrad, 1.2);
        return Image(drawing);
    }

    private static DrawingImage BuildUserAccess()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 8, 4, 10, 10, BlueGrad, White, 1);
        Path(drawing, "M3,28 C3,20 6.5,17 13,17 C16.5,17 19.5,18.5 21,21", RoyalGrad, White, 1.2);
        Path(drawing, "M19,16 V12 C19,9.5 21,7.5 23.5,7.5 C26,7.5 28,9.5 28,12 V16", null, SilverGrad, 2.2);
        Rect(drawing, 17, 15, 13, 14, GoldGrad, OrangeGrad, 2, 1);
        Ellipse(drawing, 22.5, 19, 2.5, 2.5, Charcoal);
        Path(drawing, "M23.5,21 V25", null, Charcoal, 1.5);
        return Image(drawing);
    }

    private static DrawingImage BuildSettings()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 5, 7, 18, 18, TealGrad, White, 1);
        Rect(drawing, 12.5, 4, 3, 4, DarkTealGrad, radius: 0.8);
        Rect(drawing, 12.5, 24, 3, 4, DarkTealGrad, radius: 0.8);
        Rect(drawing, 2, 14.5, 4, 3, DarkTealGrad, radius: 0.8);
        Rect(drawing, 22, 14.5, 4, 3, DarkTealGrad, radius: 0.8);
        Rect(drawing, 5.5, 7.5, 3.5, 3.5, DarkTealGrad, radius: 0.8);
        Rect(drawing, 19, 21, 3.5, 3.5, DarkTealGrad, radius: 0.8);
        Rect(drawing, 5.5, 21, 3.5, 3.5, DarkTealGrad, radius: 0.8);
        Rect(drawing, 19, 7.5, 3.5, 3.5, DarkTealGrad, radius: 0.8);
        Ellipse(drawing, 9.5, 11.5, 9, 9, Charcoal, null);
        Ellipse(drawing, 12, 14, 4, 4, GoldGrad, White, 0.6);

        Ellipse(drawing, 18, 2, 12, 12, BlueGrad, White, 0.8);
        Rect(drawing, 22.5, 0, 3, 3, RoyalGrad, radius: 0.6);
        Rect(drawing, 22.5, 13, 3, 3, RoyalGrad, radius: 0.6);
        Rect(drawing, 16, 6.5, 3, 3, RoyalGrad, radius: 0.6);
        Rect(drawing, 29, 6.5, 3, 3, RoyalGrad, radius: 0.6);
        Ellipse(drawing, 21.5, 5.5, 5, 5, Charcoal, null);
        Ellipse(drawing, 23, 7, 2, 2, White);
        return Image(drawing);
    }

    private static DrawingImage BuildPrinter()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 8, 2, 16, 12, White, SilverGrad, 1.5, 0.8);
        Path(drawing, "M10,5 H22 M10,8 H18", null, BlueGrad, 1.2);
        Rect(drawing, 3, 11, 26, 15, SilverGrad, SlateGrad, 3, 1.2);
        Rect(drawing, 5, 13, 22, 6, DarkSlateGrad, null, 1.5);
        Ellipse(drawing, 8, 15, 2, 2, GlowGreen);
        Rect(drawing, 12, 14, 13, 4, CyanGrad, null, 1);
        Rect(drawing, 7, 19, 18, 10, White, SilverGrad, 1, 0.8);
        Path(drawing, "M9,22 H23 M9,25 H19", null, TealGrad, 1.3);
        Rect(drawing, 4, 26, 24, 4, SlateGrad, null, 1.5);
        return Image(drawing);
    }

    private static DrawingImage BuildScale()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 2, 22, 28, 8, DarkSlateGrad, SlateGrad, 2, 1);
        Rect(drawing, 4, 23, 24, 2, SilverGrad, null, 1);
        Rect(drawing, 5, 29, 4, 2, SlateGrad);
        Rect(drawing, 23, 29, 4, 2, SlateGrad);
        Rect(drawing, 14, 16, 4, 7, SlateGrad);
        Rect(drawing, 5, 3, 22, 14, SlateGrad, DarkSlateGrad, 3, 1.2);
        Rect(drawing, 7, 5, 18, 7.5, Charcoal, null, 1.5);
        Path(drawing, "M9,8.5 H13 M15,8.5 H18 M20,8.5 H23 M9,10.5 H21", null, GlowGreen, 1.5);
        Ellipse(drawing, 9, 14, 2, 2, GlowGreen);
        Ellipse(drawing, 15, 14, 2, 2, GlowAmber);
        Ellipse(drawing, 21, 14, 2, 2, CyanGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildSignOut()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 4, 3, 16, 26, SlateGrad, DarkSlateGrad, 3, 1);
        Rect(drawing, 6, 5, 12, 22, DarkSlateGrad);
        Path(drawing, "M6,5 L16,8 V24 L6,27 Z", CrimsonGrad, RedGrad, 1);
        Ellipse(drawing, 14, 16, 2, 2, GoldGrad);
        Path(drawing, "M13,16 H29 M23,10 L29.5,16 L23,22", null, CyanGrad, 3.2);
        Path(drawing, "M13,16 H29 M23,10 L29.5,16 L23,22", null, White, 1.4);
        return Image(drawing);
    }

    private static DrawingImage BuildTable()
    {
        var drawing = new DrawingGroup();
        Rect(drawing, 3, 4, 26, 24, White, BlueGrad, 2.5, 1.2);
        Rect(drawing, 3, 4, 26, 6, DarkTealGrad, null, 2);
        Path(drawing, "M3,16 H29 M3,22 H29 M11,4 V28 M20,4 V28", null, SilverGrad, 1.2);
        Rect(drawing, 4, 11, 6, 4, EmeraldGrad, radius: 0.8);
        Rect(drawing, 12, 17, 7, 4, CyanGrad, radius: 0.8);
        Rect(drawing, 21, 23, 7, 4, AmberGrad, radius: 0.8);
        return Image(drawing);
    }

    private static DrawingImage BuildHelp()
    {
        var drawing = new DrawingGroup();
        Ellipse(drawing, 3, 3, 26, 26, RoyalGrad, CyanGrad, 1.5);
        Ellipse(drawing, 6, 5, 16, 10, GlassGrad);
        Path(drawing, "M11,12 C11,8 14.5,6.5 17,6.5 C20.5,6.5 22.5,8.5 22.5,11.5 C22.5,14.5 20,16 17.5,18 C16,19 16,20 16,21.5", null, White, 2.8);
        Ellipse(drawing, 14.5, 23.5, 3, 3, GoldGrad);
        return Image(drawing);
    }

    private static DrawingImage BuildHeadset()
    {
        var drawing = new DrawingGroup();
        Path(drawing, "M5,18 V15 C5,8 10,4 16,4 C22,4 27,8 27,15 V18", null, RoyalGrad, 3);
        Rect(drawing, 3, 14, 6, 11, CyanGrad, White, 2, 0.8);
        Rect(drawing, 23, 14, 6, 11, CyanGrad, White, 2, 0.8);
        Path(drawing, "M25,23 C24,28 20.5,29 16,29 H13", null, SlateGrad, 2);
        Ellipse(drawing, 10, 27, 4, 4, EmeraldGrad);
        return Image(drawing);
    }
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace YTRC.ERP.Wpf;

/// <summary>
/// Central Design Tokens for YTRC ERP Application.
/// Provides unified values for Corner Radii, Typography, Spacing, Dimensions, and Palette.
/// Can be accessed directly in C# or via {StaticResource ...} in XAML (configured in App.xaml).
/// </summary>
public static class UiTokens
{
    // =========================================================================
    // 1. CORNER RADII (ขนาดความโค้งมนของขอบ)
    // =========================================================================
    public const double CornerRadiusNoneValue = 0;
    public const double CornerRadiusSmallValue = 4;
    public const double CornerRadiusControlValue = 6;
    public const double CornerRadiusMediumValue = 8;
    public const double CornerRadiusCardValue = 8;
    public const double CornerRadiusLargeValue = 12;
    public const double CornerRadiusPillValue = 999;

    public static readonly CornerRadius CornerRadiusNone = new(CornerRadiusNoneValue);
    public static readonly CornerRadius CornerRadiusSmall = new(CornerRadiusSmallValue);
    public static readonly CornerRadius CornerRadiusControl = new(CornerRadiusControlValue);
    public static readonly CornerRadius CornerRadiusMedium = new(CornerRadiusMediumValue);
    public static readonly CornerRadius CornerRadiusCard = new(CornerRadiusCardValue);
    public static readonly CornerRadius CornerRadiusLarge = new(CornerRadiusLargeValue);
    public static readonly CornerRadius CornerRadiusPill = new(CornerRadiusPillValue);

    public static class Radii
    {
        public static CornerRadius None => CornerRadiusNone;
        public static CornerRadius Small => CornerRadiusSmall;
        public static CornerRadius Control => CornerRadiusControl;
        public static CornerRadius Medium => CornerRadiusMedium;
        public static CornerRadius Card => CornerRadiusCard;
        public static CornerRadius Large => CornerRadiusLarge;
        public static CornerRadius Pill => CornerRadiusPill;
    }

    // =========================================================================
    // 2. TYPOGRAPHY (ขนาดและน้ำหนักตัวอักษร)
    // =========================================================================
    public const double FontSizeHero = 24;
    public const double FontSizePageTitle = 19;
    public const double FontSizeSection = 16;
    public const double FontSizeCardTitle = 14;
    public const double FontSizeBody = 13;
    public const double FontSizeCaption = 12;
    public const double FontSizeSmall = 11;
    public const double FontSizeBadge = 10;

    public static class FontSizes
    {
        public const double Hero = FontSizeHero;
        public const double PageTitle = FontSizePageTitle;
        public const double Section = FontSizeSection;
        public const double CardTitle = FontSizeCardTitle;
        public const double Body = FontSizeBody;
        public const double Caption = FontSizeCaption;
        public const double Small = FontSizeSmall;
        public const double Badge = FontSizeBadge;
    }

    public static class Weights
    {
        public static FontWeight Normal => FontWeights.Normal;
        public static FontWeight Medium => FontWeights.Medium;
        public static FontWeight SemiBold => FontWeights.SemiBold;
        public static FontWeight Bold => FontWeights.Bold;
    }

    public static readonly FontFamily AppFontFamily = new("Leelawadee UI");

    // =========================================================================
    // 3. SPACING & PADDINGS (ระยะขอบและช่องไฟ)
    // =========================================================================
    public static readonly Thickness PageMargin = new(20, 12, 20, 16);
    public static readonly Thickness HeroHeaderPadding = new(18, 12, 18, 12);
    public static readonly Thickness CardPadding = new(16, 14, 16, 14);
    public static readonly Thickness CardPaddingCompact = new(12, 10, 12, 10);
    public static readonly Thickness ButtonPadding = new(14, 8, 14, 8);
    public static readonly Thickness ButtonPaddingCompact = new(10, 6, 10, 6);
    public static readonly Thickness BadgePadding = new(7, 2, 7, 2);
    public static readonly Thickness FieldMargin = new(0, 0, 0, 8);
    public static readonly Thickness FieldMarginCompact = new(0, 0, 7, 0);

    public static class Spacing
    {
        public const double GapXSmall = 4;
        public const double GapSmall = 8;
        public const double GapMedium = 12;
        public const double GapLarge = 16;
        public const double GapXLarge = 24;
    }

    // =========================================================================
    // 4. DIMENSIONS (ขนาดความกว้าง/สูงขององค์ประกอบ)
    // =========================================================================
    public const double DatePickerWidth = 130;
    public const double DatePickerHeight = 34;
    public const double ButtonHeight = 34;
    public const double ButtonHeightSmall = 28;
    public const double InputHeight = 34;
    public const double HeroIconSize = 36;
    public const double ToolbarButtonSize = 34;

    // =========================================================================
    // 5. COLORS & BRUSHES (ชุดสีและแปรงมาตรฐาน)
    // =========================================================================
    public static class Colors
    {
        public static readonly Color Accent = Color.FromRgb(15, 118, 110);           // #0F766E Teal
        public static readonly Color AccentHover = Color.FromRgb(17, 94, 89);        // #115E59
        public static readonly Color MintSurface = Color.FromRgb(240, 253, 250);     // #F0FDFA
        public static readonly Color MintBorder = Color.FromRgb(204, 251, 241);      // #CCFBF1
        public static readonly Color MintBadgeBorder = Color.FromRgb(153, 246, 228); // #99F6E4
        public static readonly Color CanvasBackground = Color.FromRgb(245, 247, 250); // #F5F7FA
        public static readonly Color CardBackground = System.Windows.Media.Colors.White;
        public static readonly Color BorderSoft = Color.FromRgb(225, 231, 239);      // #E1E7EF
        public static readonly Color BorderMedium = Color.FromRgb(216, 224, 234);    // #D8E0EA
        public static readonly Color TextPrimary = Color.FromRgb(30, 45, 62);        // #1E2D3E
        public static readonly Color TextMuted = Color.FromRgb(104, 120, 141);       // #68788D
        public static readonly Color Success = Color.FromRgb(16, 185, 129);          // #10B981
        public static readonly Color Warning = Color.FromRgb(245, 158, 11);          // #F59E0B
        public static readonly Color Danger = Color.FromRgb(239, 68, 68);            // #EF4444
        public static readonly Color Info = Color.FromRgb(2, 132, 199);              // #0284C7
    }

    public static class Brushes
    {
        public static readonly SolidColorBrush Accent = CreateFrozen(Colors.Accent);
        public static readonly SolidColorBrush AccentHover = CreateFrozen(Colors.AccentHover);
        public static readonly SolidColorBrush MintSurface = CreateFrozen(Colors.MintSurface);
        public static readonly SolidColorBrush MintBorder = CreateFrozen(Colors.MintBorder);
        public static readonly SolidColorBrush MintBadgeBorder = CreateFrozen(Colors.MintBadgeBorder);
        public static readonly SolidColorBrush CanvasBackground = CreateFrozen(Colors.CanvasBackground);
        public static readonly SolidColorBrush CardBackground = CreateFrozen(Colors.CardBackground);
        public static readonly SolidColorBrush BorderSoft = CreateFrozen(Colors.BorderSoft);
        public static readonly SolidColorBrush BorderMedium = CreateFrozen(Colors.BorderMedium);
        public static readonly SolidColorBrush TextPrimary = CreateFrozen(Colors.TextPrimary);
        public static readonly SolidColorBrush TextMuted = CreateFrozen(Colors.TextMuted);
        public static readonly SolidColorBrush Success = CreateFrozen(Colors.Success);
        public static readonly SolidColorBrush Warning = CreateFrozen(Colors.Warning);
        public static readonly SolidColorBrush Danger = CreateFrozen(Colors.Danger);
        public static readonly SolidColorBrush Info = CreateFrozen(Colors.Info);

        private static SolidColorBrush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }

    // =========================================================================
    // 6. UI COMPONENT FACTORY HELPERS (ตัวช่วยสร้างส่วนประกอบมาตรฐาน)
    // =========================================================================
    public static TextBlock CreateHeading(
        string text,
        double size = FontSizeSection,
        FontWeight? weight = null,
        Brush? foreground = null,
        Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeights.SemiBold,
            Foreground = foreground ?? Brushes.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = AppFontFamily,
            Margin = margin ?? new Thickness(0)
        };
    }

    public static TextBlock CreateLabel(
        string text,
        double size = FontSizeBody,
        Brush? foreground = null,
        FontWeight? weight = null,
        bool center = false,
        Thickness? margin = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            Foreground = foreground ?? Brushes.TextPrimary,
            FontWeight = weight ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = AppFontFamily,
            Margin = margin ?? new Thickness(0, 2, 0, 2)
        };
    }

    public static Border CreateBadge(
        string text,
        Brush? background = null,
        Brush? foreground = null,
        Brush? border = null,
        CornerRadius? cornerRadius = null,
        Thickness? padding = null)
    {
        return new Border
        {
            Background = background ?? Brushes.MintSurface,
            BorderBrush = border ?? Brushes.MintBadgeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = cornerRadius ?? CornerRadiusSmall,
            Padding = padding ?? BadgePadding,
            Child = new TextBlock
            {
                Text = text.ToUpperInvariant(),
                FontSize = FontSizeBadge,
                FontWeight = FontWeights.Bold,
                Foreground = foreground ?? Brushes.Accent,
                FontFamily = AppFontFamily,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    public static Border CreateCard(
        UIElement child,
        Thickness? padding = null,
        Thickness? margin = null,
        CornerRadius? cornerRadius = null,
        Brush? background = null,
        Brush? borderBrush = null)
    {
        return new Border
        {
            Background = background ?? Brushes.CardBackground,
            BorderBrush = borderBrush ?? Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = cornerRadius ?? CornerRadiusCard,
            Padding = padding ?? CardPadding,
            Margin = margin ?? new Thickness(0),
            Child = child
        };
    }
}

public static class UiControlProperties
{
    public static readonly DependencyProperty ButtonCornerRadiusProperty = DependencyProperty.RegisterAttached(
        "ButtonCornerRadius",
        typeof(CornerRadius),
        typeof(UiControlProperties),
        new FrameworkPropertyMetadata(UiTokens.CornerRadiusSmall));

    public static CornerRadius GetButtonCornerRadius(DependencyObject element)
        => (CornerRadius)element.GetValue(ButtonCornerRadiusProperty);

    public static void SetButtonCornerRadius(DependencyObject element, CornerRadius value)
        => element.SetValue(ButtonCornerRadiusProperty, value);
}

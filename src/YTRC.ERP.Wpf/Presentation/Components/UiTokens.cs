using System.Windows;

namespace YTRC.ERP.Wpf;

public static class UiTokens
{
    public const double CornerRadiusSmallValue = 4;
    public const double CornerRadiusMediumValue = 8;
    public const double CornerRadiusLargeValue = 8;

    public static readonly CornerRadius CornerRadiusSmall = new(CornerRadiusSmallValue);
    public static readonly CornerRadius CornerRadiusMedium = new(CornerRadiusMediumValue);
    public static readonly CornerRadius CornerRadiusLarge = new(CornerRadiusLargeValue);

    public const double DatePickerWidth = 178;
    public const double DatePickerHeight = 34;
}

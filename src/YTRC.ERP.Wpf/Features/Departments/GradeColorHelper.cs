using System.Windows.Media;

namespace YTRC.ERP.Wpf;

/// <summary>
/// Provides distinct color palettes for Marking Preparation Grades:
/// - 0241: Purple (สีม่วง)
/// - 0243: Green (สีเขียว)
/// - 0251: Pink (สีชมพู)
/// - 0263: Blue (สีฟ้า)
/// </summary>
public static class GradeColorHelper
{
    private static readonly Dictionary<string, Brush> BrushCache = new(StringComparer.OrdinalIgnoreCase);

    public static (string Foreground, string Background, string Border) GetGradeColorStrings(string? grade)
    {
        var g = grade?.Trim().ToUpperInvariant() ?? "";
        if (g.Contains("0241") || g.Contains("241"))
        {
            // Purple (สีม่วง) - Richer & darker
            return ("#581C87", "#E9D5FF", "#A855F7");
        }
        if (g.Contains("0243") || g.Contains("243"))
        {
            // Green (สีเขียว) - Richer & darker
            return ("#14532D", "#BBF7D0", "#22C55E");
        }
        if (g.Contains("0251") || g.Contains("251"))
        {
            // Pink (สีชมพู) - Richer & darker
            return ("#9D174D", "#FBCFE8", "#EC4899");
        }
        if (g.Contains("0263") || g.Contains("263"))
        {
            // Blue / Sky (สีฟ้า) - Richer & darker
            return ("#075985", "#BAE6FD", "#0EA5E9");
        }
        // Neutral fallback for unrecognized grades
        return ("#334155", "#F8FAFC", "#E2E8F0");
    }

    public static Brush GetBrush(string hex)
    {
        if (BrushCache.TryGetValue(hex, out var cached)) return cached;
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
        if (brush.CanFreeze) brush.Freeze();
        BrushCache[hex] = brush;
        return brush;
    }

    public static Brush GetForegroundBrush(string? grade) => GetBrush(GetGradeColorStrings(grade).Foreground);
    public static Brush GetBackgroundBrush(string? grade) => GetBrush(GetGradeColorStrings(grade).Background);
    public static Brush GetBorderBrush(string? grade) => GetBrush(GetGradeColorStrings(grade).Border);
}

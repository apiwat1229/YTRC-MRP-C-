namespace YTRC.ERP.Wpf;

internal static class MarkingPreparationCatalogHelper
{
    public static string CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim();
        if (trimmed.StartsWith("CY/CY", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("CY / CY", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("CY-CY", StringComparison.OrdinalIgnoreCase) ||
            (trimmed.IndexOf("CY", StringComparison.OrdinalIgnoreCase) >= 0 &&
             (trimmed.IndexOf("LOADING PORT", StringComparison.OrdinalIgnoreCase) >= 0 ||
              trimmed.IndexOf("FREETIME", StringComparison.OrdinalIgnoreCase) >= 0)))
        {
            return "CY / CY";
        }
        return trimmed;
    }
}

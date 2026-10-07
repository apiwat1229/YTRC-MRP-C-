namespace YTRC.ERP.Domain.Configuration;

public static class ServerUrl
{
    public static string NormalizeBaseUrl(string? raw)
    {
        var value = (raw ?? "").Trim().TrimEnd('/');
        if (value.Length == 0 || value.Contains("app.ytrc.co.th", StringComparison.OrdinalIgnoreCase))
            return "https://api.ytrc.co.th";
        if (!value.Contains("://", StringComparison.Ordinal))
            value = value.Contains(":2530", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                ? "http://" + value : "https://" + value;
        if (value.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            value = value[..^4];
        return value.TrimEnd('/');
    }
}

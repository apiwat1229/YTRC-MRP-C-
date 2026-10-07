using System.Text.Json;
using YTRC.ERP.Application.Contracts;

namespace YTRC.ERP.Application.Mapping;

public static class ApiRowMapper
{
    public static IReadOnlyList<ApiRow> Map(JsonElement payload, string kind)
    {
        var rows = payload.ValueKind == JsonValueKind.Array
            ? payload.EnumerateArray().ToArray()
            : payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray().ToArray()
                : payload.ValueKind == JsonValueKind.Object ? [payload] : [];
        return rows.Select((item, i) => new ApiRow(
            First(item, "displayName", "fullName", "name", "username", "roleName", "role", "title") ?? $"{kind} {i + 1}",
            First(item, "username", "email", "department", "position", "description", "code") ?? "",
            First(item, "email", "department", "position", "permissions", "description") ?? "",
            First(item, "status", "state", "isActive") ?? "—")).ToArray();
    }

    private static string? First(JsonElement item, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = ReadValue(item, key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
            if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(key, out var array) && array.ValueKind == JsonValueKind.Array)
                return string.Join(", ", array.EnumerateArray().Select(x => x.ToString()));
        }
        return null;
    }

    private static string? ReadValue(JsonElement item, string key)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(key, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => null
        };
    }
}

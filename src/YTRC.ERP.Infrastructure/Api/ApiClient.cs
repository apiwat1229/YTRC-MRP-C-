using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Configuration;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Infrastructure.Api;

public sealed class ApiClient : IApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private const string AppVersion = "0.1.0";

    public async Task<ApiHealth> TestConnectionAsync(string baseUrl, CancellationToken ct = default)
    {
        baseUrl = ServerUrl.NormalizeBaseUrl(baseUrl);
        var first = await GetUnauthenticatedAsync(baseUrl + "/api/health/smoke-test", ct);
        if (first.Success)
            return new(true, ReadString(first.Data, "version") ?? "1.0.1", "Connected to YTRC FastAPI backend.");
        var fallback = await GetUnauthenticatedAsync(baseUrl + "/api/health", ct);
        return fallback.Success
            ? new(true, ReadString(fallback.Data, "version") ?? "1.0.0", "Connected to YTRC FastAPI backend.")
            : new(false, "", fallback.Error.Length > 0 ? fallback.Error : first.Error);
    }

    public async Task<LoginResult> LoginAsync(string baseUrl, string username, string password,
        CancellationToken ct = default)
    {
        baseUrl = ServerUrl.NormalizeBaseUrl(baseUrl);
        var credentials = username.Contains('@')
            ? new Dictionary<string, string> { ["email"] = username, ["password"] = password }
            : new Dictionary<string, string> { ["username"] = username, ["password"] = password };
        try
        {
            using var response = await Http.PostAsync(baseUrl + "/api/auth/login",
                new StringContent(JsonSerializer.Serialize(credentials), Encoding.UTF8, "application/json"), ct);
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (!response.IsSuccessStatusCode)
                return new(false, null, "", ReadError(root, $"Sign in failed (HTTP {(int)response.StatusCode})."));
            var token = ReadString(root, "accessToken") ?? ReadString(root, "access_token") ?? "";
            if (string.IsNullOrWhiteSpace(token))
                return new(false, null, "", "Invalid server response: accessToken is missing.");

            var userNode = root.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object ? user : root;
            var display = ReadString(userNode, "displayName") ?? "";
            if (display.Length == 0)
            {
                var first = ReadString(userNode, "firstName") ?? "";
                var last = ReadString(userNode, "lastName") ?? "";
                display = string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            var name = ReadString(userNode, "username") ?? username;
            var roleRaw = (ReadString(userNode, "role") ?? ReadString(userNode, "position") ?? "operator").ToLowerInvariant();
            var deptRaw = ReadString(userNode, "department") ?? "";
            var rawPermissions = ReadStringArray(userNode, "permissions");
            var profile = new UserProfile
            {
                UserId = ReadString(userNode, "id") ?? "",
                Username = name,
                FullName = display.Length > 0 ? display : name,
                Email = ReadString(userNode, "email") ?? "",
                DepartmentName = deptRaw,
                PrimaryDepartment = ParseDepartment(deptRaw),
                Role = ParseRole(roleRaw),
                RawPermissions = rawPermissions
            };
            return new(true, profile, token, "");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(false, null, "", ex.Message); }
    }

    public Task<ApiResponse<JsonElement>> GetAsync(string endpoint, string token, CancellationToken ct = default)
    {
        var url = endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? endpoint : ServerUrl.NormalizeBaseUrl(AppSettings.Current.ApiBaseUrl) + endpoint;
        return SendGetAsync(url, token, ct);
    }

    public Task<ApiResponse<JsonElement>> PostJsonAsync(string endpoint, object payload, string token,
        CancellationToken ct = default) => SendJsonAsync(HttpMethod.Post, endpoint, payload, token, ct);

    public Task<ApiResponse<JsonElement>> PutJsonAsync(string endpoint, object payload, string token,
        CancellationToken ct = default) => SendJsonAsync(HttpMethod.Put, endpoint, payload, token, ct);

    public Task<ApiResponse<JsonElement>> PatchJsonAsync(string endpoint, object payload, string token,
        CancellationToken ct = default) => SendJsonAsync(HttpMethod.Patch, endpoint, payload, token, ct);

    public Task<ApiResponse<JsonElement>> DeleteAsync(string endpoint, string token,
        CancellationToken ct = default) => SendJsonAsync(HttpMethod.Delete, endpoint, null, token, ct);

    private async Task<ApiResponse<JsonElement>> SendJsonAsync(HttpMethod method, string endpoint,
        object? payload, string token, CancellationToken ct)
    {
        var url = endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? endpoint : ServerUrl.NormalizeBaseUrl(AppSettings.Current.ApiBaseUrl) + endpoint;
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (payload is not null)
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            JsonElement body;
            if (string.IsNullOrWhiteSpace(content))
            {
                using var emptyDoc = JsonDocument.Parse("{}");
                body = emptyDoc.RootElement.Clone();
            }
            else using (var doc = JsonDocument.Parse(content)) body = doc.RootElement.Clone();
            return response.IsSuccessStatusCode
                ? new(true, body, (int)response.StatusCode, "")
                : new(false, body, (int)response.StatusCode,
                    ReadError(body, $"Request failed (HTTP {(int)response.StatusCode})."));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(false, default, 0, ex.Message); }
    }

    private static async Task<ApiResponse<JsonElement>> GetUnauthenticatedAsync(string url, CancellationToken ct)
        => await SendGetAsync(url, "", ct);

    private static async Task<ApiResponse<JsonElement>> SendGetAsync(string url, string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var clone = doc.RootElement.Clone();
            if (!response.IsSuccessStatusCode)
                return new(false, clone, (int)response.StatusCode,
                    ReadError(clone, $"Request failed (HTTP {(int)response.StatusCode})."));
            return new(true, clone, (int)response.StatusCode, "");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return new(false, default, 0, ex.Message); }
    }

    public static string? ReadString(JsonElement node, string key)
    {
        if (node.ValueKind == JsonValueKind.Object && node.TryGetProperty(key, out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
        return null;
    }

    public static string ReadError(JsonElement root, string fallback)
        => ReadString(root, "detail") ?? ReadString(root, "message") ?? ReadString(root, "error") ?? fallback;

    public static IReadOnlyList<string> ReadStringArray(JsonElement node, string key)
    {
        if (!node.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array) return [];
        return list.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString() ?? "").Where(v => v.Length > 0).ToArray();
    }

    private static UserRole ParseRole(string role) => role.Contains("admin") ? UserRole.SystemAdmin
        : role.Contains("manager") ? UserRole.Manager
        : role.Contains("supervisor") ? UserRole.Supervisor : UserRole.Operator;

    private static DepartmentId ParseDepartment(string department)
    {
        var d = department.ToLowerInvariant();
        if (d.Contains("quality") || d.Contains("qa")) return DepartmentId.QA;
        if (d.Contains("purchase") || d == "po") return DepartmentId.Purchase;
        if (d.Contains("receiv")) return DepartmentId.Receiving;
        if (d.Contains("warehouse") || d == "wh") return DepartmentId.Warehouse;
        if (d.Contains("production") || d.Contains("prod")) return DepartmentId.Production;
        if (d.Contains("shipping")) return DepartmentId.Shipping;
        if (d.Contains("maint") || d.Contains("mymachine")) return DepartmentId.Maintenance;
        if (d == "it" || d.StartsWith("it ") || d.Contains("information technology") || d.Contains("it support")) return DepartmentId.IT;
        if (d.Contains("rrc")) return DepartmentId.RRC;
        return DepartmentId.Home;
    }
}

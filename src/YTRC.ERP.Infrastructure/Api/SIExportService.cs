using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using YTRC.ERP.Application.Configuration;
using YTRC.ERP.Domain.Configuration;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Infrastructure.Api;

public sealed class SIExportService(IApiClient api) : ISIExportService
{
    private const string Route = "/api/si-exports";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private static T Read<T>(ApiResponse<JsonElement> response)
    {
        if (!response.Success) throw new InvalidOperationException(response.Error);
        return response.Data.Deserialize<T>(Options)
            ?? throw new InvalidOperationException("Backend returned an empty SI response");
    }

    public async Task<Dictionary<string, List<string>>> CatalogAsync(string token)
        => Read<Dictionary<string, List<string>>>(await api.GetAsync(Route + "/catalog", token));

    public async Task<SIExportMigrationSummary> MigrationSummaryAsync(string token)
        => Read<SIExportMigrationSummary>(await api.GetAsync(Route + "/migration-summary", token));

    public async Task<List<SIExportSourceFile>> SourceFilesAsync(string token)
        => Read<List<SIExportSourceFile>>(await api.GetAsync(Route + "/source-files", token));

    public async Task<JsonElement> SourceFileAsync(string token, string id)
        => Read<JsonElement>(await api.GetAsync(Route + "/source-files/" + Uri.EscapeDataString(id), token));

    public async Task SaveOriginalAsync(string token, SIExportSourceFile source, string path)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var request = new HttpRequestMessage(HttpMethod.Get,
            ServerUrl.NormalizeBaseUrl(AppSettings.Current.ApiBaseUrl) + Route + "/source-files/" + Uri.EscapeDataString(source.Id) + "/original");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(source.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ไฟล์ต้นฉบับไม่ตรงกับ checksum ที่บันทึกไว้");
        await File.WriteAllBytesAsync(path, bytes);
    }

    public async Task<SIExportPage> ListAsync(string token, string search, int? year, int? month, int skip)
    {
        var query = $"?take=50&skip={skip}&search={Uri.EscapeDataString(search)}";
        if (year is not null) query += $"&year={year}";
        if (month is not null) query += $"&month={month}";
        return Read<SIExportPage>(await api.GetAsync(Route + query, token));
    }

    public async Task<SIExportSchedule> GetAsync(string token, string id)
        => Read<SIExportSchedule>(await api.GetAsync($"{Route}/{Uri.EscapeDataString(id)}", token));

    public async Task<SIExportSchedule> SaveAsync(string token, string? id, SIExportSave payload)
    {
        SIExportValidation.Validate(payload);
        return Read<SIExportSchedule>(id is null
            ? await api.PostJsonAsync(Route, JsonSerializer.SerializeToElement(payload, Options), token)
            : await api.PutJsonAsync($"{Route}/{Uri.EscapeDataString(id)}", JsonSerializer.SerializeToElement(payload, Options), token));
    }

    public async Task<SIExportSchedule> ActionAsync(string token, string id, int revision, string reason, bool confirm)
        => Read<SIExportSchedule>(await api.PostJsonAsync(
            $"{Route}/{Uri.EscapeDataString(id)}/{(confirm ? "confirm" : "cancel")}",
            new { expectedRevision = revision, reason }, token));

    public async Task<List<SIExportRevision>> RevisionsAsync(string token, string id)
        => Read<List<SIExportRevision>>(await api.GetAsync($"{Route}/{Uri.EscapeDataString(id)}/revisions", token));

    public async Task<SIExportComparison> CompareAsync(string token, string id, int from, int to)
        => Read<SIExportComparison>(await api.GetAsync(
            $"{Route}/{Uri.EscapeDataString(id)}/compare?fromRevision={from}&toRevision={to}", token));
}

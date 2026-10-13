using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<SIExportPage> ListAsync(string token, string search, int skip, int? year = null, int? month = null, int take = 50)
    {
        var query = $"?take={take}&skip={skip}&search={Uri.EscapeDataString(search)}";
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

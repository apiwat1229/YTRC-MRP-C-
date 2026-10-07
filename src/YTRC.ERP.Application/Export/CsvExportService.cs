using YTRC.ERP.Application.Contracts;

namespace YTRC.ERP.Application.Export;

public static class CsvExportService
{
    public static string ToCsv(IEnumerable<ApiRow> rows)
    {
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        return "Name,Account / Department,Details,Status\r\n" + string.Join("\r\n", rows.Select(row =>
            string.Join(',', Quote(row.Title), Quote(row.Subtitle), Quote(row.Detail), Quote(row.Status))));
    }
}

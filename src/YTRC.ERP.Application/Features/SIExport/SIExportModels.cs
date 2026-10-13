using System.Text.Json;
using System.Globalization;

namespace YTRC.ERP.Application.Features.SIExport;

public sealed class SIExportLine
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int No { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Contract { get; set; } = "";
    public string CustomerPo { get; set; } = "";
    public string FullContract { get; set; } = "";
    public string Plant { get; set; } = "";
    public string Grade { get; set; } = "";
    public string Code { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Factory { get; set; } = "";
    public string ContractType { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public string QuantityUnit { get; set; } = "MT";
    public string Package { get; set; } = "";
    public int? MbUnits { get; set; }
    public string Pol { get; set; } = "";
    public string Pod { get; set; } = "";
    public string TransportMode { get; set; } = "CONTAINER";
    public int? ContainerCount { get; set; }
    public string ContainerType { get; set; } = "";
    public string SharedContainerGroup { get; set; } = "";
    public string Service { get; set; } = "";
    public string Carrier { get; set; } = "";
    public string CarrierContractId { get; set; } = "";
    public string Vessel { get; set; } = "";
    public string SecondNotifyParty { get; set; } = "";
    public DateOnly? PlannedEtd { get; set; }
    public DateOnly? PlannedEta { get; set; }
    public DateOnly? SentToCarrierDate { get; set; }
    public DateOnly? CarrierConfirmedDate { get; set; }
    public DateOnly? ActualEtd { get; set; }
    public DateOnly? ActualEta { get; set; }
    public bool Cancelled { get; set; }
    public string Remark { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string SourceData { get; set; } = "";
    public bool SourceReviewRequired { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string ShipmentStatus => Cancelled ? "CANCELLED" : ActualEta is not null ? "ARRIVED"
        : ActualEtd is not null ? "DEPARTED" : CarrierConfirmedDate is not null ? "CARRIER CONFIRMED"
        : SentToCarrierDate is not null ? "SENT TO CARRIER" : "PLANNED";
}

public class SIExportDocument
{
    public string Title { get; set; } = "";
    public string Recipient { get; set; } = "";
    public int ShipmentYear { get; set; } = DateTime.Today.Year;
    public int ShipmentMonth { get; set; } = DateTime.Today.Month;
    public string PlanningStatus { get; set; } = "DRAFT";
    public string Remark { get; set; } = "";
    public List<SIExportLine> Lines { get; set; } = [];
}

public sealed class SIExportSave : SIExportDocument
{
    public int? ExpectedRevision { get; set; }
    public string ChangeReason { get; set; } = "";
}

public sealed class SIExportSchedule
{
    public int No { get; set; }
    public string Id { get; set; } = "";
    public string ScheduleNo { get; set; } = "";
    public int Revision { get; set; }
    public string Status { get; set; } = "DRAFT";
    public SIExportDocument Document { get; set; } = new();
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedAtDisplay => UpdatedAt.ToLocalTime()
        .ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture);
    public List<string> ValidationIssues { get; set; } = [];
    public string Title => Document.Title;
    public string Recipient => Document.Recipient;
    public string Period => $"{Document.ShipmentYear}-{Document.ShipmentMonth:00}";
    public int LineCount => Document.Lines.Count;
    public string RevisionLabel => $"R{Revision:00}";
}

public sealed class SIExportPage
{
    public List<SIExportSchedule> Items { get; set; } = [];
    public int Total { get; set; }
}

public sealed class SIExportRevision
{
    public int Revision { get; set; }
    public string Action { get; set; } = "";
    public string Status { get; set; } = "";
    public string Reason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public SIExportDocument Document { get; set; } = new();
}

public sealed class SIExportComparison
{
    public int FromRevision { get; set; }
    public int ToRevision { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public List<SIExportChange> Changes { get; set; } = [];
}

public sealed class SIExportChange
{
    public string? LineId { get; set; }
    public string? Contract { get; set; }
    public string Field { get; set; } = "";
    public JsonElement Before { get; set; }
    public JsonElement After { get; set; }
    public string BeforeText => Before.ToString();
    public string AfterText => After.ToString();
}

public interface ISIExportService
{
    Task<Dictionary<string, List<string>>> CatalogAsync(string token);
    Task<SIExportPage> ListAsync(string token, string search, int skip, int? year = null, int? month = null, int take = 50);
    Task<SIExportSchedule> GetAsync(string token, string id);
    Task<SIExportSchedule> SaveAsync(string token, string? id, SIExportSave payload);
    Task<SIExportSchedule> ActionAsync(string token, string id, int revision, string reason, bool confirm);
    Task<List<SIExportRevision>> RevisionsAsync(string token, string id);
    Task<SIExportComparison> CompareAsync(string token, string id, int from, int to);
}

public static class SIExportValidation
{
    public static void Validate(SIExportDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.Title) || string.IsNullOrWhiteSpace(document.Recipient))
            throw new ArgumentException("Enter an SI title and recipient.");
        if (document.ShipmentYear < 2025 || document.ShipmentYear > 2100
            || document.ShipmentMonth < 1 || document.ShipmentMonth > 12)
            throw new ArgumentException("Enter a shipment year from 2025 and a month from 1 to 12.");
        if (document.Lines.Count == 0) throw new ArgumentException("Add at least one contract line.");
        foreach (var line in document.Lines) ValidateLine(line);
    }

    public static void ValidateLine(SIExportLine line)
    {
        if ((string.IsNullOrWhiteSpace(line.Contract) && string.IsNullOrWhiteSpace(line.CustomerPo)
            && string.IsNullOrWhiteSpace(line.FullContract)) || line.Quantity <= 0)
            throw new ArgumentException("Enter a contract number or PO and a quantity greater than zero.");
        if (decimal.Round(line.Quantity, 3) != line.Quantity || line.MbUnits < 0 || line.ContainerCount < 0)
            throw new ArgumentException("Quantity supports up to 3 decimal places; counts cannot be negative.");
        if (line.PlannedEtd is { } etd && line.PlannedEta is { } eta && eta < etd)
            throw new ArgumentException("Planned ETA cannot be earlier than planned ETD.");
        if (line.ActualEta is { } arrival && (line.ActualEtd is null || arrival < line.ActualEtd))
            throw new ArgumentException("Actual ETA requires an actual ETD and cannot be earlier than it.");
        if (line.SentToCarrierDate is { } sent && line.CarrierConfirmedDate is { } confirmed && confirmed < sent)
            throw new ArgumentException("Carrier confirmation cannot be earlier than the sent date.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (new[] { line.SentToCarrierDate, line.CarrierConfirmedDate, line.ActualEtd, line.ActualEta }
            .Any(date => date > today))
            throw new ArgumentException("Actual milestone dates cannot be in the future.");
    }
}

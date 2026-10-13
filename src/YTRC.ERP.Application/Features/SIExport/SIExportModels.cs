using System.Text.Json;

namespace YTRC.ERP.Application.Features.SIExport;

public sealed class SIExportLine
{
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
    public string Id { get; set; } = "";
    public string ScheduleNo { get; set; } = "";
    public int Revision { get; set; }
    public string Status { get; set; } = "DRAFT";
    public SIExportDocument Document { get; set; } = new();
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
    Task<SIExportPage> ListAsync(string token, string search, int? year, int? month, int skip);
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
            throw new ArgumentException("กรุณาระบุชื่อชุด SI และผู้รับ");
        if (document.ShipmentYear < 2025 || document.ShipmentYear > 2100
            || document.ShipmentMonth < 1 || document.ShipmentMonth > 12)
            throw new ArgumentException("กรุณาระบุปีตั้งแต่ 2025 และเดือน 1–12");
        if (document.Lines.Count == 0) throw new ArgumentException("กรุณาเพิ่มรายการสัญญาอย่างน้อย 1 รายการ");
        foreach (var line in document.Lines) ValidateLine(line);
    }

    public static void ValidateLine(SIExportLine line)
    {
        if ((string.IsNullOrWhiteSpace(line.Contract) && string.IsNullOrWhiteSpace(line.CustomerPo)
            && string.IsNullOrWhiteSpace(line.FullContract)) || line.Quantity <= 0)
            throw new ArgumentException("กรุณาระบุเลขสัญญาหรือ PO และปริมาณมากกว่า 0");
        if (decimal.Round(line.Quantity, 3) != line.Quantity || line.MbUnits < 0 || line.ContainerCount < 0)
            throw new ArgumentException("ปริมาณใช้ทศนิยมได้ไม่เกิน 3 ตำแหน่ง และจำนวนต้องไม่ติดลบ");
        if (line.PlannedEtd is { } etd && line.PlannedEta is { } eta && eta < etd)
            throw new ArgumentException("กำหนดถึงต้องไม่ก่อนกำหนดออกเดินทาง");
        if (line.ActualEta is { } arrival && (line.ActualEtd is null || arrival < line.ActualEtd))
            throw new ArgumentException("วันถึงจริงต้องมีวันออกจริง และไม่ก่อนวันออกจริง");
        if (line.SentToCarrierDate is { } sent && line.CarrierConfirmedDate is { } confirmed && confirmed < sent)
            throw new ArgumentException("วันผู้ขนส่งยืนยันต้องไม่ก่อนวันที่ส่งให้ผู้ขนส่ง");
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (new[] { line.SentToCarrierDate, line.CarrierConfirmedDate, line.ActualEtd, line.ActualEta }
            .Any(date => date > today))
            throw new ArgumentException("วันที่เกิดขึ้นจริงต้องไม่เป็นวันในอนาคต");
    }
}

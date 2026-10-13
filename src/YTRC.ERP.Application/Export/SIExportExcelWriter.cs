using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Application.Export;

public static class SIExportExcelWriter
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly (string Label, string Property)[] Fields =
    [
        ("Contract #", "Contract"), ("Customer PO", "CustomerPo"), ("Full Contract A+B", "FullContract"),
        ("Plant", "Plant"), ("Grade", "Grade"), ("Code", "Code"), ("Supplier", "Supplier"), ("Factory", "Factory"),
        ("Contract Type", "ContractType"), ("Quantity", "Quantity"), ("Quantity Unit", "QuantityUnit"),
        ("Package", "Package"), ("MB Units", "MbUnits"), ("POL", "Pol"), ("POD", "Pod"),
        ("Transport Mode", "TransportMode"), ("Container Count", "ContainerCount"), ("Container Type", "ContainerType"),
        ("Shared Container Group", "SharedContainerGroup"), ("Planned ETD", "PlannedEtd"), ("Planned ETA", "PlannedEta"),
        ("Service", "Service"), ("Carrier Service Contract ID", "CarrierContractId"), ("Carrier", "Carrier"),
        ("Vessel / Voyage", "Vessel"), ("2nd Notify Party", "SecondNotifyParty"),
        ("Sent to Carrier Date", "SentToCarrierDate"), ("Carrier Confirmed Date", "CarrierConfirmedDate"),
        ("Actual ETD", "ActualEtd"), ("Actual ETA", "ActualEta"), ("Shipment Status", "ShipmentStatus"),
        ("Cancelled", "Cancelled"), ("Remark", "Remark"), ("Source Reference", "SourceReference"), ("Line ID", "Id")
    ];

    public static void Write(SIExportSchedule schedule, string path, bool separateCarriers)
    {
        var groups = separateCarriers
            ? schedule.Document.Lines.GroupBy(x => string.IsNullOrWhiteSpace(x.Carrier) ? "Unassigned" : x.Carrier,
                StringComparer.OrdinalIgnoreCase).Select(x => (Name: x.Key, Lines: x.ToList())).ToList()
            : [(Name: "SI Export", Lines: schedule.Document.Lines)];
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
        var contentTypes = new XElement(types + "Types",
            new XElement(types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
        XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        Entry(zip, "_rels/.rels", new XElement(rel + "Relationships", new XElement(rel + "Relationship",
            new XAttribute("Id", "rId1"), new XAttribute("Type", R.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
        var sheets = new XElement(S + "sheets");
        var relationships = new XElement(rel + "Relationships");
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < groups.Count; i++)
        {
            var number = i + 1;
            var name = new string(groups[i].Name.Where(c => !"[]:*?/\\".Contains(c) && !char.IsControl(c)).ToArray()).Trim('\'');
            if (name.Length == 0) name = "Carrier";
            name = name[..Math.Min(name.Length, 25)];
            if (!usedNames.Add(name)) { name += $" {number}"; usedNames.Add(name); }
            sheets.Add(new XElement(S + "sheet", new XAttribute("name", name), new XAttribute("sheetId", number), new XAttribute(R + "id", $"rId{number}")));
            relationships.Add(new XElement(rel + "Relationship", new XAttribute("Id", $"rId{number}"),
                new XAttribute("Type", R.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{number}.xml")));
            contentTypes.Add(new XElement(types + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{number}.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            var data = new XElement(S + "sheetData");
            data.Add(Row(1, [schedule.Document.Title, schedule.ScheduleNo, schedule.RevisionLabel, schedule.Status, schedule.Document.Recipient, schedule.Period]));
            data.Add(Row(2, ["Recipient", schedule.Document.Recipient, "Carrier", groups[i].Name, "Remark", schedule.Document.Remark]));
            data.Add(Row(3, Fields.Select(x => (object?)x.Label)));
            var lineNumber = 4;
            foreach (var line in groups[i].Lines)
                data.Add(Row(lineNumber++, Fields.Select(field => typeof(SIExportLine).GetProperty(field.Property)!.GetValue(line))));
            Entry(zip, $"xl/worksheets/sheet{number}.xml", new XElement(S + "worksheet",
                new XElement(S + "sheetViews", new XElement(S + "sheetView", new XAttribute("workbookViewId", 0),
                    new XElement(S + "pane", new XAttribute("ySplit", 3), new XAttribute("topLeftCell", "A4"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
                new XElement(S + "cols", new XElement(S + "col", new XAttribute("min", 1), new XAttribute("max", Fields.Length), new XAttribute("width", 22), new XAttribute("customWidth", 1))),
                data, new XElement(S + "autoFilter", new XAttribute("ref", $"A3:{ColumnName(Fields.Length)}{lineNumber - 1}")),
                new XElement(S + "pageMargins", new XAttribute("left", ".3"), new XAttribute("right", ".3"), new XAttribute("top", ".5"), new XAttribute("bottom", ".5"), new XAttribute("header", ".2"), new XAttribute("footer", ".2")),
                new XElement(S + "pageSetup", new XAttribute("paperSize", 9), new XAttribute("orientation", "landscape"))));
        }
        Entry(zip, "[Content_Types].xml", contentTypes);
        Entry(zip, "xl/_rels/workbook.xml.rels", relationships);
        Entry(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", R), sheets));
    }

    private static string ColumnName(int index)
    {
        var result = "";
        while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; }
        return result;
    }

    private static XElement Row(int number, IEnumerable<object?> values)
    {
        var row = new XElement(S + "row", new XAttribute("r", number));
        var column = 0;
        foreach (var value in values)
        {
            var cell = new XElement(S + "c", new XAttribute("r", ColumnName(++column) + number));
            if (value is decimal or int)
                cell.Add(new XElement(S + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
            else
            {
                var text = value is DateOnly date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                text = new string(text.Where(c => System.Xml.XmlConvert.IsXmlChar(c)).ToArray());
                cell.Add(new XAttribute("t", "inlineStr"), new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
            }
            row.Add(cell);
        }
        return row;
    }

    private static void Entry(ZipArchive zip, string path, XElement root)
    {
        using var stream = zip.CreateEntry(path).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream);
    }
}

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace YTRC.ERP.Wpf;

public static class MarkingPreparationExcelExportService
{
    public static void ExportToXlsx(JsonElement report, string monthLabel, string filePath)
    {
        // Delete if already exists
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, false);

        // 1. [Content_Types].xml
        WriteEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """);

        // 2. _rels/.rels
        WriteEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);

        // 3. xl/_rels/workbook.xml.rels
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);

        // 4. xl/workbook.xml
        WriteEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Marking Preparation" sheetId="1" r:id="rId1"/>
              </sheets>
            </workbook>
            """);

        // 5. xl/styles.xml
        WriteEntry(archive, "xl/styles.xml", BuildStylesXml());

        // 6. xl/worksheets/sheet1.xml
        WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(report, monthLabel));
    }

    public static void ExportToCsv(JsonElement report, string filePath)
    {
        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.AppendLine("No,Month,Joint Contract Name,Code,Destination,Grade,Unit,Gross (MT),Net (MT),Package,Marking Due Date,Sign,Date,ETD Date");

        var details = GetJsonArray(report, "details");
        var index = 0;
        foreach (var detail in details.EnumerateArray())
        {
            index++;
            var values = new[]
            {
                index.ToString(CultureInfo.InvariantCulture),
                ReadString(detail, "monthLabel"),
                ReadString(detail, "jointContractName"),
                MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "code")),
                MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "destination")),
                ReadString(detail, "grade"),
                ReadString(detail, "unit"),
                ReadDecimalString(detail, "grossWeight"),
                ReadDecimalString(detail, "netWeight"),
                ReadString(detail, "packageType"),
                FormatDate(detail, "markingDueDate", "dd-MMM-yyyy"),
                ReadString(detail, "markingSign"),
                FormatDate(detail, "statusDate", "dd/MM/yyyy"),
                FormatDate(detail, "etdDate", "dd/MM/yyyy")
            };
            sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(true));
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    private static string BuildStylesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <numFmts count="1">
            <numFmt numFmtId="164" formatCode="#,##0"/>
          </numFmts>
          <fonts count="7">
            <!-- 0: Regular Arial 10pt -->
            <font><sz val="10"/><name val="Arial"/></font>
            <!-- 1: Bold Arial 10pt -->
            <font><b/><sz val="10"/><name val="Arial"/></font>
            <!-- 2: Title Bold Arial 14pt (Teal) -->
            <font><b/><sz val="14"/><color rgb="FF0F766E"/><name val="Arial"/></font>
            <!-- 3: Header Bold Arial 10pt (White) -->
            <font><b/><sz val="10"/><color rgb="FFFFFFFF"/><name val="Arial"/></font>
            <!-- 4: Gray Arial 9pt -->
            <font><sz val="9"/><color rgb="FF64748B"/><name val="Arial"/></font>
            <!-- 5: Purple Bold Grade -->
            <font><b/><sz val="10"/><color rgb="FF581C87"/><name val="Arial"/></font>
            <!-- 6: Pink Bold Grade -->
            <font><b/><sz val="10"/><color rgb="FF9D174D"/><name val="Arial"/></font>
          </fonts>
          <fills count="7">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <!-- 2: Header Teal #0F766E -->
            <fill><patternFill patternType="solid"><fgColor rgb="FF0F766E"/></patternFill></fill>
            <!-- 3: Purple Grade #E9D5FF -->
            <fill><patternFill patternType="solid"><fgColor rgb="FFE9D5FF"/></patternFill></fill>
            <!-- 4: Green Grade #BBF7D0 -->
            <fill><patternFill patternType="solid"><fgColor rgb="FFBBF7D0"/></patternFill></fill>
            <!-- 5: Pink Grade #FBCFE8 -->
            <fill><patternFill patternType="solid"><fgColor rgb="FFFBCFE8"/></patternFill></fill>
            <!-- 6: Blue Grade #BAE6FD -->
            <fill><patternFill patternType="solid"><fgColor rgb="FFBAE6FD"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <!-- 1: Light gray border -->
            <border>
              <left style="thin"><color rgb="FFCBD5E1"/></left>
              <right style="thin"><color rgb="FFCBD5E1"/></right>
              <top style="thin"><color rgb="FFCBD5E1"/></top>
              <bottom style="thin"><color rgb="FFCBD5E1"/></bottom>
              <diagonal/>
            </border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
          </cellStyleXfs>
          <cellXfs count="14">
            <!-- 0: Default -->
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <!-- 1: Title -->
            <xf numFmtId="0" fontId="2" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <!-- 2: Header cell (Teal, White, Bold, Centered) -->
            <xf numFmtId="0" fontId="3" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center" wrapText="1"/>
            </xf>
            <!-- 3: Normal Left -->
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1">
              <alignment horizontal="left" vertical="center"/>
            </xf>
            <!-- 4: Normal Center -->
            <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 5: Number Right (#,##0) -->
            <xf numFmtId="164" fontId="0" fillId="0" borderId="1" xfId="0" applyNumberFormat="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center"/>
            </xf>
            <!-- 6: Grade Purple -->
            <xf numFmtId="0" fontId="5" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 7: Grade Green -->
            <xf numFmtId="0" fontId="1" fillId="4" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 8: Grade Pink -->
            <xf numFmtId="0" fontId="6" fillId="5" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 9: Grade Blue -->
            <xf numFmtId="0" fontId="1" fillId="6" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 10: Total label (Bold, Center, Border) -->
            <xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center"/>
            </xf>
            <!-- 11: Total number (Bold, Right, Number, Border) -->
            <xf numFmtId="164" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyNumberFormat="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="right" vertical="center"/>
            </xf>
            <!-- 12: Sub-info label (Bold) -->
            <xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/>
            <!-- 13: Sub-info muted (Gray) -->
            <xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1"/>
          </cellXfs>
        </styleSheet>
        """;

    private static string BuildWorksheetXml(JsonElement report, string monthLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");

        // Column widths
        sb.AppendLine("""
          <cols>
            <col min="1" max="1" width="6" customWidth="1"/>
            <col min="2" max="2" width="12" customWidth="1"/>
            <col min="3" max="3" width="30" customWidth="1"/>
            <col min="4" max="4" width="8" customWidth="1"/>
            <col min="5" max="5" width="22" customWidth="1"/>
            <col min="6" max="6" width="12" customWidth="1"/>
            <col min="7" max="7" width="10" customWidth="1"/>
            <col min="8" max="8" width="14" customWidth="1"/>
            <col min="9" max="9" width="14" customWidth="1"/>
            <col min="10" max="10" width="12" customWidth="1"/>
            <col min="11" max="11" width="16" customWidth="1"/>
            <col min="12" max="12" width="12" customWidth="1"/>
            <col min="13" max="13" width="14" customWidth="1"/>
            <col min="14" max="14" width="14" customWidth="1"/>
          </cols>
          <sheetData>
        """);

        var row = 1;

        // Title
        sb.AppendLine($"""    <row r="{row}" ht="28" customHeight="1">""");
        sb.AppendLine($"""      <c r="A{row}" s="1" t="inlineStr"><is><t>Marking Preparation Report</t></is></c>""");
        sb.AppendLine("""    </row>""");
        row++;

        // Metadata rows
        var creator = ReadString(report, "createdBy");
        var issueBy = ReadString(report, "issueBy");
        var issueDate = FormatDate(report, "issueDate", "dd/MM/yyyy");

        sb.AppendLine($"""    <row r="{row}" ht="18" customHeight="1">""");
        sb.AppendLine($"""      <c r="A{row}" s="12" t="inlineStr"><is><t>Report Month:</t></is></c>""");
        sb.AppendLine($"""      <c r="B{row}" s="12" t="inlineStr"><is><t>{EscapeXml(monthLabel)}</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="13" t="inlineStr"><is><t>Created By:</t></is></c>""");
        sb.AppendLine($"""      <c r="E{row}" s="0" t="inlineStr"><is><t>{EscapeXml(creator)}</t></is></c>""");
        sb.AppendLine("""    </row>""");
        row++;

        sb.AppendLine($"""    <row r="{row}" ht="18" customHeight="1">""");
        sb.AppendLine($"""      <c r="A{row}" s="13" t="inlineStr"><is><t>Exported Date:</t></is></c>""");
        sb.AppendLine($"""      <c r="B{row}" s="0" t="inlineStr"><is><t>{DateTime.Now:dd/MM/yyyy HH:mm}</t></is></c>""");
        if (!string.IsNullOrWhiteSpace(issueBy))
        {
            sb.AppendLine($"""      <c r="D{row}" s="13" t="inlineStr"><is><t>Issue By:</t></is></c>""");
            sb.AppendLine($"""      <c r="E{row}" s="0" t="inlineStr"><is><t>{EscapeXml(issueBy)} ({issueDate})</t></is></c>""");
        }
        sb.AppendLine("""    </row>""");
        row += 2; // Blank row

        // Revisions table if any
        var revisions = GetJsonArray(report, "revisions");
        if (revisions.ValueKind == JsonValueKind.Array && revisions.GetArrayLength() > 0)
        {
            sb.AppendLine($"""    <row r="{row}" ht="20" customHeight="1">""");
            sb.AppendLine($"""      <c r="A{row}" s="12" t="inlineStr"><is><t>Revision History</t></is></c>""");
            sb.AppendLine("""    </row>""");
            row++;

            sb.AppendLine($"""    <row r="{row}" ht="22" customHeight="1">""");
            sb.AppendLine($"""      <c r="A{row}" s="2" t="inlineStr"><is><t>Revision</t></is></c>""");
            sb.AppendLine($"""      <c r="B{row}" s="2" t="inlineStr"><is><t>Date Report</t></is></c>""");
            sb.AppendLine($"""      <c r="C{row}" s="2" t="inlineStr"><is><t>Remark</t></is></c>""");
            sb.AppendLine("""    </row>""");
            row++;

            foreach (var rev in revisions.EnumerateArray())
            {
                var revNo = ReadString(rev, "revision");
                var revDate = FormatDate(rev, "reportDate", "dd/MM/yyyy");
                var revRemark = ReadString(rev, "remark");

                sb.AppendLine($"""    <row r="{row}" ht="19" customHeight="1">""");
                sb.AppendLine($"""      <c r="A{row}" s="4" t="inlineStr"><is><t>{EscapeXml(revNo)}</t></is></c>""");
                sb.AppendLine($"""      <c r="B{row}" s="4" t="inlineStr"><is><t>{EscapeXml(revDate)}</t></is></c>""");
                sb.AppendLine($"""      <c r="C{row}" s="3" t="inlineStr"><is><t>{EscapeXml(revRemark)}</t></is></c>""");
                sb.AppendLine("""    </row>""");
                row++;
            }
            row++; // Blank row
        }

        // Main Table Headers
        sb.AppendLine($"""    <row r="{row}" ht="24" customHeight="1">""");
        sb.AppendLine($"""      <c r="A{row}" s="2" t="inlineStr"><is><t>No</t></is></c>""");
        sb.AppendLine($"""      <c r="B{row}" s="2" t="inlineStr"><is><t>Month</t></is></c>""");
        sb.AppendLine($"""      <c r="C{row}" s="2" t="inlineStr"><is><t>Joint Contract Name</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="2" t="inlineStr"><is><t>Code</t></is></c>""");
        sb.AppendLine($"""      <c r="E{row}" s="2" t="inlineStr"><is><t>Destination</t></is></c>""");
        sb.AppendLine($"""      <c r="F{row}" s="2" t="inlineStr"><is><t>Grade</t></is></c>""");
        sb.AppendLine($"""      <c r="G{row}" s="2" t="inlineStr"><is><t>Unit</t></is></c>""");
        sb.AppendLine($"""      <c r="H{row}" s="2" t="inlineStr"><is><t>Gross (MT)</t></is></c>""");
        sb.AppendLine($"""      <c r="I{row}" s="2" t="inlineStr"><is><t>Net (MT)</t></is></c>""");
        sb.AppendLine($"""      <c r="J{row}" s="2" t="inlineStr"><is><t>Package</t></is></c>""");
        sb.AppendLine($"""      <c r="K{row}" s="2" t="inlineStr"><is><t>Marking Due Date</t></is></c>""");
        sb.AppendLine($"""      <c r="L{row}" s="2" t="inlineStr"><is><t>Sign</t></is></c>""");
        sb.AppendLine($"""      <c r="M{row}" s="2" t="inlineStr"><is><t>Date</t></is></c>""");
        sb.AppendLine($"""      <c r="N{row}" s="2" t="inlineStr"><is><t>ETD Date</t></is></c>""");
        sb.AppendLine("""    </row>""");
        row++;

        // Detail Rows
        var details = GetJsonArray(report, "details");
        var itemIndex = 0;
        decimal totalGross = 0;
        decimal totalNet = 0;
        int totalUnits = 0;

        foreach (var detail in details.EnumerateArray())
        {
            itemIndex++;
            var mMonth = ReadString(detail, "monthLabel");
            var contract = ReadString(detail, "jointContractName");
            var code = MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "code"));
            var dest = MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "destination"));
            var grade = ReadString(detail, "grade");
            var unitStr = ReadString(detail, "unit");
            var package = ReadString(detail, "packageType");
            var dueDate = FormatDate(detail, "markingDueDate", "dd-MMM-yyyy");
            var sign = ReadString(detail, "markingSign");
            var statusDate = FormatDate(detail, "statusDate", "dd/MM/yyyy");
            var etdDate = FormatDate(detail, "etdDate", "dd/MM/yyyy");

            var gross = ReadDecimal(detail, "grossWeight");
            var net = ReadDecimal(detail, "netWeight");
            if (gross.HasValue) totalGross += gross.Value;
            if (net.HasValue) totalNet += net.Value;
            if (int.TryParse(unitStr, out var u)) totalUnits += u;

            // Pick style for Grade
            var gradeStyle = GetGradeStyleIndex(grade);

            sb.AppendLine($"""    <row r="{row}" ht="20" customHeight="1">""");
            sb.AppendLine($"""      <c r="A{row}" s="4"><v>{itemIndex}</v></c>""");
            sb.AppendLine($"""      <c r="B{row}" s="4" t="inlineStr"><is><t>{EscapeXml(mMonth)}</t></is></c>""");
            sb.AppendLine($"""      <c r="C{row}" s="3" t="inlineStr"><is><t>{EscapeXml(contract)}</t></is></c>""");
            sb.AppendLine($"""      <c r="D{row}" s="4" t="inlineStr"><is><t>{EscapeXml(code)}</t></is></c>""");
            sb.AppendLine($"""      <c r="E{row}" s="3" t="inlineStr"><is><t>{EscapeXml(dest)}</t></is></c>""");
            sb.AppendLine($"""      <c r="F{row}" s="{gradeStyle}" t="inlineStr"><is><t>{EscapeXml(grade)}</t></is></c>""");

            if (int.TryParse(unitStr, out var unitVal))
                sb.AppendLine($"""      <c r="G{row}" s="5"><v>{unitVal}</v></c>""");
            else
                sb.AppendLine($"""      <c r="G{row}" s="4" t="inlineStr"><is><t>{EscapeXml(unitStr)}</t></is></c>""");

            if (gross.HasValue)
                sb.AppendLine($"""      <c r="H{row}" s="5"><v>{gross.Value.ToString(CultureInfo.InvariantCulture)}</v></c>""");
            else
                sb.AppendLine($"""      <c r="H{row}" s="3"/>""");

            if (net.HasValue)
                sb.AppendLine($"""      <c r="I{row}" s="5"><v>{net.Value.ToString(CultureInfo.InvariantCulture)}</v></c>""");
            else
                sb.AppendLine($"""      <c r="I{row}" s="3"/>""");

            sb.AppendLine($"""      <c r="J{row}" s="4" t="inlineStr"><is><t>{EscapeXml(package)}</t></is></c>""");
            sb.AppendLine($"""      <c r="K{row}" s="4" t="inlineStr"><is><t>{EscapeXml(dueDate)}</t></is></c>""");
            sb.AppendLine($"""      <c r="L{row}" s="4" t="inlineStr"><is><t>{EscapeXml(sign)}</t></is></c>""");
            sb.AppendLine($"""      <c r="M{row}" s="4" t="inlineStr"><is><t>{EscapeXml(statusDate)}</t></is></c>""");
            sb.AppendLine($"""      <c r="N{row}" s="4" t="inlineStr"><is><t>{EscapeXml(etdDate)}</t></is></c>""");
            sb.AppendLine("""    </row>""");
            row++;
        }

        // Summary Total Row
        sb.AppendLine($"""    <row r="{row}" ht="22" customHeight="1">""");
        sb.AppendLine($"""      <c r="A{row}" s="10" t="inlineStr"><is><t>Total</t></is></c>""");
        sb.AppendLine($"""      <c r="B{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="C{row}" s="10" t="inlineStr"><is><t>{itemIndex} item(s)</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="E{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="F{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="G{row}" s="11"><v>{totalUnits}</v></c>""");
        sb.AppendLine($"""      <c r="H{row}" s="11"><v>{totalGross.ToString(CultureInfo.InvariantCulture)}</v></c>""");
        sb.AppendLine($"""      <c r="I{row}" s="11"><v>{totalNet.ToString(CultureInfo.InvariantCulture)}</v></c>""");
        sb.AppendLine($"""      <c r="J{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="K{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="L{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="M{row}" s="10"/>""");
        sb.AppendLine($"""      <c r="N{row}" s="10"/>""");
        sb.AppendLine("""    </row>""");
        row += 2;

        // Footer Approvals
        var prodSign = ReadString(report, "productionAdminSign");
        var prodDate = FormatDate(report, "productionAdminDate", "dd/MM/yyyy");
        var qacSign = ReadString(report, "qacSign");
        var qacDate = FormatDate(report, "qacDate", "dd/MM/yyyy");
        var whSign = ReadString(report, "warehouseSign");
        var whDate = FormatDate(report, "warehouseDate", "dd/MM/yyyy");

        sb.AppendLine($"""    <row r="{row}" ht="18" customHeight="1">""");
        sb.AppendLine($"""      <c r="C{row}" s="12" t="inlineStr"><is><t>Production Admin:</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="0" t="inlineStr"><is><t>{EscapeXml(prodSign)}</t></is></c>""");
        sb.AppendLine($"""      <c r="F{row}" s="12" t="inlineStr"><is><t>Date:</t></is></c>""");
        sb.AppendLine($"""      <c r="G{row}" s="0" t="inlineStr"><is><t>{EscapeXml(prodDate)}</t></is></c>""");
        sb.AppendLine("""    </row>""");
        row++;

        sb.AppendLine($"""    <row r="{row}" ht="18" customHeight="1">""");
        sb.AppendLine($"""      <c r="C{row}" s="12" t="inlineStr"><is><t>QAC:</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="0" t="inlineStr"><is><t>{EscapeXml(qacSign)}</t></is></c>""");
        sb.AppendLine($"""      <c r="F{row}" s="12" t="inlineStr"><is><t>Date:</t></is></c>""");
        sb.AppendLine($"""      <c r="G{row}" s="0" t="inlineStr"><is><t>{EscapeXml(qacDate)}</t></is></c>""");
        sb.AppendLine("""    </row>""");
        row++;

        sb.AppendLine($"""    <row r="{row}" ht="18" customHeight="1">""");
        sb.AppendLine($"""      <c r="C{row}" s="12" t="inlineStr"><is><t>W/H:</t></is></c>""");
        sb.AppendLine($"""      <c r="D{row}" s="0" t="inlineStr"><is><t>{EscapeXml(whSign)}</t></is></c>""");
        sb.AppendLine($"""      <c r="F{row}" s="12" t="inlineStr"><is><t>Date:</t></is></c>""");
        sb.AppendLine($"""      <c r="G{row}" s="0" t="inlineStr"><is><t>{EscapeXml(whDate)}</t></is></c>""");
        sb.AppendLine("""    </row>""");

        sb.AppendLine("""  </sheetData>""");
        sb.AppendLine("""</worksheet>""");

        return sb.ToString();
    }

    private static int GetGradeStyleIndex(string? grade)
    {
        var g = grade?.Trim().ToUpperInvariant() ?? "";
        if (g.Contains("0241") || g.Contains("241")) return 6; // Purple
        if (g.Contains("0243") || g.Contains("243")) return 7; // Green
        if (g.Contains("0251") || g.Contains("251")) return 8; // Pink
        if (g.Contains("0263") || g.Contains("263")) return 9; // Blue
        return 4; // Normal center
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("&", "&amp;")
                    .Replace("<", "&lt;")
                    .Replace(">", "&gt;")
                    .Replace("\"", "&quot;")
                    .Replace("'", "&apos;");
    }

    private static JsonElement GetJsonArray(JsonElement root, string property)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.Array)
            return val;
        return default;
    }

    private static string ReadString(JsonElement item, string property)
    {
        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var val))
        {
            if (val.ValueKind == JsonValueKind.String) return val.GetString()?.Trim() ?? "";
            if (val.ValueKind == JsonValueKind.Number) return val.ToString();
        }
        return "";
    }

    private static decimal? ReadDecimal(JsonElement item, string property)
    {
        string[] candidates = property switch
        {
            "grossWeight" => ["grossWeight", "gross_weight", "GrossWeight", "gross", "Gross"],
            "netWeight" => ["netWeight", "net_weight", "NetWeight", "net", "Net"],
            _ => [property]
        };

        foreach (var key in candidates)
        {
            if (!item.TryGetProperty(key, out var val)) continue;
            if (val.ValueKind == JsonValueKind.Number && val.TryGetDecimal(out var d)) return d;
            if (val.ValueKind == JsonValueKind.String && decimal.TryParse(val.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p;
        }
        return null;
    }

    private static string ReadDecimalString(JsonElement item, string property)
    {
        var val = ReadDecimal(item, property);
        return val.HasValue ? val.Value.ToString("N0", CultureInfo.InvariantCulture) : "";
    }

    private static string FormatDate(JsonElement item, string property, string format)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(property, out var val)) return "";
        if (val.ValueKind == JsonValueKind.String && DateTime.TryParse(val.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString(format, CultureInfo.InvariantCulture);
        return "";
    }
}

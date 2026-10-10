using System.Globalization;
using System.Text.Json;
using System.Windows.Documents;
using System.Printing;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const string MarkingPreparationRoute = "/api/marking-preparations";
    private static readonly JsonElement EmptyMarkingPreparationArray = JsonDocument.Parse("[]").RootElement;
    private List<MarkingPreparationListItem> _markingPreparationReports = [];
    private DataGrid? _markingPreparationReportGrid;
    private DataGrid? _markingPreparationDetailGrid;
    private TextBox? _markingPreparationSearch;
    private TextBlock? _markingPreparationMessage;
    private bool _markingPreparationLoading;

    private sealed record MarkingPreparationListItem(
        JsonElement Raw, string Id, DateTime ReportMonth, int RevisionCount, int DetailCount)
    {
        public string MonthLabel => ReportMonth.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    }

    private sealed record MarkingPreparationDetailListItem(
        int No, string Month, string Contract, string Code, string Destination, string Grade,
        string Unit, string Gross, string Net, string Package, string DueDate, string Sign, string Date, string EtdDate);

    private UIElement BuildMarkingPreparationPage()
    {
        var page = PageStack();
        page.Children.Add(TitleBlock("Marking Preparation Report", "Maintain monthly marking schedules, revisions and print-ready A4 reports."));

        var toolbar = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 8) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var refresh = MarkingPreparationButton("Refresh");
        refresh.Click += async (_, _) => await LoadMarkingPreparationReportsAsync();
        var create = MarkingPreparationButton("New report", primary: true);
        create.Click += async (_, _) => await EditMarkingPreparationAsync(null);
        var edit = MarkingPreparationButton("Edit selected");
        edit.Click += async (_, _) => await EditMarkingPreparationAsync(SelectedMarkingPreparation());
        edit.IsEnabled = SelectedMarkingPreparation() is not null && !_session.IsPreview;
        var delete = MarkingPreparationButton("Delete selected");
        delete.Click += async (_, _) => await DeleteMarkingPreparationAsync();
        delete.IsEnabled = SelectedMarkingPreparation() is not null && !_session.IsPreview;
        var print = MarkingPreparationButton("Print A4");
        print.Click += (_, _) =>
        {
            if (SelectedMarkingPreparation() is { } report) PrintMarkingPreparation(report);
        };
        print.IsEnabled = SelectedMarkingPreparation() is not null;
        actions.Children.Add(refresh);
        actions.Children.Add(create);
        actions.Children.Add(edit);
        actions.Children.Add(delete);
        actions.Children.Add(print);
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);

        _markingPreparationSearch = new TextBox
        {
            Width = 280,
            Height = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "Search report month"
        };
        _markingPreparationSearch.TextChanged += (_, _) => RefreshMarkingPreparationReportGrid();
        toolbar.Children.Add(_markingPreparationSearch);
        page.Children.Add(toolbar);

        _markingPreparationMessage = Label(
            _markingPreparationLoading ? "Loading marking preparation reports…" :
            _session.IsPreview ? "Preview mode · reports are read-only." : "",
            12, (Brush)FindResource("MutedBrush"));
        _markingPreparationMessage.Margin = new Thickness(0, 0, 0, 6);
        page.Children.Add(_markingPreparationMessage);

        _markingPreparationReportGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            Height = 176,
            Margin = new Thickness(0, 0, 0, 8),
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(247, 249, 252))
        };
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Report month", nameof(MarkingPreparationListItem.MonthLabel), 1.1);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Revisions", nameof(MarkingPreparationListItem.RevisionCount), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Details", nameof(MarkingPreparationListItem.DetailCount), .65, centered: true);
        _markingPreparationReportGrid.SelectionChanged += (_, _) =>
        {
            RefreshMarkingPreparationDetailGrid();
            UpdateMarkingPreparationButtons(actions);
        };
        _markingPreparationReportGrid.MouseDoubleClick += async (_, _) =>
        {
            if (SelectedMarkingPreparation() is { } report) await EditMarkingPreparationAsync(report);
        };
        page.Children.Add(_markingPreparationReportGrid);

        page.Children.Add(SectionHeading("Report details"));
        _markingPreparationDetailGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(247, 249, 252)),
            MinHeight = 260
        };
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "No", nameof(MarkingPreparationDetailListItem.No), .38, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Month", nameof(MarkingPreparationDetailListItem.Month), .7);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Joint contract", nameof(MarkingPreparationDetailListItem.Contract), 1.7);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Code", nameof(MarkingPreparationDetailListItem.Code), .6);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Destination", nameof(MarkingPreparationDetailListItem.Destination), 1.05);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Grade", nameof(MarkingPreparationDetailListItem.Grade), .65);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Unit", nameof(MarkingPreparationDetailListItem.Unit), .42, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Gross", nameof(MarkingPreparationDetailListItem.Gross), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Net", nameof(MarkingPreparationDetailListItem.Net), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Package", nameof(MarkingPreparationDetailListItem.Package), .65);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Marking due", nameof(MarkingPreparationDetailListItem.DueDate), .78, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Sign", nameof(MarkingPreparationDetailListItem.Sign), .7);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Date", nameof(MarkingPreparationDetailListItem.Date), .78, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "ETD date · not printed", nameof(MarkingPreparationDetailListItem.EtdDate), .9, centered: true);
        page.Children.Add(_markingPreparationDetailGrid);

        RefreshMarkingPreparationReportGrid();
        RefreshMarkingPreparationDetailGrid();
        return page;
    }

    private Button MarkingPreparationButton(string title, bool primary = false) => new()
    {
        Content = title,
        MinWidth = title == "Print A4" ? 94 : 100,
        Height = 34,
        Margin = new Thickness(0, 0, 8, 0),
        Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button)),
        IsEnabled = !_session.IsPreview || title is "Refresh" or "Print A4"
    };

    private static void AddMarkingPreparationColumn(DataGrid grid, string header, string binding,
        double widthWeight, bool centered = false)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(4, 1, 4, 1)));
        if (centered) style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(binding),
            Width = new DataGridLength(widthWeight, DataGridLengthUnitType.Star),
            ElementStyle = style
        });
    }

    private async Task LoadMarkingPreparationReportsAsync()
    {
        if (_session.IsPreview)
        {
            _markingPreparationReports = [];
            RefreshMarkingPreparationReportGrid();
            RefreshMarkingPreparationDetailGrid();
            if (_markingPreparationMessage is not null) _markingPreparationMessage.Text = "Preview mode · connect with an account to load reports.";
            return;
        }

        _markingPreparationLoading = true;
        if (_markingPreparationMessage is not null) _markingPreparationMessage.Text = "Loading marking preparation reports…";
        var result = await _apiClient.GetAsync(MarkingPreparationRoute, _session.Token);
        _markingPreparationLoading = false;
        if (!result.Success || result.Data is not { ValueKind: JsonValueKind.Array } data)
        {
            if (_markingPreparationMessage is not null)
                _markingPreparationMessage.Text = string.IsNullOrWhiteSpace(result.Error)
                    ? "Could not load marking preparation reports."
                    : result.Error;
            return;
        }

        _markingPreparationReports = data.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => new MarkingPreparationListItem(
                item.Clone(),
                ReadString(item, "id"),
                ReadDate(item, "reportMonth") ?? DateTime.Today,
                ReadJsonArray(item, "revisions").GetArrayLength(),
                ReadJsonArray(item, "details").GetArrayLength()))
            .OrderByDescending(item => item.ReportMonth)
            .ToList();
        if (_markingPreparationMessage is not null)
            _markingPreparationMessage.Text = $"{_markingPreparationReports.Count} report(s) · Select a month to review or print.";
        RefreshMarkingPreparationReportGrid();
        RefreshMarkingPreparationDetailGrid();
    }

    private static string ReadString(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString() : "";

    private static DateTime? ReadDate(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String &&
           DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.Date : null;

    private static JsonElement ReadJsonArray(JsonElement item, string property)
        => item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value : EmptyMarkingPreparationArray;

    private MarkingPreparationListItem? SelectedMarkingPreparation()
        => _markingPreparationReportGrid?.SelectedItem as MarkingPreparationListItem;

    private void RefreshMarkingPreparationReportGrid()
    {
        if (_markingPreparationReportGrid is null) return;
        var selectedId = SelectedMarkingPreparation()?.Id;
        var search = _markingPreparationSearch?.Text.Trim() ?? "";
        _markingPreparationReportGrid.ItemsSource = _markingPreparationReports
            .Where(item => search.Length == 0 || item.MonthLabel.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (selectedId is not null)
            _markingPreparationReportGrid.SelectedItem = _markingPreparationReportGrid.Items
                .Cast<MarkingPreparationListItem>().FirstOrDefault(item => item.Id == selectedId);
    }

    private void RefreshMarkingPreparationDetailGrid()
    {
        if (_markingPreparationDetailGrid is null) return;
        var report = SelectedMarkingPreparation();
        if (report is null || !report.Raw.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Array)
        {
            _markingPreparationDetailGrid.ItemsSource = Array.Empty<MarkingPreparationDetailListItem>();
            return;
        }

        _markingPreparationDetailGrid.ItemsSource = details.EnumerateArray().Select((line, index) => new MarkingPreparationDetailListItem(
            index + 1,
            ReadString(line, "monthLabel"),
            ReadString(line, "jointContractName"),
            ReadString(line, "code"),
            ReadString(line, "destination"),
            ReadString(line, "grade"),
            ReadString(line, "unit"),
            FormatWeight(line, "grossWeight"),
            FormatWeight(line, "netWeight"),
            ReadString(line, "packageType"),
            FormatDate(line, "markingDueDate"),
            ReadString(line, "markingSign"),
            FormatDate(line, "statusDate"),
            FormatDate(line, "etdDate"))).ToList();
    }

    private static string FormatWeight(JsonElement line, string property)
        => line.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)
            ? number.ToString(number == decimal.Truncate(number) ? "N0" : "N3", CultureInfo.InvariantCulture) : "";

    private static string FormatDate(JsonElement item, string property)
        => ReadDate(item, property)?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";

    private void UpdateMarkingPreparationButtons(Panel actions)
    {
        var selected = actions.Children.OfType<Button>().Skip(2).Take(3).ToArray();
        if (selected.Length == 3)
        {
            var enabled = SelectedMarkingPreparation() is not null;
            selected[0].IsEnabled = enabled && !_session.IsPreview;
            selected[1].IsEnabled = enabled && !_session.IsPreview;
            selected[2].IsEnabled = enabled;
        }
    }

    private async Task EditMarkingPreparationAsync(MarkingPreparationListItem? report)
    {
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Connect with an account to create or edit reports.", "Marking Preparation",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new MarkingPreparationEditorWindow(report?.Raw);
        editor.Owner = this;
        if (editor.ShowDialog() != true || editor.Payload is null) return;
        var result = report is null
            ? await _apiClient.PostJsonAsync(MarkingPreparationRoute, editor.Payload, _session.Token)
            : await _apiClient.PutJsonAsync($"{MarkingPreparationRoute}/{Uri.EscapeDataString(report.Id)}", editor.Payload, _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, string.IsNullOrWhiteSpace(result.Error) ? "Could not save the report." : result.Error,
                "Marking Preparation", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        await LoadMarkingPreparationReportsAsync();
        if (result.Data is { ValueKind: JsonValueKind.Object } saved && _markingPreparationReportGrid is not null)
        {
            var id = ReadString(saved, "id");
            _markingPreparationReportGrid.SelectedItem = _markingPreparationReportGrid.Items
                .Cast<MarkingPreparationListItem>().FirstOrDefault(item => item.Id == id);
        }
    }

    private async Task DeleteMarkingPreparationAsync()
    {
        var report = SelectedMarkingPreparation();
        if (report is null || _session.IsPreview) return;
        var answer = MessageBox.Show(this,
            $"Delete the {report.MonthLabel} report and all of its revisions and detail rows?",
            "Delete marking report", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        var result = await _apiClient.DeleteAsync($"{MarkingPreparationRoute}/{Uri.EscapeDataString(report.Id)}", _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, string.IsNullOrWhiteSpace(result.Error) ? "Could not delete the report." : result.Error,
                "Marking Preparation", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        await LoadMarkingPreparationReportsAsync();
    }

    private void PrintMarkingPreparation(MarkingPreparationListItem report)
    {
        var dialog = new PrintDialog();
        try
        {
            dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            dialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        }
        catch (ArgumentException) { }
        if (dialog.ShowDialog() != true) return;

        const double pageWidth = 793.7;
        const double pageHeight = 1122.5;
        var document = new FlowDocument
        {
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            PagePadding = new Thickness(26),
            ColumnWidth = pageWidth - 52,
            FontFamily = new System.Windows.Media.FontFamily("Arial"),
            FontSize = 7.2,
            IsColumnWidthFlexible = false
        };
        document.Blocks.Add(new Paragraph(new Run("Marking Preparation Report"))
        {
            TextAlignment = TextAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 9)
        });
        document.Blocks.Add(BuildMarkingPreparationPrintHeader(report.Raw));
        document.Blocks.Add(BuildMarkingPreparationPrintTable(report.Raw));
        document.Blocks.Add(BuildMarkingPreparationPrintFooter(report.Raw));

        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator,
            $"Marking Preparation {report.MonthLabel}");
    }

    private static Table BuildMarkingPreparationPrintHeader(JsonElement report)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
        table.Columns.Add(new TableColumn { Width = new GridLength(225) });
        table.Columns.Add(new TableColumn { Width = new GridLength(516) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var month = ReadDate(report, "reportMonth")?.ToString("MMM-yy", CultureInfo.InvariantCulture) ?? "";
        var left = new TableCell(new Paragraph(new Run($"Report Month :    {month}"))
        {
            Margin = new Thickness(0, 8, 4, 0),
            FontWeight = FontWeights.SemiBold,
            FontSize = 8
        });
        left.BorderThickness = new Thickness(0);
        var revisionTable = new Table { CellSpacing = 0 };
        foreach (var width in new[] { 70d, 93d, 353d }) revisionTable.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var revisions = new TableRowGroup();
        revisionTable.RowGroups.Add(revisions);
        revisions.Rows.Add(CreateMarkingPreparationPrintRow(["Revision", "Date Report", "Remark"], true));
        var values = ReadJsonArray(report, "revisions");
        foreach (var item in values.EnumerateArray())
        {
            revisions.Rows.Add(CreateMarkingPreparationPrintRow([
                ReadString(item, "revision"), FormatDate(item, "reportDate"), ReadString(item, "remark")
            ], false));
        }
        if (values.GetArrayLength() == 0)
            revisions.Rows.Add(CreateMarkingPreparationPrintRow(["", "", ""], false));
        var right = new TableCell { Padding = new Thickness(0) };
        right.Blocks.Add(revisionTable);
        right.BorderThickness = new Thickness(0);
        var row = new TableRow();
        row.Cells.Add(left);
        row.Cells.Add(right);
        group.Rows.Add(row);
        var printedBy = ReadString(report, "printedBy");
        var by = new TableRow();
        by.Cells.Add(new TableCell(new Paragraph(new Run($"User Print : {printedBy}"))
        {
            FontSize = 5.5,
            Margin = new Thickness(0, 6, 0, 0)
        }) { BorderThickness = new Thickness(0), ColumnSpan = 2 });
        group.Rows.Add(by);
        return table;
    }

    private static Table BuildMarkingPreparationPrintTable(JsonElement report)
    {
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.8) };
        var widths = new[] { 24d, 48d, 128d, 36d, 78d, 47d, 33d, 54d, 50d, 48d, 72d, 53d, 54d };
        foreach (var width in widths) table.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        var statusRow = new TableRow();
        for (var index = 0; index < 10; index++)
            statusRow.Cells.Add(CreateMarkingPreparationPrintCell("", false));
        var status = CreateMarkingPreparationPrintCell("Status", true);
        status.ColumnSpan = 3;
        statusRow.Cells.Add(status);
        group.Rows.Add(statusRow);

        var labels = new[] { "No", "Month", "Joint Contract Name", "Code", "Dest.", "Grade", "Unit", "Gross", "Net.", "Package", "Marking Due Date", "Sign", "Date" };
        var header = new TableRow();
        for (var index = 0; index < labels.Length; index++)
            header.Cells.Add(CreateMarkingPreparationPrintCell(labels[index], true));
        group.Rows.Add(header);

        var lines = ReadJsonArray(report, "details");
        var no = 0;
        foreach (var line in lines.EnumerateArray())
        {
            no++;
            group.Rows.Add(CreateMarkingPreparationPrintRow([
                no.ToString(CultureInfo.InvariantCulture), ReadString(line, "monthLabel"),
                ReadString(line, "jointContractName"), ReadString(line, "code"),
                ReadString(line, "destination"), ReadString(line, "grade"), ReadString(line, "unit"),
                FormatWeight(line, "grossWeight"), FormatWeight(line, "netWeight"),
                ReadString(line, "packageType"), FormatDate(line, "markingDueDate"),
                ReadString(line, "markingSign"), FormatDate(line, "statusDate")
            ], false));
        }
        return table;
    }

    private static Table BuildMarkingPreparationPrintFooter(JsonElement report)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 8, 0, 0) };
        table.Columns.Add(new TableColumn { Width = new GridLength(250) });
        table.Columns.Add(new TableColumn { Width = new GridLength(491) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var issue = new TableCell(new Paragraph(new Run($"Issue By : {ReadString(report, "issueBy")}\nDate : {FormatDate(report, "issueDate")}"))
        {
            FontSize = 7,
            Margin = new Thickness(2)
        });
        issue.BorderBrush = Brushes.Black;
        issue.BorderThickness = new Thickness(.5);
        var distribution = new Table { CellSpacing = 0 };
        foreach (var width in new[] { 95d, 95d, 95d, 130d }) distribution.Columns.Add(new TableColumn { Width = new GridLength(width) });
        var rows = new TableRowGroup();
        distribution.RowGroups.Add(rows);
        rows.Rows.Add(CreateMarkingPreparationPrintRow(["Distribute to", "Production Admin", "QAC", "W/H"], true));
        rows.Rows.Add(CreateMarkingPreparationPrintRow([
            "", ReadString(report, "productionAdminSign"), ReadString(report, "qacSign"), ReadString(report, "warehouseSign")
        ], false));
        rows.Rows.Add(CreateMarkingPreparationPrintRow([
            "Date", FormatDate(report, "productionAdminDate"), FormatDate(report, "qacDate"), FormatDate(report, "warehouseDate")
        ], false));
        var distributionCell = new TableCell { Padding = new Thickness(0) };
        distributionCell.Blocks.Add(distribution);
        distributionCell.BorderThickness = new Thickness(0);
        var row = new TableRow();
        row.Cells.Add(issue);
        row.Cells.Add(distributionCell);
        group.Rows.Add(row);
        return table;
    }

    private static TableRow CreateMarkingPreparationPrintRow(IReadOnlyList<string> values, bool header)
    {
        var row = new TableRow();
        foreach (var value in values) row.Cells.Add(CreateMarkingPreparationPrintCell(value, header));
        return row;
    }

    private static TableCell CreateMarkingPreparationPrintCell(string value, bool header)
    {
        var paragraph = new Paragraph(new Run(value))
        {
            Margin = new Thickness(0),
            TextAlignment = header ? TextAlignment.Center : TextAlignment.Left,
            LineHeight = 8.1
        };
        return new TableCell(paragraph)
        {
            Padding = new Thickness(1.3, 1.4, 1.3, 1.4),
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.35),
            Background = Brushes.White,
            FontSize = header ? 6.8 : 6.5,
            FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal
        };
    }
}

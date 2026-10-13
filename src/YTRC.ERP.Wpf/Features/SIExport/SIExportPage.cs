using System.Printing;
using System.Windows.Documents;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private readonly ISIExportService _siExportService;
    private List<SIExportSchedule> _siSchedules = [];
    private DataGrid? _siGrid;
    private DataGrid? _siLines;
    private TextBlock? _siMessage;
    private TextBlock? _siDetailsMessage;
    private string _siSearch = "";
    private string _siYear = "";
    private int _siMonth;
    private int _siSkip;
    private int _siTotal;
    private int _siLoadVersion;
    private bool _siLoading;
    private string? _siSelectedId;
    private readonly List<(Button Button, string Permission, bool Selection)> _siButtons = [];

    private bool SICan(string permission) => !_session.IsPreview &&
        (_session.User.Role == UserRole.SystemAdmin || _session.User.RawPermissions.Contains(permission)
            || _session.User.RawPermissions.Contains("*"));
    private SIExportSchedule? SISelected => _siGrid?.SelectedItem as SIExportSchedule;

    private UIElement BuildSIExportPage()
    {
        _siButtons.Clear();
        var root = new Grid { Margin = UiTokens.PageMargin };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto,
            new GridLength(2, GridUnitType.Star), GridLength.Auto, new GridLength(3, GridUnitType.Star) })
            root.RowDefinitions.Add(new RowDefinition { Height = height });
        root.Children.Add(new TextBlock { Text = "SI Export · Shipping Instructions", FontSize = 23,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; Grid.SetRow(tools, 1); root.Children.Add(tools);
        var search = SIExportEditorWindow.Input(_siSearch); search.Width = 210; search.Margin = new Thickness(0, 0, 8, 0);
        search.ToolTip = "ค้นหาชุด SI เลขสัญญา ผู้รับ หรือผู้ขนส่ง";
        search.TextChanged += (_, _) => _siSearch = search.Text;
        search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { _siSkip = 0; await LoadSIExportsAsync(); } };
        tools.Children.Add(search);
        var year = SIExportEditorWindow.Input(_siYear);
        year.ToolTip = "กรองตามปี ค.ศ. เช่น 2025 · เว้นว่างเพื่อแสดงทุกปี";
        year.TextChanged += (_, _) => _siYear = year.Text.Trim();
        var yearHint = new TextBlock { Text = "ปี ค.ศ.", Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0),
            IsHitTestVisible = false, Visibility = string.IsNullOrEmpty(year.Text) ? Visibility.Visible : Visibility.Collapsed };
        year.TextChanged += (_, _) => yearHint.Visibility = string.IsNullOrEmpty(year.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        var yearFilter = new Grid { Width = 90, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        yearFilter.Children.Add(year); yearFilter.Children.Add(yearHint); tools.Children.Add(yearFilter);
        var month = SIExportEditorWindow.Choice(new[] { "All months" }.Concat(Enumerable.Range(1, 12).Select(x => x.ToString("00"))),
            _siMonth == 0 ? "All months" : _siMonth.ToString("00"));
        month.Width = 120; month.Margin = new Thickness(0, 0, 8, 0);
        month.SelectionChanged += (_, _) => _siMonth = int.TryParse(month.SelectedItem?.ToString(), out var number) ? number : 0;
        tools.Children.Add(month);
        AddSIButton(tools, "ค้นหา / Refresh", "orders:read", false, async () => { _siSkip = 0; await LoadSIExportsAsync(); });
        AddSIButton(tools, "สร้าง SI", "orders:create", false, () => EditSIExportAsync(null));
        AddSIButton(tools, "แก้ไข / Tracking", "orders:update", true, () => EditSIExportAsync(SISelected));
        AddSIButton(tools, "คัดลอกชุด SI", "orders:create", true, () => EditSIExportAsync(SISelected, copy: true));
        AddSIButton(tools, "ยืนยันฉบับใช้งาน", "orders:approve", true, () => SIActionAsync(true));
        AddSIButton(tools, "ยกเลิกชุด SI", "orders:update", true, () => SIActionAsync(false));
        AddSIButton(tools, "Revision / Compare", "orders:read", true, ShowSIHistoryAsync);
        AddSIButton(tools, "Excel แยกสายเรือ", "orders:read", true, ExportSIAsync);
        AddSIButton(tools, "รายงาน / พิมพ์", "orders:read", true, PrintSIAsync);
        AddSIButton(tools, "‹ ก่อนหน้า", "orders:read", false, async () => { _siSkip = Math.Max(0, _siSkip - 50); await LoadSIExportsAsync(); });
        AddSIButton(tools, "ถัดไป ›", "orders:read", false, async () => { if (_siSkip + 50 < _siTotal) { _siSkip += 50; await LoadSIExportsAsync(); } });
        _siMessage = new TextBlock { Text = _session.IsPreview ? "Sign in to use SI Export." : "สร้างข้อมูลผ่านแอป · Draft / Forecast เติมรายละเอียดขนส่งภายหลังได้",
            Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(_siMessage, 2); root.Children.Add(_siMessage);
        _siGrid = new DataGrid { ItemsSource = _siSchedules, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column, RowHeight = 34, Background = Brushes.White };
        foreach (var (label, key, width) in new[] { ("SI No.", "ScheduleNo", 220d),
            ("ผู้รับ", "Recipient", 140d), ("เดือนส่งออก", "Period", 110d), ("Revision", "RevisionLabel", 90d),
            ("สถานะฉบับ", "Status", 130d), ("รายการ", "LineCount", 80d), ("แก้ไขล่าสุด", "UpdatedAt", 210d) })
            SIExportUi.Column(_siGrid, label, key, width);
        _siLines = SIExportUi.LineGrid(Array.Empty<SIExportLine>());
        _siDetailsMessage = new TextBlock { Text = "เลือกรายการเพื่อดูข้อมูลสัญญาและขนส่ง", Foreground = Brushes.SlateGray,
            Margin = new Thickness(0, 10, 0, 8), TextWrapping = TextWrapping.Wrap };
        _siGrid.SelectionChanged += (_, _) => { _siSelectedId = SISelected?.Id; UpdateSISelection(); };
        _siGrid.MouseDoubleClick += async (_, _) => { if (SICan("orders:update") && SISelected is { } selected) await EditSIExportAsync(selected); };
        Grid.SetRow(_siGrid, 3); root.Children.Add(_siGrid);
        Grid.SetRow(_siDetailsMessage, 4); root.Children.Add(_siDetailsMessage);
        Grid.SetRow(_siLines, 5); root.Children.Add(_siLines);
        _siGrid.SelectedItem = _siSchedules.FirstOrDefault(x => x.Id == _siSelectedId);
        UpdateSISelection();
        return root;
    }

    private void AddSIButton(Panel panel, string label, string permission, bool selection, Func<Task> action)
    {
        var button = SIExportUi.Button(label, () => { });
        button.Click += async (_, _) =>
        {
            if (!SICan(permission)) return;
            try { await action(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "SI Export", MessageBoxButton.OK, MessageBoxImage.Error); }
        };
        _siButtons.Add((button, permission, selection)); panel.Children.Add(button);
    }

    private void UpdateSISelection()
    {
        var selected = SISelected;
        if (_siLines is not null) _siLines.ItemsSource = selected?.Document.Lines ?? [];
        if (_siDetailsMessage is not null)
            _siDetailsMessage.Text = selected is null ? "เลือกรายการเพื่อดูข้อมูลสัญญาและขนส่ง"
                : $"{selected.ScheduleNo} · {selected.RevisionLabel} · {selected.Status} · " +
                    (selected.ValidationIssues.Count == 0 ? "ข้อมูล SI ครบสำหรับยืนยัน" : $"ยังขาดข้อมูล SI {selected.ValidationIssues.Count} จุด: {string.Join("; ", selected.ValidationIssues.Take(3))}");
        foreach (var (button, permission, selection) in _siButtons)
            button.IsEnabled = !_siLoading && SICan(permission) && (!selection || selected is not null);
    }

    private async Task LoadSIExportsAsync()
    {
        if (_session.IsPreview || _purchasingSection != "SI Export") return;
        var version = ++_siLoadVersion; var token = _session.Token;
        try
        {
            _siLoading = true; UpdateSISelection();
            if (_siMessage is not null) _siMessage.Text = "กำลังโหลด SI Export…";
            int? year = null;
            if (_siYear.Length > 0)
            {
                if (!int.TryParse(_siYear, out var value) || value < 2025 || value > 2100)
                    throw new ArgumentException("ปีส่งออกต้องเป็น ค.ศ. ระหว่าง 2025–2100");
                year = value;
            }
            var page = await _siExportService.ListAsync(token, _siSearch, year, _siMonth == 0 ? null : _siMonth, _siSkip);
            if (version != _siLoadVersion || _session.Token != token || _purchasingSection != "SI Export") return;
            _siSchedules = page.Items; _siTotal = page.Total;
            if (_siGrid is not null)
            {
                var id = _siSelectedId;
                _siGrid.ItemsSource = _siSchedules;
                _siGrid.SelectedItem = _siSchedules.FirstOrDefault(x => x.Id == id);
            }
            if (_siMessage is not null) _siMessage.Text = page.Total == 0 ? "ยังไม่มีข้อมูล SI — กดสร้าง SI เพื่อเริ่มบันทึก"
                : $"แสดง {_siSkip + (page.Items.Count > 0 ? 1 : 0)}–{_siSkip + page.Items.Count} จาก {page.Total} ชุด SI";
        }
        catch (Exception ex)
        {
            if (version == _siLoadVersion && _siMessage is not null) _siMessage.Text = "โหลดไม่สำเร็จ: " + ex.Message;
        }
        finally { if (version == _siLoadVersion) { _siLoading = false; UpdateSISelection(); } }
    }

    private async Task EditSIExportAsync(SIExportSchedule? source, bool copy = false)
    {
        if (!SICan(source is null || copy ? "orders:create" : "orders:update")) return;
        try
        {
            if (source is not null) source = await _siExportService.GetAsync(_session.Token, source.Id);
            if (source?.Status == "CANCELLED" && !copy) throw new InvalidOperationException("ชุด SI นี้ยกเลิกแล้ว สามารถคัดลอกเพื่อสร้างชุดใหม่ได้");
            var token = _session.Token;
            var catalog = await _siExportService.CatalogAsync(token);
            var editor = new SIExportEditorWindow(source, copy, catalog, async payload =>
            {
                var saved = await _siExportService.SaveAsync(token, source is null || copy ? null : source.Id, payload);
                _siSelectedId = saved.Id;
            }) { Owner = this };
            if (editor.ShowDialog() == true) await LoadSIExportsAsync();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "SI Export", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task SIActionAsync(bool confirm)
    {
        if (SISelected is not { } selected) return;
        var current = await _siExportService.GetAsync(_session.Token, selected.Id);
        if (current.Revision != selected.Revision)
        {
            await LoadSIExportsAsync();
            throw new InvalidOperationException("รายการถูกแก้ไขแล้ว กรุณาตรวจ revision ใหม่ก่อนยืนยันหรือยกเลิก");
        }
        if (confirm && current.ValidationIssues.Count > 0)
            throw new InvalidOperationException("กรุณาเติมข้อมูลก่อนยืนยัน:\n" + string.Join("\n", current.ValidationIssues));
        var reason = SIReasonWindow.Ask(this, confirm ? "ยืนยันฉบับใช้งาน" : "ยกเลิกชุด SI", current);
        if (reason is null) return;
        await _siExportService.ActionAsync(_session.Token, current.Id, current.Revision, reason, confirm);
        await LoadSIExportsAsync();
    }

    private async Task ShowSIHistoryAsync()
    {
        if (SISelected is not { } selected) return;
        var history = await _siExportService.RevisionsAsync(_session.Token, selected.Id);
        new SIExportHistoryWindow(_siExportService, _session.Token, selected, history) { Owner = this }.ShowDialog();
    }

    private async Task ExportSIAsync()
    {
        if (SISelected is not { } selected) return;
        var current = await _siExportService.GetAsync(_session.Token, selected.Id);
        var dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"{current.ScheduleNo}_{current.RevisionLabel}.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        SIExportExcelWriter.Write(current, dialog.FileName, separateCarriers: true);
        if (_siMessage is not null) _siMessage.Text = $"ส่งออก {current.RevisionLabel} สำเร็จ: {dialog.FileName}";
    }

    private async Task PrintSIAsync()
    {
        if (SISelected is not { } selected) return;
        var current = await _siExportService.GetAsync(_session.Token, selected.Id);
        AppPrintDialog.ShowPrint(this, $"{current.ScheduleNo} {current.RevisionLabel}", () => SIExportReport.Build(current), PageOrientation.Landscape);
    }
}

internal static class SIReasonWindow
{
    internal static string? Ask(Window owner, string title, SIExportSchedule schedule)
    {
        var window = new Window { Owner = owner, Title = title, Width = 580, Height = 270,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"{schedule.ScheduleNo} · {schedule.RevisionLabel}\n{schedule.Title}", Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "เหตุผล / หมายเหตุ *", Margin = new Thickness(0, 0, 0, 4) });
        var reason = SIExportEditorWindow.Input(""); panel.Children.Add(reason);
        var action = SIExportUi.Button(title, () => { if (reason.Text.Trim().Length > 0) window.DialogResult = true; });
        action.Margin = new Thickness(0, 14, 0, 0); panel.Children.Add(action); window.Content = panel;
        return window.ShowDialog() == true ? reason.Text.Trim() : null;
    }
}

internal static class SIExportReport
{
    internal static FlowDocument Build(SIExportSchedule schedule)
    {
        var document = new FlowDocument { FontFamily = new FontFamily("Leelawadee UI"), FontSize = 10,
            PagePadding = new Thickness(30), ColumnWidth = double.PositiveInfinity };
        document.Blocks.Add(new Paragraph(new Run($"{schedule.Document.Title} · {schedule.ScheduleNo} · {schedule.RevisionLabel} · {schedule.Status}")) { FontSize = 17, FontWeight = FontWeights.Bold });
        document.Blocks.Add(new Paragraph(new Run($"Recipient: {schedule.Recipient} · Shipment: {schedule.Period}\n{schedule.Document.Remark}")));
        foreach (var group in schedule.Document.Lines.GroupBy(x => string.IsNullOrWhiteSpace(x.Carrier) ? "Unassigned carrier" : x.Carrier))
        {
            document.Blocks.Add(new Paragraph(new Run(group.Key)) { FontSize = 13, FontWeight = FontWeights.Bold });
            var table = new Table { CellSpacing = 0 }; var rows = new TableRowGroup(); table.RowGroups.Add(rows);
            var header = new TableRow(); rows.Rows.Add(header);
            foreach (var label in new[] { "Contract / Plant", "Grade / Code", "Quantity / Pack", "POL → POD", "Planned ETD / ETA", "Shipment / Actual dates" })
                header.Cells.Add(Cell(label, true));
            foreach (var line in group)
            {
                var row = new TableRow(); rows.Rows.Add(row);
                foreach (var value in new[] { $"{line.Contract}\n{line.Plant}", $"{line.Grade}\n{line.Code}",
                    $"{line.Quantity} {line.QuantityUnit}\n{line.Package} / {line.MbUnits} MB", $"{line.Pol} → {line.Pod}",
                    $"{line.PlannedEtd:yyyy-MM-dd}\n{line.PlannedEta:yyyy-MM-dd}",
                    $"{line.ShipmentStatus}\n{line.ActualEtd:yyyy-MM-dd} / {line.ActualEta:yyyy-MM-dd}" }) row.Cells.Add(Cell(value, false));
            }
            document.Blocks.Add(table);
        }
        return document;
    }
    private static TableCell Cell(string text, bool header) => new(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
    { Padding = new Thickness(6), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(.5),
        Background = header ? Brushes.AliceBlue : Brushes.White, FontWeight = header ? FontWeights.Bold : FontWeights.Normal };
}

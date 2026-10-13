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
    private ProgressBar? _siProgress;
    private string _siSearch = "";
    private BookingQueueDatePicker? _siStartDatePicker;
    private BookingQueueDatePicker? _siEndDatePicker;
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
        var search = SIExportEditorWindow.Input(_siSearch);
        search.ToolTip = "Search SI No., recipient, shipment month, contract, PO, carrier, and line details";
        search.TextChanged += (_, _) => _siSearch = search.Text;
        search.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { _siSkip = 0; await LoadSIExportsAsync(); } };
        var searchHint = new TextBlock { Text = "Search SI No. / recipient / month / contract / PO…",
            Foreground = Brushes.Gray, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0), IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(search.Text) ? Visibility.Visible : Visibility.Collapsed };
        search.TextChanged += (_, _) => searchHint.Visibility = string.IsNullOrEmpty(search.Text)
            ? Visibility.Visible : Visibility.Collapsed;
        var searchBox = new Grid { Width = 260, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
        searchBox.Children.Add(search); searchBox.Children.Add(searchHint); tools.Children.Add(searchBox);

        _siStartDatePicker = new BookingQueueDatePicker
        {
            PlaceholderText = "Start date",
            ToolTip = "Start date (From) · Press Delete to clear",
            Width = 120,
            Height = 32,
            Margin = new Thickness(0, 0, 4, 0)
        };
        _siStartDatePicker.DateSelected += async (_, _) => { _siSkip = 0; await LoadSIExportsAsync(); };
        tools.Children.Add(_siStartDatePicker);

        var toText = new TextBlock
        {
            Text = "-",
            Foreground = Brushes.SlateGray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            FontWeight = FontWeights.SemiBold
        };
        tools.Children.Add(toText);

        _siEndDatePicker = new BookingQueueDatePicker
        {
            PlaceholderText = "End date",
            ToolTip = "End date (To) · Press Delete to clear",
            Width = 120,
            Height = 32,
            Margin = new Thickness(0, 0, 6, 0)
        };
        _siEndDatePicker.DateSelected += async (_, _) => { _siSkip = 0; await LoadSIExportsAsync(); };
        tools.Children.Add(_siEndDatePicker);

        var clearDateBtn = IconOnlyActionButton("Clear dates", FluentIcons.Common.Icon.CalendarCancel, new Thickness(0, 0, 8, 0));
        clearDateBtn.Click += async (_, _) =>
        {
            if (_siStartDatePicker.SelectedDate is null && _siEndDatePicker.SelectedDate is null) return;
            _siStartDatePicker.SelectedDate = null;
            _siEndDatePicker.SelectedDate = null;
            _siSkip = 0;
            await LoadSIExportsAsync();
        };
        tools.Children.Add(clearDateBtn);

        AddSIButton(tools, "Search / Refresh", "orders:read", false, async () => { _siSkip = 0; await LoadSIExportsAsync(); }, icon: FluentIcons.Common.Icon.ArrowClockwise);
        AddSIButton(tools, "Create SI", "orders:create", false, () => EditSIExportAsync(null));
        AddSIButton(tools, "Edit / Tracking", "orders:update", true, () => EditSIExportAsync(SISelected), icon: FluentIcons.Common.Icon.Edit);
        AddSIButton(tools, "Copy SI", "orders:create", true, () => EditSIExportAsync(SISelected, copy: true), icon: FluentIcons.Common.Icon.Copy);
        AddSIButton(tools, "Confirm SI", "orders:approve", true, () => SIActionAsync(true));
        AddSIButton(tools, "Cancel SI", "orders:update", true, () => SIActionAsync(false));
        AddSIButton(tools, "Revision / Compare", "orders:read", true, ShowSIHistoryAsync);
        AddSIButton(tools, "Export Excel by Carrier", "orders:read", true, ExportSIAsync);
        AddSIButton(tools, "Report / Print", "orders:read", true, PrintSIAsync, icon: FluentIcons.Common.Icon.Print);
        AddSIButton(tools, "‹ Previous", "orders:read", false, async () => { _siSkip = Math.Max(0, _siSkip - 50); await LoadSIExportsAsync(); });
        AddSIButton(tools, "Next ›", "orders:read", false, async () => { if (_siSkip + 50 < _siTotal) { _siSkip += 50; await LoadSIExportsAsync(); } });
        _siProgress = new ProgressBar { Height = 4, IsIndeterminate = true, Visibility = Visibility.Collapsed,
            Margin = new Thickness(0, 0, 0, 5) };
        _siMessage = new TextBlock { Text = _session.IsPreview ? "Sign in to use SI Export."
                : "Select Search / Refresh to load SI schedules, or choose Create SI to add a schedule.",
            Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap };
        var statusPanel = new StackPanel(); statusPanel.Children.Add(_siProgress); statusPanel.Children.Add(_siMessage);
        var statusBorder = new Border { Child = statusPanel, Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 6), Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            CornerRadius = new CornerRadius(3) };
        Grid.SetRow(statusBorder, 2); root.Children.Add(statusBorder);
        _siGrid = new DataGrid { ItemsSource = _siSchedules, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column, RowHeight = 34 };
        SIExportUi.ApplySoftGridTheme(_siGrid);
        SIExportUi.Column(_siGrid, "No.", "No", 50d, center: true);
        SIExportUi.Column(_siGrid, "SI No.", "ScheduleNo", 220d);
        SIExportUi.Column(_siGrid, "Recipient", "Recipient", 140d);
        SIExportUi.Column(_siGrid, "Shipment Month", "Period", 120d, center: true);
        SIExportUi.Column(_siGrid, "Revision", "RevisionLabel", 90d, center: true);
        SIExportUi.StatusBadgeColumn(_siGrid, "Status", 120d);
        SIExportUi.Column(_siGrid, "Lines", "LineCount", 80d, center: true);
        SIExportUi.Column(_siGrid, "Last Updated", "UpdatedAtDisplay", 210d);
        _siLines = SIExportUi.LineGrid(Array.Empty<SIExportLine>());
        _siDetailsMessage = new TextBlock { Text = "Select a schedule to view its contract and shipping details.", Foreground = Brushes.SlateGray,
            Margin = new Thickness(0, 10, 0, 8), TextWrapping = TextWrapping.Wrap };
        _siGrid.SelectionChanged += (_, _) => { _siSelectedId = SISelected?.Id; UpdateSISelection(); };
        _siGrid.MouseDoubleClick += async (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<System.Windows.Controls.Primitives.DataGridColumnHeader>(dep) is not null ||
                    FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) is not null ||
                    FindVisualParent<Button>(dep) is not null)
                {
                    return;
                }
            }
            if (SISelected is { } selected)
            {
                await EditSIExportAsync(selected, isReadOnly: true);
            }
        };
        _siLines.MouseDoubleClick += async (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindVisualParent<System.Windows.Controls.Primitives.DataGridColumnHeader>(dep) is not null ||
                    FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) is not null ||
                    FindVisualParent<Button>(dep) is not null)
                {
                    return;
                }
            }
            if (SISelected is { } selected)
            {
                await EditSIExportAsync(selected, isReadOnly: true);
            }
        };
        Grid.SetRow(_siGrid, 3); root.Children.Add(_siGrid);
        Grid.SetRow(_siDetailsMessage, 4); root.Children.Add(_siDetailsMessage);
        Grid.SetRow(_siLines, 5); root.Children.Add(_siLines);
        _siGrid.SelectedItem = _siSchedules.FirstOrDefault(x => x.Id == _siSelectedId);
        UpdateSISelection();
        return root;
    }

    private void AddSIButton(Panel panel, string label, string permission, bool selection, Func<Task> action, FluentIcons.Common.Icon? icon = null)
    {
        var button = icon is { } iconName
            ? IconOnlyActionButton(label, iconName)
            : SIExportUi.Button(label, () => { });
        button.Click += async (_, _) =>
        {
            if (!SICan(permission)) return;
            try { await action(); }
            catch (Exception ex)
            {
                SetSIStatus($"Action failed: {ex.Message}", error: true);
                MessageBox.Show(this, ex.Message, "SI Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        _siButtons.Add((button, permission, selection)); panel.Children.Add(button);
    }

    private void UpdateSISelection()
    {
        var selected = SISelected;
        if (selected is not null)
        {
            for (var i = 0; i < selected.Document.Lines.Count; i++)
            {
                selected.Document.Lines[i].No = i + 1;
            }
        }
        if (_siLines is not null) _siLines.ItemsSource = selected?.Document.Lines ?? [];
        if (_siDetailsMessage is not null)
            _siDetailsMessage.Text = selected is null ? "Select a schedule to view its contract and shipping details."
                : $"{selected.ScheduleNo} · {selected.RevisionLabel} · {selected.Status} · " +
                    (selected.ValidationIssues.Count == 0 ? "Ready to confirm."
                        : $"{selected.ValidationIssues.Count} required field(s) need attention. Open Edit / Tracking to review.");
        foreach (var (button, permission, selection) in _siButtons)
            button.IsEnabled = !_siLoading && SICan(permission) && (!selection || selected is not null);
    }

    private void SetSIStatus(string message, bool busy = false, bool error = false)
    {
        if (_siMessage is not null)
        {
            _siMessage.Text = message;
            _siMessage.Foreground = error ? Brushes.Firebrick : Brushes.SlateGray;
        }
        if (_siProgress is not null) _siProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<T> LoadSIDataAsync<T>(string loadingMessage, Func<Task<T>> load, string completedMessage)
    {
        _siLoading = true;
        UpdateSISelection();
        SetSIStatus(loadingMessage, busy: true);
        try
        {
            var result = await load();
            SetSIStatus(completedMessage);
            return result;
        }
        catch (Exception ex)
        {
            SetSIStatus($"Unable to load SI data: {ex.Message}", error: true);
            throw;
        }
        finally
        {
            _siLoading = false;
            UpdateSISelection();
        }
    }

    private async Task LoadSIExportsAsync()
    {
        if (_session.IsPreview || _purchasingSection != "SI Export") return;
        var version = ++_siLoadVersion; var token = _session.Token;
        try
        {
            _siLoading = true; UpdateSISelection();
            SetSIStatus("Loading SI schedules…", busy: true);

            var startDate = _siStartDatePicker?.SelectedDate;
            var endDate = _siEndDatePicker?.SelectedDate;
            int? queryYear = null;
            int? queryMonth = null;
            if (startDate is not null && endDate is not null &&
                startDate.Value.Year == endDate.Value.Year &&
                startDate.Value.Month == endDate.Value.Month)
            {
                queryYear = startDate.Value.Year;
                queryMonth = startDate.Value.Month;
            }

            var page = await _siExportService.ListAsync(token, _siSearch, _siSkip, queryYear, queryMonth,
                (startDate is not null || endDate is not null) ? 200 : 50);
            if (version != _siLoadVersion || _session.Token != token || _purchasingSection != "SI Export") return;

            var items = page.Items;
            if (startDate is not null || endDate is not null)
            {
                var s = startDate?.Date ?? DateTime.MinValue;
                var e = endDate?.Date ?? DateTime.MaxValue;
                items = items.Where(item =>
                {
                    // Check Shipment Month (Period)
                    if (item.Document.ShipmentYear > 0 && item.Document.ShipmentMonth > 0)
                    {
                        var periodStart = new DateTime(item.Document.ShipmentYear, item.Document.ShipmentMonth, 1);
                        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                        if (periodEnd >= s && periodStart <= e) return true;
                    }
                    // Check Lines dates (Planned/Actual ETD/ETA)
                    foreach (var line in item.Document.Lines)
                    {
                        var lineDate = line.ActualEtd ?? line.PlannedEtd ?? line.ActualEta ?? line.PlannedEta;
                        if (lineDate is { } d)
                        {
                            var dt = d.ToDateTime(TimeOnly.MinValue);
                            if (dt >= s && dt <= e) return true;
                        }
                    }
                    return false;
                }).ToList();
            }

            for (var i = 0; i < items.Count; i++)
            {
                items[i].No = _siSkip + i + 1;
            }

            _siSchedules = items;
            _siTotal = (startDate is not null || endDate is not null) ? items.Count : page.Total;
            if (_siGrid is not null)
            {
                var id = _siSelectedId;
                _siGrid.ItemsSource = _siSchedules;
                _siGrid.SelectedItem = _siSchedules.FirstOrDefault(x => x.Id == id);
            }
            SetSIStatus(_siSchedules.Count == 0 ? "No SI schedules found for the selected criteria."
                : $"Loaded {_siTotal:N0} SI schedule(s). Showing {_siSkip + (_siSchedules.Count > 0 ? 1 : 0):N0}–{_siSkip + _siSchedules.Count:N0}.");
        }
        catch (Exception ex)
        {
            if (version == _siLoadVersion) SetSIStatus("Unable to load SI schedules: " + ex.Message, error: true);
        }
        finally { if (version == _siLoadVersion) { _siLoading = false; UpdateSISelection(); } }
    }

    private async Task EditSIExportAsync(SIExportSchedule? source, bool copy = false, bool isReadOnly = false)
    {
        if (!isReadOnly && !SICan(source is null || copy ? "orders:create" : "orders:update")) return;
        try
        {
            if (source is not null)
                source = await LoadSIDataAsync("Loading SI schedule details…",
                    () => _siExportService.GetAsync(_session.Token, source.Id), "SI schedule details loaded.");
            if (!isReadOnly && source?.Status == "CANCELLED" && !copy)
                throw new InvalidOperationException("This SI schedule is cancelled. Copy it to create a new schedule.");
            var token = _session.Token;
            var catalog = await LoadSIDataAsync("Loading SI reference data…",
                () => _siExportService.CatalogAsync(token), isReadOnly ? "SI schedule details loaded." : "SI data loaded. Ready to edit.");
            var editor = new SIExportEditorWindow(source, copy, catalog, isReadOnly ? null : async payload =>
            {
                var saved = await _siExportService.SaveAsync(token, source is null || copy ? null : source.Id, payload);
                _siSelectedId = saved.Id;
            }, isReadOnly: isReadOnly) { Owner = this };
            if (editor.ShowDialog() == true && !isReadOnly) await LoadSIExportsAsync();
        }
        catch (Exception ex)
        {
            SetSIStatus($"Unable to open SI editor: {ex.Message}", error: true);
            MessageBox.Show(this, ex.Message, "SI Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task SIActionAsync(bool confirm)
    {
        if (SISelected is not { } selected) return;
        var current = await LoadSIDataAsync("Checking the latest SI revision…",
            () => _siExportService.GetAsync(_session.Token, selected.Id), "Latest SI revision loaded.");
        if (current.Revision != selected.Revision)
        {
            await LoadSIExportsAsync();
            throw new InvalidOperationException("This schedule has changed. Review the latest revision before confirming or cancelling.");
        }
        if (confirm && current.ValidationIssues.Count > 0)
            throw new InvalidOperationException($"{current.ValidationIssues.Count} required field(s) need attention. Open Edit / Tracking to review them.");
        var reason = SIReasonWindow.Ask(this, confirm ? "Confirm SI" : "Cancel SI", current);
        if (reason is null) return;
        await LoadSIDataAsync(confirm ? "Confirming SI schedule…" : "Cancelling SI schedule…",
            () => _siExportService.ActionAsync(_session.Token, current.Id, current.Revision, reason, confirm),
            confirm ? "SI schedule confirmed." : "SI schedule cancelled.");
        await LoadSIExportsAsync();
    }

    private async Task ShowSIHistoryAsync()
    {
        if (SISelected is not { } selected) return;
        var history = await LoadSIDataAsync("Loading revision history…",
            () => _siExportService.RevisionsAsync(_session.Token, selected.Id), "Revision history loaded.");
        new SIExportHistoryWindow(_siExportService, _session.Token, selected, history) { Owner = this }.ShowDialog();
    }

    private async Task ExportSIAsync()
    {
        if (SISelected is not { } selected) return;
        var current = await LoadSIDataAsync("Loading SI schedule for Excel export…",
            () => _siExportService.GetAsync(_session.Token, selected.Id), "SI schedule loaded for export.");
        var dialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", FileName = $"{current.ScheduleNo}_{current.RevisionLabel}.xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        SIExportExcelWriter.Write(current, dialog.FileName, separateCarriers: true);
        SetSIStatus($"Exported {current.RevisionLabel} to {dialog.FileName}.");
    }

    private async Task PrintSIAsync()
    {
        if (SISelected is not { } selected) return;
        var current = await LoadSIDataAsync("Preparing SI report…",
            () => _siExportService.GetAsync(_session.Token, selected.Id), "SI report is ready to print.");
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
        panel.Children.Add(new TextBlock { Text = "Reason / Notes *", Margin = new Thickness(0, 0, 0, 4) });
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

using System.Globalization;
using System.Text.Json;
using System.Windows.Data;

namespace YTRC.ERP.Wpf;

internal sealed class MarkingPreparationSISourceWindow : Window
{
    private readonly List<SourceLine> _allLines;
    private readonly ComboBox _monthFilter;
    private readonly TextBox _search;
    private readonly DataGrid _grid;
    private readonly TextBlock _status;
    private readonly List<MonthOption> _months;

    public IReadOnlyList<JsonElement> SelectedLines { get; private set; } = [];

    public MarkingPreparationSISourceWindow(JsonElement candidates, DateTime? initialMonth = null)
    {
        _allLines = ReadLines(candidates);
        _months = _allLines
            .Select(line => line.ShipmentMonth)
            .Distinct()
            .OrderByDescending(month => month)
            .Select(month => new MonthOption(month, month.ToString("MMM yyyy", CultureInfo.InvariantCulture)))
            .ToList();

        Title = "Select SI lines for Marking Preparation";
        Width = 1540;
        Height = 760;
        MinWidth = 1120;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC")!);

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var filters = new StackPanel { Orientation = Orientation.Horizontal };
        _monthFilter = new ComboBox
        {
            Width = 160,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            DisplayMemberPath = nameof(MonthOption.Label),
            ItemsSource = _months,
            ToolTip = "Filter by SI shipment month"
        };
        _search = new TextBox
        {
            Width = 280,
            Height = 34,
            Padding = new Thickness(8, 4, 8, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search contract, PO, recipient, destination or SI number"
        };
        filters.Children.Add(_monthFilter);
        filters.Children.Add(_search);
        DockPanel.SetDock(filters, Dock.Left);
        header.Children.Add(filters);

        _status = new TextBlock
        {
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")!),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(_status, Dock.Right);
        header.Children.Add(_status);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = false,
            SelectionMode = DataGridSelectionMode.Extended,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = System.Windows.Media.Brushes.White,
            AlternatingRowBackground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8FAFC")!),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 10)
        };
        AddCheckColumn(_grid);
        AddColumn(_grid, "Shipment Month", nameof(SourceLine.MonthLabel), 105);
        AddColumn(_grid, "SI No.", nameof(SourceLine.ScheduleNo), 210);
        AddColumn(_grid, "Recipient", nameof(SourceLine.Recipient), 115);
        AddColumn(_grid, "SI Rev.", nameof(SourceLine.RevisionLabel), 75, centered: true);
        AddColumn(_grid, "Contract", nameof(SourceLine.JointContractName), 210);
        AddColumn(_grid, "Customer PO", nameof(SourceLine.CustomerPo), 150);
        AddColumn(_grid, "SI Code", nameof(SourceLine.SiCode), 80, centered: true);
        AddColumn(_grid, "POD", nameof(SourceLine.SiPod), 180);
        AddColumn(_grid, "Marking Grade", nameof(SourceLine.MarkingGrade), 110, centered: true);
        AddColumn(_grid, "MB Units", nameof(SourceLine.Unit), 80, centered: true);
        AddColumn(_grid, "Package", nameof(SourceLine.Package), 90, centered: true);
        AddColumn(_grid, "Planned ETD", nameof(SourceLine.PlannedEtd), 105, centered: true);
        AddColumn(_grid, "Review", nameof(SourceLine.WarningSummary), 300);
        _grid.CellEditEnding += (_, _) => Dispatcher.BeginInvoke(new Action(() => RefreshStatus()));
        Grid.SetRow(_grid, 1);
        root.Children.Add(_grid);

        var footer = new DockPanel();
        var cancel = new Button
        {
            Content = "Cancel",
            MinWidth = 88,
            Height = 34,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 3, 14, 3),
            IsCancel = true
        };
        DockPanel.SetDock(cancel, Dock.Right);
        footer.Children.Add(cancel);
        var select = new Button
        {
            Content = "Use selected SI lines",
            MinWidth = 170,
            Height = 34,
            Padding = new Thickness(14, 3, 14, 3),
            IsDefault = true
        };
        select.Click += (_, _) => AcceptSelection();
        DockPanel.SetDock(select, Dock.Right);
        footer.Children.Add(select);
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _monthFilter.SelectionChanged += (_, _) => RefreshGrid();
        _search.TextChanged += (_, _) => RefreshGrid();
        _grid.ItemsSource = _allLines;

        if (_months.Count > 0)
        {
            var desired = initialMonth is { } value
                ? new DateTime(value.Year, value.Month, 1)
                : DateTime.Today.AddDays(1 - DateTime.Today.Day);
            _monthFilter.SelectedItem = _months.FirstOrDefault(item => item.Month == desired) ?? _months[0];
        }
        else
        {
            select.IsEnabled = false;
        }

        Content = root;
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        var selectedMonth = (_monthFilter.SelectedItem as MonthOption)?.Month;
        var query = _search.Text.Trim();
        var view = _allLines.Where(line => selectedMonth is null || line.ShipmentMonth == selectedMonth.Value);
        if (!string.IsNullOrWhiteSpace(query))
            view = view.Where(line => line.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));
        var rows = view.ToArray();
        _grid.ItemsSource = rows;
        RefreshStatus(rows);
    }

    private void RefreshStatus(SourceLine[]? visibleRows = null)
    {
        var selectedMonth = (_monthFilter.SelectedItem as MonthOption)?.Month;
        var query = _search.Text.Trim();
        var rows = visibleRows ?? _allLines.Where(line =>
        {
            var monthMatches = selectedMonth is null || line.ShipmentMonth == selectedMonth.Value;
            var searchMatches = string.IsNullOrWhiteSpace(query) ||
                                line.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase);
            return monthMatches && searchMatches;
        }).ToArray();
        _status.Text = _allLines.Count == 0
            ? "No active SI lines are available. Create or update an SI schedule first."
            : $"{rows.Length} available line(s) · {rows.Count(line => line.Warnings.Count > 0)} need review · {_allLines.Count(line => line.IsSelected)} selected";
    }

    private void AcceptSelection()
    {
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        var selected = _allLines.Where(line => line.IsSelected).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select at least one SI line.", "SI source selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (selected.Select(line => line.ShipmentMonth).Distinct().Skip(1).Any())
        {
            MessageBox.Show(this, "Select SI lines from one shipment month at a time.", "SI source selection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectedLines = selected.Select(line => line.Raw.Clone()).ToList();
        DialogResult = true;
        Close();
    }

    private static List<SourceLine> ReadLines(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("items", out var items) ||
            items.ValueKind != JsonValueKind.Array)
            return [];

        return items.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(item =>
        {
            var month = ReadDate(item, "shipmentMonth") ?? DateTime.Today;
            var warnings = item.TryGetProperty("warnings", out var warningArray) && warningArray.ValueKind == JsonValueKind.Array
                ? warningArray.EnumerateArray().Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToList()
                : [];
            return new SourceLine
            {
                Raw = item.Clone(),
                ShipmentMonth = new DateTime(month.Year, month.Month, 1),
                MonthLabel = month.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                ScheduleNo = ReadString(item, "scheduleNo"),
                Recipient = ReadString(item, "recipient"),
                RevisionLabel = $"R{ReadInt(item, "sourceSiRevision"):D2}",
                JointContractName = ReadString(item, "jointContractName"),
                CustomerPo = ReadString(item, "customerPo"),
                SiCode = ReadString(item, "siCode"),
                SiPod = ReadString(item, "siPod"),
                MarkingGrade = ReadString(item, "grade"),
                Unit = ReadString(item, "unit"),
                Package = ReadString(item, "packageType"),
                PlannedEtd = ReadDate(item, "etdDate")?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "",
                Warnings = warnings
            };
        }).ToList();
    }

    private static string ReadString(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static int ReadInt(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) return 0;
        if (value.TryGetInt32(out var result)) return result;
        return value.ValueKind == JsonValueKind.String &&
               int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
            ? result
            : 0;
    }

    private static DateTime? ReadDate(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed;
        return null;
    }

    private static void AddCheckColumn(DataGrid grid)
    {
        var check = new DataGridCheckBoxColumn
        {
            Header = "Use",
            Binding = new Binding(nameof(SourceLine.IsSelected)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = 54
        };
        grid.Columns.Add(check);
    }

    private static void AddColumn(DataGrid grid, string header, string property, double width, bool centered = false)
    {
        var text = new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property),
            Width = new DataGridLength(width),
            IsReadOnly = true
        };
        if (centered)
        {
            text.ElementStyle = new Style(typeof(TextBlock));
            text.ElementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            text.ElementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        }
        grid.Columns.Add(text);
    }

    private sealed record MonthOption(DateTime Month, string Label)
    {
        // Keep ComboBox items readable even when a control template falls back to ToString().
        public override string ToString() => Label;
    }

    private sealed class SourceLine
    {
        public JsonElement Raw { get; init; }
        public bool IsSelected { get; set; }
        public DateTime ShipmentMonth { get; init; }
        public string MonthLabel { get; init; } = "";
        public string ScheduleNo { get; init; } = "";
        public string Recipient { get; init; } = "";
        public string RevisionLabel { get; init; } = "";
        public string JointContractName { get; init; } = "";
        public string CustomerPo { get; init; } = "";
        public string SiCode { get; init; } = "";
        public string SiPod { get; init; } = "";
        public string MarkingGrade { get; init; } = "";
        public string Unit { get; init; } = "";
        public string Package { get; init; } = "";
        public string PlannedEtd { get; init; } = "";
        public List<string> Warnings { get; init; } = [];
        public string WarningSummary => string.Join("; ", Warnings);
        public string SearchText => string.Join(" ", ScheduleNo, Recipient, JointContractName, CustomerPo, SiCode, SiPod);
    }
}

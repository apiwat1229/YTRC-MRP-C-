using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows.Data;

namespace YTRC.ERP.Wpf;

internal sealed class MarkingPreparationEditorWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly EditorState _state;
    private readonly ObservableCollection<RevisionDraft> _revisions;
    private readonly ObservableCollection<DetailDraft> _details;
    private readonly DatePicker _reportMonth;
    private readonly TextBox _printedBy;
    private readonly TextBox _issueBy;
    private readonly DatePicker _issueDate;
    private readonly TextBox _productionAdminSign;
    private readonly DatePicker _productionAdminDate;
    private readonly TextBox _qacSign;
    private readonly DatePicker _qacDate;
    private readonly TextBox _warehouseSign;
    private readonly DatePicker _warehouseDate;
    private readonly DataGrid _revisionGrid;
    private readonly DataGrid _detailGrid;

    public object? Payload { get; private set; }

    public MarkingPreparationEditorWindow(JsonElement? source, string defaultPrintedBy = "")
    {
        _state = source is { ValueKind: JsonValueKind.Object } json
            ? JsonSerializer.Deserialize<EditorState>(json.GetRawText(), JsonOptions) ?? new EditorState()
            : new EditorState
            {
                ReportMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                PrintedBy = defaultPrintedBy,
                Revisions = [new RevisionDraft { Revision = "00", ReportDate = DateTime.Today }]
            };
        _state.ReportMonth = _state.ReportMonth == default
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : new DateTime(_state.ReportMonth.Year, _state.ReportMonth.Month, 1);
        _state.Revisions ??= [];
        _state.Details ??= [];
        _revisions = new ObservableCollection<RevisionDraft>(_state.Revisions);
        _details = new ObservableCollection<DetailDraft>(_state.Details);

        Title = source is null ? "New Marking Preparation Report" : "Edit Marking Preparation Report";
        Width = 1510;
        Height = 860;
        MinWidth = 1120;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = Brush("#F5F7FA");

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        heading.Children.Add(new TextBlock
        {
            Text = "Marking Preparation Report",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E")
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Edit report month, revision history, shipment marking details and distribution sign-off.",
            FontSize = 12.5,
            Foreground = Brush("#708197"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        root.Children.Add(heading);

        var fields = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        for (var i = 0; i < 4; i++) fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _reportMonth = CreateDatePicker(_state.ReportMonth);
        _printedBy = CreateTextBox(_state.PrintedBy);
        _issueBy = CreateTextBox(_state.IssueBy);
        _issueDate = CreateDatePicker(_state.IssueDate);
        _productionAdminSign = CreateTextBox(_state.ProductionAdminSign);
        _productionAdminDate = CreateDatePicker(_state.ProductionAdminDate);
        _qacSign = CreateTextBox(_state.QacSign);
        _qacDate = CreateDatePicker(_state.QacDate);
        _warehouseSign = CreateTextBox(_state.WarehouseSign);
        _warehouseDate = CreateDatePicker(_state.WarehouseDate);
        AddField(fields, 0, 0, "Report month", _reportMonth);
        AddField(fields, 1, 0, "Printed by", _printedBy);
        AddField(fields, 2, 0, "Issue by", _issueBy);
        AddField(fields, 3, 0, "Issue date", _issueDate);
        AddField(fields, 0, 1, "Production Admin sign", _productionAdminSign);
        AddField(fields, 1, 1, "Production Admin date", _productionAdminDate);
        AddField(fields, 2, 1, "QAC sign", _qacSign);
        AddField(fields, 3, 1, "QAC date", _qacDate);
        AddField(fields, 0, 2, "W/H sign", _warehouseSign);
        AddField(fields, 1, 2, "W/H date", _warehouseDate);
        fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(fields);
        Grid.SetRow(fields, 1);

        var revisionHost = BuildGridSection("Revision history", out _revisionGrid, _revisions,
            AddRevisionColumns);
        revisionHost.Height = 165;
        revisionHost.Margin = new Thickness(0, 0, 0, 8);
        root.Children.Add(revisionHost);
        Grid.SetRow(revisionHost, 2);

        var detailHost = BuildGridSection("Marking details (ETD is retained for date calculation and is not printed)",
            out _detailGrid, _details, AddDetailColumns);
        root.Children.Add(detailHost);
        Grid.SetRow(detailHost, 3);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var cancel = CreateButton("Cancel");
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = CreateButton("Save report", primary: true);
        save.Click += (_, _) => SaveReport();
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        root.Children.Add(footer);
        Grid.SetRow(footer, 4);

        Content = root;
    }

    private Grid BuildGridSection<T>(string title, out DataGrid grid, ObservableCollection<T> items,
        Action<DataGrid> configureColumns)
    {
        var section = new Grid();
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        DataGrid? gridForButtons = null;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var label = new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(label);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var add = CreateButton(title.StartsWith("Revision", StringComparison.Ordinal) ? "Add revision" : "Add line");
        add.Click += (_, _) =>
        {
            if (items is ObservableCollection<RevisionDraft> revisions)
                revisions.Add(new RevisionDraft { Revision = NextRevision(revisions), ReportDate = DateTime.Today });
            else if (items is ObservableCollection<DetailDraft> details)
                details.Add(new DetailDraft());
        };
        var remove = CreateButton(title.StartsWith("Revision", StringComparison.Ordinal) ? "Remove revision" : "Remove line");
        remove.Click += (_, _) =>
        {
            if (gridForButtons?.SelectedItem is T selected) items.Remove(selected);
        };
        controls.Children.Add(add);
        controls.Children.Add(remove);
        DockPanel.SetDock(controls, Dock.Right);
        header.Children.Add(controls);
        section.Children.Add(header);

        grid = new DataGrid
        {
            ItemsSource = items,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = Brush("#F7F9FC"),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 11.5,
            RowHeight = 28
        };
        configureColumns(grid);
        gridForButtons = grid;
        if (items is ObservableCollection<DetailDraft>)
        {
            grid.CellEditEnding += (_, args) =>
            {
                if (args.Column.Header?.ToString() != "ETD Date" ||
                    args.Row.Item is not DetailDraft detail ||
                    args.EditingElement is not DatePicker editor)
                    return;

                var previousEtd = detail.EtdDate;
                var nextEtd = editor.SelectedDate?.Date;
                if (detail.MarkingDueDate is null || detail.MarkingDueDate == previousEtd?.AddDays(-20))
                    detail.MarkingDueDate = nextEtd?.AddDays(-20);
                detail.EtdDate = nextEtd;
            };
        }
        grid.LoadingRow += (_, eventArgs) => eventArgs.Row.Header = (eventArgs.Row.GetIndex() + 1).ToString(CultureInfo.InvariantCulture);
        section.Children.Add(grid);
        Grid.SetRow(grid, 1);
        return section;
    }

    private static void AddRevisionColumns(DataGrid grid)
    {
        AddTextColumn(grid, "Revision", nameof(RevisionDraft.Revision), 100);
        AddDateColumn(grid, "Date Report", nameof(RevisionDraft.ReportDate), 140);
        AddTextColumn(grid, "Remark", nameof(RevisionDraft.Remark), new DataGridLength(1, DataGridLengthUnitType.Star));
    }

    private static void AddDetailColumns(DataGrid grid)
    {
        AddTextColumn(grid, "Month", nameof(DetailDraft.MonthLabel), 70);
        AddTextColumn(grid, "Joint Contract Name", nameof(DetailDraft.JointContractName), 175);
        AddTextColumn(grid, "Code", nameof(DetailDraft.Code), 58);
        AddTextColumn(grid, "Dest.", nameof(DetailDraft.Destination), 110);
        AddTextColumn(grid, "Grade", nameof(DetailDraft.Grade), 70);
        AddTextColumn(grid, "Unit", nameof(DetailDraft.Unit), 56);
        AddTextColumn(grid, "Gross", nameof(DetailDraft.GrossWeight), 82, "N3");
        AddTextColumn(grid, "Net.", nameof(DetailDraft.NetWeight), 82, "N3");
        AddTextColumn(grid, "Package", nameof(DetailDraft.PackageType), 75);
        AddDateColumn(grid, "Marking Due Date", nameof(DetailDraft.MarkingDueDate), 126);
        AddTextColumn(grid, "Sign", nameof(DetailDraft.MarkingSign), 100);
        AddDateColumn(grid, "Date", nameof(DetailDraft.StatusDate), 108);
        AddDateColumn(grid, "ETD Date", nameof(DetailDraft.EtdDate), 108);
    }

    private static void AddTextColumn(DataGrid grid, string title, string property, double width, string? format = null)
        => AddTextColumn(grid, title, property, new DataGridLength(width), format);

    private static void AddTextColumn(DataGrid grid, string title, string property, DataGridLength width, string? format = null)
    {
        var binding = new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            StringFormat = format is null ? null : $"{{0:{format}}}"
        };
        grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = binding, Width = width });
    }

    private static void AddDateColumn(DataGrid grid, string title, string property, double width)
    {
#pragma warning disable CS0618
        var factory = new FrameworkElementFactory(typeof(DatePicker));
        factory.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        factory.SetValue(DatePicker.SelectedDateFormatProperty, DatePickerFormat.Short);
        factory.SetBinding(DatePicker.SelectedDateProperty, new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        var template = new DataTemplate { VisualTree = factory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn { Header = title, CellTemplate = template, CellEditingTemplate = template, Width = new DataGridLength(width) });
    }

    private void SaveReport()
    {
        _revisionGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _revisionGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _detailGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _detailGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_reportMonth.SelectedDate is not { } reportMonth)
        {
            ShowValidation("Choose a report month.");
            return;
        }

        var revisions = _revisions.Where(IsMeaningful).ToList();
        if (revisions.Any(item => string.IsNullOrWhiteSpace(item.Revision) || item.ReportDate is null))
        {
            ShowValidation("Each revision needs a revision number and report date.");
            return;
        }
        if (revisions.Select(item => item.Revision?.Trim() ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != revisions.Count)
        {
            ShowValidation("Revision numbers must be unique within a report.");
            return;
        }

        var details = _details.Where(IsMeaningful).ToList();
        if (details.Any(item => string.IsNullOrWhiteSpace(item.JointContractName)))
        {
            ShowValidation("Each detail row needs a Joint Contract Name.");
            return;
        }

        Payload = new
        {
            reportMonth = reportMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            printedBy = Optional(_printedBy.Text),
            issueBy = Optional(_issueBy.Text),
            issueDate = ToIso(_issueDate.SelectedDate),
            productionAdminSign = Optional(_productionAdminSign.Text),
            productionAdminDate = ToIso(_productionAdminDate.SelectedDate),
            qacSign = Optional(_qacSign.Text),
            qacDate = ToIso(_qacDate.SelectedDate),
            warehouseSign = Optional(_warehouseSign.Text),
            warehouseDate = ToIso(_warehouseDate.SelectedDate),
            revisions = revisions.Select(item => new
            {
                revision = item.Revision!.Trim(),
                reportDate = ToIso(item.ReportDate),
                remark = Optional(item.Remark)
            }).ToList(),
            details = details.Select(item => new
            {
                monthLabel = Optional(item.MonthLabel),
                jointContractName = item.JointContractName!.Trim(),
                code = Optional(item.Code),
                destination = Optional(item.Destination),
                grade = Optional(item.Grade),
                unit = item.Unit,
                grossWeight = item.GrossWeight,
                netWeight = item.NetWeight,
                packageType = Optional(item.PackageType),
                markingDueDate = ToIso(item.MarkingDueDate),
                markingSign = Optional(item.MarkingSign),
                statusDate = ToIso(item.StatusDate),
                etdDate = ToIso(item.EtdDate)
            }).ToList()
        };
        DialogResult = true;
        Close();
    }

    private static bool IsMeaningful(RevisionDraft item)
        => !string.IsNullOrWhiteSpace(item.Revision) || !string.IsNullOrWhiteSpace(item.Remark) || item.ReportDate is not null;

    private static bool IsMeaningful(DetailDraft item)
        => !string.IsNullOrWhiteSpace(item.MonthLabel) || !string.IsNullOrWhiteSpace(item.JointContractName) ||
           !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Destination) ||
           !string.IsNullOrWhiteSpace(item.Grade) || item.Unit is not null || item.GrossWeight is not null ||
           item.NetWeight is not null || !string.IsNullOrWhiteSpace(item.PackageType) || item.MarkingDueDate is not null ||
           !string.IsNullOrWhiteSpace(item.MarkingSign) || item.StatusDate is not null || item.EtdDate is not null;

    private void ShowValidation(string message)
        => MessageBox.Show(this, message, "Check report details", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ToIso(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NextRevision(IEnumerable<RevisionDraft> revisions)
    {
        var numbers = revisions.Select(item => int.TryParse(item.Revision, out var number) ? number : -1);
        return numbers.DefaultIfEmpty(-1).Max() is var next && next >= 0 ? (next + 1).ToString("D2", CultureInfo.InvariantCulture) : "00";
    }

    private static void AddField(Grid grid, int column, int row, string caption, UIElement control)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 8, 6) };
        stack.Children.Add(new TextBlock { Text = caption, FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 0, 0, 2) });
        stack.Children.Add(control);
        grid.Children.Add(stack);
        Grid.SetColumn(stack, column);
        Grid.SetRow(stack, row);
    }

    private static TextBox CreateTextBox(string? value) => new()
    {
        Text = value ?? "",
        Height = 30,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(7, 3, 7, 3)
    };

    private static DatePicker CreateDatePicker(DateTime? value) => new()
    {
        SelectedDate = value?.Date,
        Height = 30,
        VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(4, 2, 4, 2)
    };

    private static Button CreateButton(string text, bool primary = false) => new()
    {
        Content = text,
        Height = 31,
        MinWidth = 90,
        Margin = new Thickness(0, 0, 6, 0),
        Padding = new Thickness(9, 3, 9, 3),
        Background = primary ? Brush("#0F766E") : Brushes.White,
        Foreground = primary ? Brushes.White : Brush("#334155"),
        BorderBrush = primary ? Brush("#0F766E") : Brush("#D6DEE8"),
        BorderThickness = new Thickness(1)
    };

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);

    private sealed class EditorState
    {
        public DateTime ReportMonth { get; set; }
        public string? PrintedBy { get; set; }
        public string? IssueBy { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? ProductionAdminSign { get; set; }
        public DateTime? ProductionAdminDate { get; set; }
        public string? QacSign { get; set; }
        public DateTime? QacDate { get; set; }
        public string? WarehouseSign { get; set; }
        public DateTime? WarehouseDate { get; set; }
        public List<RevisionDraft> Revisions { get; set; } = [];
        public List<DetailDraft> Details { get; set; } = [];
    }

    private sealed class RevisionDraft
    {
        public string? Revision { get; set; }
        public DateTime? ReportDate { get; set; }
        public string? Remark { get; set; }
    }

    private sealed class DetailDraft
    {
        public string? MonthLabel { get; set; }
        public string? JointContractName { get; set; }
        public string? Code { get; set; }
        public string? Destination { get; set; }
        public string? Grade { get; set; }
        public int? Unit { get; set; }
        public decimal? GrossWeight { get; set; }
        public decimal? NetWeight { get; set; }
        public string? PackageType { get; set; }
        public DateTime? MarkingDueDate { get; set; }
        public string? MarkingSign { get; set; }
        public DateTime? StatusDate { get; set; }
        public DateTime? EtdDate { get; set; }
    }
}

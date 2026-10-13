using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

internal sealed record MarkingPreparationSIChangeField(string Field, string PreviousValue, string CurrentValue);

internal sealed record MarkingPreparationSIChangeLine(
    string Id,
    string Contract,
    string PreviousRevision,
    string CurrentRevision,
    string Fingerprint,
    IReadOnlyList<MarkingPreparationSIChangeField> Changes);

internal sealed record MarkingPreparationSIChangeReport(
    string Id,
    string Month,
    string Revision,
    IReadOnlyList<MarkingPreparationSIChangeLine> Lines);

internal sealed class MarkingPreparationSIChangeReviewWindow : Window
{
    private readonly IReadOnlyList<MarkingPreparationSIChangeReport> _reports;
    private readonly DataGrid _reportGrid;
    private readonly DataGrid _lineGrid;
    private readonly DataGrid _changeGrid;
    private readonly Button _editButton;
    private readonly Button _reviewButton;

    public string? OpenReportId { get; private set; }
    public string? ReviewReportId { get; private set; }
    public string? ReviewDecision { get; private set; }
    public IReadOnlyList<MarkingPreparationSIChangeLine> LinesToReview { get; private set; } = [];

    public MarkingPreparationSIChangeReviewWindow(
        IReadOnlyList<MarkingPreparationSIChangeReport> reports)
    {
        _reports = reports;
        Title = "Review SI changes";
        Width = 1180;
        Height = 780;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        header.Children.Add(new TextBlock
        {
            Text = "SI changes need review",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59))
        });
        header.Children.Add(new TextBlock
        {
            Text = "Compare the saved SI values with the latest revision. Edit Marking first if needed, then record your decision.",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.15, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _reportGrid = CreateGrid();
        AddColumn(_reportGrid, "Report month", nameof(MarkingPreparationSIChangeReport.Month), 1.2);
        AddColumn(_reportGrid, "Report revision", nameof(MarkingPreparationSIChangeReport.Revision), .8);
        AddColumn(_reportGrid, "Lines to review", nameof(ReportRow.PendingCount), .8, centered: true);
        _reportGrid.ItemsSource = reports.Select(report => new ReportRow(report)).ToList();
        _reportGrid.SelectionChanged += (_, _) => RefreshLines();
        var reportSection = CreateSection("Reports with pending SI changes", _reportGrid);
        Grid.SetRow(reportSection, 0);
        content.Children.Add(reportSection);

        _lineGrid = CreateGrid();
        _lineGrid.SelectionMode = DataGridSelectionMode.Extended;
        AddColumn(_lineGrid, "Contract", nameof(LineRow.Contract), 1.7);
        AddColumn(_lineGrid, "SI revision", nameof(LineRow.Revision), .9, centered: true);
        AddColumn(_lineGrid, "Changed fields", nameof(LineRow.ChangeCount), .75, centered: true);
        _lineGrid.SelectionChanged += (_, _) => RefreshChanges();
        var lineSection = CreateSection("Changed lines", _lineGrid);
        Grid.SetRow(lineSection, 1);
        content.Children.Add(lineSection);

        _changeGrid = CreateGrid();
        AddColumn(_changeGrid, "SI field", nameof(MarkingPreparationSIChangeField.Field), 1.2);
        AddColumn(_changeGrid, "Previous value", nameof(MarkingPreparationSIChangeField.PreviousValue), 1.6);
        AddColumn(_changeGrid, "Current value", nameof(MarkingPreparationSIChangeField.CurrentValue), 1.6);
        var changesSection = CreateSection("SI value comparison", _changeGrid);
        Grid.SetRow(changesSection, 2);
        content.Children.Add(changesSection);

        var note = new TextBlock
        {
            Text = "Select a report, then one or more changed lines. The review is tied to the SI revision shown here; a later SI change will require a new review.",
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(note, 3);
        content.Children.Add(note);
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var close = CreateButton("Close");
        close.IsCancel = true;
        DockPanel.SetDock(close, Dock.Right);
        footer.Children.Add(close);

        _reviewButton = CreateButton("Record review");
        _reviewButton.IsEnabled = false;
        _reviewButton.Click += (_, _) => RecordReview();
        DockPanel.SetDock(_reviewButton, Dock.Right);
        footer.Children.Add(_reviewButton);

        _editButton = CreateButton("Edit selected report");
        _editButton.IsEnabled = false;
        _editButton.Click += (_, _) => OpenSelectedReport();
        DockPanel.SetDock(_editButton, Dock.Right);
        footer.Children.Add(_editButton);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);
        Content = root;

        if (_reportGrid.Items.Count > 0)
            _reportGrid.SelectedIndex = 0;
    }

    private void RefreshLines()
    {
        var row = _reportGrid.SelectedItem as ReportRow;
        _lineGrid.ItemsSource = row?.Report.Lines.Select(line => new LineRow(line)).ToList() ?? [];
        _editButton.IsEnabled = row is not null;
        if (_lineGrid.Items.Count > 0)
            _lineGrid.SelectedIndex = 0;
        RefreshChanges();
    }

    private void RefreshChanges()
    {
        var selected = _lineGrid.SelectedItems.Cast<LineRow>().ToList();
        _changeGrid.ItemsSource = selected
            .SelectMany(row => row.Line.Changes.Select(change => new MarkingPreparationSIChangeField(
                selected.Count > 1 ? $"{row.Line.Contract}: {change.Field}" : change.Field,
                change.PreviousValue,
                change.CurrentValue)))
            .ToList();
        _reviewButton.IsEnabled = selected.Count > 0;
    }

    private void OpenSelectedReport()
    {
        if (_reportGrid.SelectedItem is not ReportRow row)
            return;
        OpenReportId = row.Report.Id;
        DialogResult = true;
    }

    private void RecordReview()
    {
        var report = _reportGrid.SelectedItem as ReportRow;
        var lines = _lineGrid.SelectedItems.Cast<LineRow>().Select(row => row.Line).ToList();
        if (report is null || lines.Count == 0)
            return;

        var answer = MessageBox.Show(
            this,
            "Did you update the Marking values to reflect the latest SI?\n\nYes: Marking updated\nNo: Keep current Marking values",
            "Record SI review",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Cancel)
            return;

        ReviewDecision = answer == MessageBoxResult.Yes ? "UPDATED" : "KEPT";
        ReviewReportId = report.Report.Id;
        LinesToReview = lines;
        DialogResult = true;
    }

    private static DataGrid CreateGrid()
        => new()
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            Background = Brushes.White,
            RowBackground = Brushes.White,
            AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 8)
        };

    private static Grid CreateSection(string title, UIElement content)
    {
        var section = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        section.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(0, 0, 0, 4),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85))
        });
        Grid.SetRow(content, 1);
        section.Children.Add(content);
        return section;
    }

    private static void AddColumn(DataGrid grid, string header, string property, double width, bool centered = false)
    {
        var text = new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property),
            Width = new DataGridLength(width, DataGridLengthUnitType.Star),
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

    private static Button CreateButton(string content)
        => new()
        {
            Content = content,
            MinWidth = 104,
            Height = 34,
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(8, 0, 0, 0)
        };

    private sealed record ReportRow(MarkingPreparationSIChangeReport Report)
    {
        public string Month => Report.Month;
        public string Revision => Report.Revision;
        public int PendingCount => Report.Lines.Count;
    }

    private sealed record LineRow(MarkingPreparationSIChangeLine Line)
    {
        public string Contract => Line.Contract;
        public string Revision => $"{Line.PreviousRevision} → {Line.CurrentRevision}";
        public int ChangeCount => Line.Changes.Count;
    }
}

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Documents;
using System.Printing;
using System.Windows.Data;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const string MarkingPreparationRoute = "/api/marking-preparations";
    private static readonly JsonElement EmptyMarkingPreparationArray = JsonDocument.Parse("[]").RootElement;
    private List<MarkingPreparationListItem> _markingPreparationReports = [];
    private DataGrid? _markingPreparationReportGrid;
    private DataGrid? _markingPreparationDetailGrid;
    private TextBox? _markingPreparationSearch;
    private ComboBox? _markingPreparationMonthFilter;
    private TextBlock? _markingPreparationMessage;
    private Border? _markingPreparationSIAlert;
    private TextBlock? _markingPreparationSIAlertText;
    private Button? _markingPreparationSIAlertButton;
    private Button? _markingPreparationEditButton;
    private Button? _markingPreparationAddFromSiButton;
    private Button? _markingPreparationDeleteButton;
    private Button? _markingPreparationPreviewButton;
    private Button? _markingPreparationExportButton;
    private Button? _markingPreparationPrintButton;
    private bool _markingPreparationLoading;

    private sealed record MarkingPreparationListItem(
        JsonElement Raw, string Id, DateTime ReportMonth, string RevisionLabel, int DetailCount, int No = 0, int RevisionCount = 0)
    {
        public string MonthLabel => ReportMonth.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        public int ChangedSiCount => Raw.TryGetProperty("siChangeCount", out var value) && value.TryGetInt32(out var count)
            ? count
            : ReadJsonArray(Raw, "details").EnumerateArray().Count(line => ReadBoolean(line, "sourceSiChanged"));
        public int PendingSiReviewCount => Raw.TryGetProperty("siChangePendingCount", out var value) && value.TryGetInt32(out var count)
            ? count
            : ReadJsonArray(Raw, "details").EnumerateArray().Count(IsSourceSiReviewPending);
        public string SiReviewLabel => PendingSiReviewCount > 0
            ? $"{PendingSiReviewCount} pending"
            : ChangedSiCount > 0 ? "Reviewed" : "—";
    }

    private sealed record MarkingPreparationDetailListItem(
        int No, string Month, string Contract, string Code, string Destination, string Grade,
        string Unit, string Gross, string Net, string Package, string DueDate, string Sign, string Date,
        string EtdDate, string SiSource, bool IsSiReviewPending, string SiSourceTooltip)
    {
        public Brush GradeBackground => GradeColorHelper.GetBackgroundBrush(Grade);
        public Brush GradeForeground => GradeColorHelper.GetForegroundBrush(Grade);
        public Brush GradeBorderBrush => GradeColorHelper.GetBorderBrush(Grade);
    }

    private UIElement BuildMarkingPreparationPage()
    {
        var root = new Grid
        {
            Margin = UiTokens.PageMargin
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _markingPreparationSearch = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 8, 2),
            ToolTip = "Search report month"
        };
        var searchPlaceholder = new TextBlock
        {
            Text = "Search...",
            FontSize = 13,
            Foreground = CreateBrush("#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(_markingPreparationSearch.Text) ? Visibility.Visible : Visibility.Collapsed
        };
        _markingPreparationSearch.TextChanged += (_, _) =>
        {
            searchPlaceholder.Visibility = string.IsNullOrEmpty(_markingPreparationSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            RefreshMarkingPreparationReportGrid();
        };

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var searchIcon = new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Search,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            FontSize = 16,
            Width = 16,
            Height = 16,
            Foreground = CreateBrush("#64748B"),
            Margin = new Thickness(8, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(searchIcon, 0);
        searchGrid.Children.Add(searchIcon);

        var inputHost = new Grid();
        inputHost.Children.Add(searchPlaceholder);
        inputHost.Children.Add(_markingPreparationSearch);
        Grid.SetColumn(inputHost, 1);
        searchGrid.Children.Add(inputHost);

        var searchBoxContainer = new Border
        {
            Width = 190,
            Height = 34,
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = searchGrid
        };

        _markingPreparationMonthFilter = new ComboBox
        {
            Width = 140,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Filter by report month"
        };
        UpdateMarkingPreparationMonthFilter();
        _markingPreparationMonthFilter.SelectionChanged += (_, _) => RefreshMarkingPreparationReportGrid();

        var refresh = MarkingPreparationButton("Refresh", FluentIcons.Common.Icon.ArrowClockwise);
        refresh.Click += async (_, _) => await LoadMarkingPreparationReportsAsync();
        var edit = MarkingPreparationButton("Edit selected", FluentIcons.Common.Icon.Edit);
        _markingPreparationEditButton = edit;
        edit.Click += async (_, _) => await EditMarkingPreparationAsync(SelectedMarkingPreparation());
        var addFromSi = MarkingPreparationButton("Add from SI", FluentIcons.Common.Icon.Add);
        _markingPreparationAddFromSiButton = addFromSi;
        addFromSi.Click += async (_, _) => await AddMarkingPreparationLinesFromSiAsync();
        var delete = MarkingPreparationButton("Delete selected", FluentIcons.Common.Icon.Delete);
        _markingPreparationDeleteButton = delete;
        delete.Click += async (_, _) => await DeleteMarkingPreparationAsync();
        var preview = MarkingPreparationButton("Preview", FluentIcons.Common.Icon.DocumentBulletList);
        _markingPreparationPreviewButton = preview;
        preview.Click += (_, _) =>
        {
            if (SelectedMarkingPreparation() is { } report) OpenMarkingPreparationPreview(report);
        };
        var exportExcel = new Button
        {
            Content = new ExcelIcon { Width = 20, Height = 20 },
            Width = UiTokens.ToolbarButtonSize + 2,
            MinWidth = UiTokens.ToolbarButtonSize + 2,
            Height = UiTokens.ButtonHeight,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Export to Excel (.xlsx)",
            Style = (Style)FindResource(typeof(Button))
        };
        ApplyToolbarButtonHoverEffect(exportExcel);
        System.Windows.Automation.AutomationProperties.SetName(exportExcel, "Export to Excel");
        _markingPreparationExportButton = exportExcel;
        exportExcel.Click += (_, _) =>
        {
            if (SelectedMarkingPreparation() is { } report) ExportMarkingPreparationToExcel(report);
        };
        var print = MarkingPreparationButton("Print A4", FluentIcons.Common.Icon.Print);
        _markingPreparationPrintButton = print;
        print.Click += (_, _) =>
        {
            if (SelectedMarkingPreparation() is { } report) PrintMarkingPreparationVisual(report);
        };
        var hasSelection = SelectedMarkingPreparation() is not null;
        edit.IsEnabled = hasSelection && !_session.IsPreview;
        addFromSi.IsEnabled = hasSelection && !_session.IsPreview;
        delete.IsEnabled = hasSelection && !_session.IsPreview;
        preview.IsEnabled = hasSelection;
        exportExcel.IsEnabled = hasSelection;
        print.IsEnabled = hasSelection;
        var create = MarkingPreparationButton("New report", primary: true);
        create.Click += async (_, _) => await CreateMarkingPreparationFromSiAsync();

        create.Margin = new Thickness(0, 0, 8, 0);
        edit.Margin = new Thickness(0, 0, 8, 0);
        addFromSi.Margin = new Thickness(0, 0, 8, 0);
        delete.Margin = new Thickness(0, 0, 8, 0);
        preview.Margin = new Thickness(0, 0, 8, 0);
        exportExcel.Margin = new Thickness(0, 0, 8, 0);
        print.Margin = new Thickness(0, 0, 8, 0);
        refresh.Margin = new Thickness(0);

        var headerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerActions.Children.Add(_markingPreparationMonthFilter);
        headerActions.Children.Add(searchBoxContainer);
        headerActions.Children.Add(create);
        headerActions.Children.Add(addFromSi);
        headerActions.Children.Add(edit);
        headerActions.Children.Add(delete);
        headerActions.Children.Add(preview);
        headerActions.Children.Add(exportExcel);
        headerActions.Children.Add(print);
        headerActions.Children.Add(refresh);

        var heroHeader = CreatePageHeroHeader(
            string.Empty,
            "Marking Preparation Report",
            "Export marking specifications, container lot manifests, and package markings.",
            FluentIcons.Common.Icon.DocumentBulletList,
            headerActions);
        heroHeader.Tag = null;
        Grid.SetRow(heroHeader, 0);
        root.Children.Add(heroHeader);

        var alertContent = new DockPanel();
        _markingPreparationSIAlertButton = new Button
        {
            Content = "Review SI changes",
            MinWidth = 150,
            Height = 32,
            Padding = new Thickness(12, 3, 12, 3),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _markingPreparationSIAlertButton.Click += async (_, _) => await OpenMarkingPreparationSIChangesAsync();
        DockPanel.SetDock(_markingPreparationSIAlertButton, Dock.Right);
        alertContent.Children.Add(_markingPreparationSIAlertButton);
        _markingPreparationSIAlertText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = CreateBrush("#9A3412"),
            FontWeight = FontWeights.SemiBold
        };
        alertContent.Children.Add(_markingPreparationSIAlertText);
        _markingPreparationSIAlert = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 8, 0, 0),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = CreateBrush("#FDBA74"),
            Background = CreateBrush("#FFF7ED"),
            Child = alertContent,
            Visibility = Visibility.Collapsed
        };
        Grid.SetRow(_markingPreparationSIAlert, 1);
        root.Children.Add(_markingPreparationSIAlert);

        var splitGrid = new Grid
        {
            Margin = new Thickness(0, 6, 0, 0)
        };
        splitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 110 });
        splitGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        splitGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 110 });

        var topContainer = new Grid();
        topContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        topContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _markingPreparationMessage = Label(
            _markingPreparationLoading ? "Loading marking preparation reports…" :
            _session.IsPreview ? "Preview mode · reports are read-only." : "",
            12, (Brush)FindResource("MutedBrush"));
        _markingPreparationMessage.Margin = new Thickness(0, 0, 0, 4);
        Grid.SetRow(_markingPreparationMessage, 0);
        topContainer.Children.Add(_markingPreparationMessage);

        _markingPreparationReportGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        ApplySoftDataGridTheme(_markingPreparationReportGrid);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "No", nameof(MarkingPreparationListItem.No), .38, centered: true);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Report month", nameof(MarkingPreparationListItem.MonthLabel), 1.1);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Revision", nameof(MarkingPreparationListItem.RevisionLabel), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "Items", nameof(MarkingPreparationListItem.DetailCount), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationReportGrid, "SI review", nameof(MarkingPreparationListItem.SiReviewLabel), .9, centered: true);
        AddMarkingPreparationActionColumn(_markingPreparationReportGrid);
        _markingPreparationReportGrid.SelectionChanged += (_, _) =>
        {
            RefreshMarkingPreparationDetailGrid();
            UpdateMarkingPreparationButtons();
        };
        _markingPreparationReportGrid.MouseDoubleClick += async (_, e) =>
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
            if (SelectedMarkingPreparation() is { } report)
            {
                await EditMarkingPreparationAsync(report, isReadOnly: true);
            }
        };
        _markingPreparationReportGrid.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
            new RoutedEventHandler((_, e) =>
            {
                var btn = e.Source as Button;
                if (btn is null && e.OriginalSource is DependencyObject d)
                {
                    while (d is not null && d is not Button)
                        d = VisualTreeHelper.GetParent(d);
                    btn = d as Button;
                }
                if (btn is not null && btn.DataContext is MarkingPreparationListItem report)
                {
                    if (Equals(btn.Tag, "Preview"))
                    {
                        OpenMarkingPreparationPreview(report);
                        e.Handled = true;
                    }
                    else if (Equals(btn.Tag, "ExportExcel"))
                    {
                        ExportMarkingPreparationToExcel(report);
                        e.Handled = true;
                    }
                    else if (Equals(btn.Tag, "Print"))
                    {
                        PrintMarkingPreparationVisual(report);
                        e.Handled = true;
                    }
                }
            }));
        Grid.SetRow(_markingPreparationReportGrid, 1);
        topContainer.Children.Add(_markingPreparationReportGrid);

        Grid.SetRow(topContainer, 0);
        splitGrid.Children.Add(topContainer);

        var splitter = new GridSplitter
        {
            Height = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeNS,
            ResizeDirection = GridResizeDirection.Rows
        };
        Grid.SetRow(splitter, 1);
        splitGrid.Children.Add(splitter);

        var bottomContainer = new Grid
        {
            Margin = new Thickness(0, 2, 0, 0)
        };
        bottomContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        bottomContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var detailsHeading = SectionHeading("Report details");
        detailsHeading.Margin = new Thickness(0, 2, 0, 4);
        Grid.SetRow(detailsHeading, 0);
        bottomContainer.Children.Add(detailsHeading);

        _markingPreparationDetailGrid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        ApplySoftDataGridTheme(_markingPreparationDetailGrid);
        var detailRowStyle = new Style(typeof(DataGridRow));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridRow)) is Style defaultRowStyle)
            detailRowStyle.BasedOn = defaultRowStyle;
        var pendingReviewTrigger = new DataTrigger
        {
            Binding = new Binding(nameof(MarkingPreparationDetailListItem.IsSiReviewPending)),
            Value = true
        };
        pendingReviewTrigger.Setters.Add(new Setter(Control.BackgroundProperty, CreateBrush("#FFF7ED")));
        pendingReviewTrigger.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#FDBA74")));
        _markingPreparationDetailGrid.RowStyle = detailRowStyle;
        detailRowStyle.Triggers.Add(pendingReviewTrigger);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "No", nameof(MarkingPreparationDetailListItem.No), .38, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Month", nameof(MarkingPreparationDetailListItem.Month), .7, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Joint contract", nameof(MarkingPreparationDetailListItem.Contract), 1.7);
        AddMarkingPreparationColumn(
            _markingPreparationDetailGrid,
            "SI source",
            nameof(MarkingPreparationDetailListItem.SiSource),
            .9,
            centered: true,
            tooltipBinding: nameof(MarkingPreparationDetailListItem.SiSourceTooltip));
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Code", nameof(MarkingPreparationDetailListItem.Code), .6, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Destination", nameof(MarkingPreparationDetailListItem.Destination), 1.05);
        AddMarkingPreparationGradeColumn(_markingPreparationDetailGrid, .65);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Unit", nameof(MarkingPreparationDetailListItem.Unit), .42, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Gross", nameof(MarkingPreparationDetailListItem.Gross), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Net", nameof(MarkingPreparationDetailListItem.Net), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Package", nameof(MarkingPreparationDetailListItem.Package), .65, centered: true);
        AddMarkingPreparationColumn(_markingPreparationDetailGrid, "Marking due", nameof(MarkingPreparationDetailListItem.DueDate), .78, centered: true);

        Grid.SetRow(_markingPreparationDetailGrid, 1);
        bottomContainer.Children.Add(_markingPreparationDetailGrid);

        Grid.SetRow(bottomContainer, 2);
        splitGrid.Children.Add(bottomContainer);

        Grid.SetRow(splitGrid, 2);
        root.Children.Add(splitGrid);

        _markingPreparationDetailGrid.MouseDoubleClick += async (_, e) =>
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
            if (SelectedMarkingPreparation() is { } report)
            {
                await EditMarkingPreparationAsync(report, isReadOnly: true);
            }
        };

        RefreshMarkingPreparationReportGrid();
        RefreshMarkingPreparationDetailGrid();
        return root;
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private Button MarkingPreparationButton(string title, FluentIcons.Common.Icon? icon = null, bool primary = false)
    {
        if (icon is { } iconName)
        {
            var iconButton = IconOnlyActionButton(title, iconName);
            iconButton.IsEnabled = !_session.IsPreview || title is "Refresh" or "Print A4" or "Preview";
            return iconButton;
        }
        var button = new Button
        {
            Content = title,
            MinWidth = 100,
            Height = 34,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(9, 3, 9, 3),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = title,
            Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button)),
            IsEnabled = !_session.IsPreview || title is "Refresh" or "Print A4" or "Preview"
        };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        return button;
    }

    private static void AddMarkingPreparationColumn(DataGrid grid, string header, string binding,
        double widthWeight, bool centered = false, string? tooltipBinding = null)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 2, 8, 2)));
        if (centered) style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        if (!string.IsNullOrWhiteSpace(tooltipBinding))
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(tooltipBinding)));

        var column = new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(binding),
            Width = new DataGridLength(widthWeight, DataGridLengthUnitType.Star),
            ElementStyle = style,
            HeaderStyle = CreateMarkingPreparationHeaderStyle(centered)
        };

        grid.Columns.Add(column);
    }

    private static void AddMarkingPreparationGradeColumn(DataGrid grid, double widthWeight)
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4));
        borderFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        borderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.SetValue(FrameworkElement.MinWidthProperty, 60d);
        borderFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(MarkingPreparationDetailListItem.GradeBackground)));
        borderFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(MarkingPreparationDetailListItem.GradeBorderBrush)));

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
        textFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        textFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        textFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        textFactory.SetValue(TextBlock.MarginProperty, new Thickness(6, 2, 6, 2));
        textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(MarkingPreparationDetailListItem.Grade)));
        textFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(MarkingPreparationDetailListItem.GradeForeground)));

        borderFactory.AppendChild(textFactory);

        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Grade",
            CellTemplate = new DataTemplate { VisualTree = borderFactory },
            Width = new DataGridLength(widthWeight, DataGridLengthUnitType.Star),
            HeaderStyle = CreateMarkingPreparationHeaderStyle(centered: true)
        });
    }

    private static Style CreateMarkingPreparationHeaderStyle(bool centered = false)
    {
        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        if (System.Windows.Application.Current?.TryFindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)) is Style defaultHeaderStyle)
            headerStyle.BasedOn = defaultHeaderStyle;

        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, CreateBrush("#F8FAFC")));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, CreateBrush("#475569")));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        headerStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
        headerStyle.Setters.Add(new Setter(Control.HeightProperty, 34d));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#E2E8F0")));
        headerStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        headerStyle.Setters.Add(new Setter(HorizontalContentAlignmentProperty, centered ? HorizontalAlignment.Center : HorizontalAlignment.Left));
        headerStyle.Setters.Add(new Setter(VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return headerStyle;
    }

    private static void ApplySoftDataGridTheme(DataGrid grid)
    {
        grid.Background = Brushes.White;
        grid.BorderBrush = CreateBrush("#E2E8F0");
        grid.BorderThickness = new Thickness(1);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        grid.HorizontalGridLinesBrush = CreateBrush("#E2E8F0");
        grid.VerticalGridLinesBrush = CreateBrush("#F1F5F9");
        grid.RowBackground = Brushes.White;
        grid.AlternatingRowBackground = CreateBrush("#F8FAFC");
        grid.RowHeight = 34;
        grid.RowHeaderWidth = 0;
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.FontSize = 13;

        // Custom soft selection highlights instead of harsh Windows system colors
        grid.Resources[SystemColors.HighlightBrushKey] = CreateBrush("#E6F4F1");
        grid.Resources[SystemColors.HighlightTextBrushKey] = CreateBrush("#0F172A");
        grid.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = CreateBrush("#F1F5F9");
        grid.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = CreateBrush("#0F172A");

        var cellStyle = new Style(typeof(DataGridCell));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridCell)) is Style defaultCellStyle)
            cellStyle.BasedOn = defaultCellStyle;
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        grid.CellStyle = cellStyle;
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
            .Select(item =>
            {
                var revCount = ReadJsonArray(item, "revisions").GetArrayLength();
                return new MarkingPreparationListItem(
                    item.Clone(),
                    ReadString(item, "id"),
                    ReadDate(item, "reportMonth") ?? DateTime.Today,
                    GetLatestRevisionLabel(item),
                    ReadJsonArray(item, "details").GetArrayLength(),
                    RevisionCount: revCount);
            })
            .OrderByDescending(item => item.ReportMonth)
            .ToList();
        if (_markingPreparationMessage is not null)
            _markingPreparationMessage.Text = $"{_markingPreparationReports.Count} report(s) · Select a month to review or print.";
        UpdateMarkingPreparationMonthFilter();
        RefreshMarkingPreparationReportGrid();
        RefreshMarkingPreparationDetailGrid();
        RefreshMarkingPreparationSIAlert();
    }

    private static string GetLatestRevisionLabel(JsonElement report)
    {
        var revisions = ReadJsonArray(report, "revisions");
        if (revisions.ValueKind != JsonValueKind.Array || revisions.GetArrayLength() == 0)
            return "Rev.00";

        string? lastRev = null;
        int maxNum = -1;

        foreach (var rev in revisions.EnumerateArray())
        {
            var rawRev = ReadString(rev, "revision").Trim();
            if (string.IsNullOrWhiteSpace(rawRev)) continue;

            lastRev = rawRev;
            if (int.TryParse(rawRev, out var n) && n > maxNum)
            {
                maxNum = n;
            }
        }

        if (maxNum >= 0)
            return $"Rev.{maxNum:D2}";

        if (!string.IsNullOrWhiteSpace(lastRev))
        {
            if (lastRev.StartsWith("Rev.", StringComparison.OrdinalIgnoreCase))
                return lastRev;
            if (lastRev.StartsWith("Rev", StringComparison.OrdinalIgnoreCase))
                return $"Rev.{lastRev[3..].TrimStart('.', ' ')}";
            if (lastRev.StartsWith("R", StringComparison.OrdinalIgnoreCase) && int.TryParse(lastRev[1..], out var rNum))
                return $"Rev.{rNum:D2}";
            return $"Rev.{lastRev}";
        }

        return "Rev.00";
    }

    private void UpdateMarkingPreparationMonthFilter()
    {
        if (_markingPreparationMonthFilter is null) return;
        var currentSelection = _markingPreparationMonthFilter.SelectedItem as string;

        var months = _markingPreparationReports
            .Select(r => new DateTime(r.ReportMonth.Year, r.ReportMonth.Month, 1))
            .Distinct()
            .OrderByDescending(d => d)
            .Select(d => d.ToString("MMM yyyy", CultureInfo.InvariantCulture))
            .ToList();

        var items = new List<string> { "All months" };
        items.AddRange(months);

        _markingPreparationMonthFilter.ItemsSource = items;
        if (currentSelection is not null && items.Contains(currentSelection))
            _markingPreparationMonthFilter.SelectedItem = currentSelection;
        else
            _markingPreparationMonthFilter.SelectedIndex = 0;
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
        var selectedMonth = _markingPreparationMonthFilter?.SelectedItem as string;
        var filterByMonth = !string.IsNullOrWhiteSpace(selectedMonth) &&
                            !string.Equals(selectedMonth, "All months", StringComparison.OrdinalIgnoreCase);

        var filtered = _markingPreparationReports
            .Where(item => !filterByMonth || string.Equals(item.MonthLabel, selectedMonth, StringComparison.OrdinalIgnoreCase))
            .Where(item => search.Length == 0 ||
                           item.MonthLabel.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           item.RevisionLabel.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select((item, index) => item with { No = index + 1 })
            .ToList();

        _markingPreparationReportGrid.ItemsSource = filtered;
        if (selectedId is not null)
            _markingPreparationReportGrid.SelectedItem = filtered.FirstOrDefault(item => item.Id == selectedId);
        if (_markingPreparationReportGrid.SelectedItem is null && filtered.Count > 0)
            _markingPreparationReportGrid.SelectedItem = filtered[0];

        RefreshMarkingPreparationDetailGrid();
        UpdateMarkingPreparationButtons();
        RefreshMarkingPreparationSIAlert();
    }

    private void RefreshMarkingPreparationSIAlert()
    {
        if (_markingPreparationSIAlert is null || _markingPreparationSIAlertText is null)
            return;

        var pendingReports = _markingPreparationReports
            .Where(report => report.PendingSiReviewCount > 0)
            .ToList();
        var pendingLines = pendingReports.Sum(report => report.PendingSiReviewCount);
        _markingPreparationSIAlert.Visibility = pendingLines > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (pendingLines > 0)
            _markingPreparationSIAlertText.Text =
                $"SI changes need review: {pendingReports.Count} report(s), {pendingLines} line(s). Marking values have not been changed automatically.";
    }

    private async Task OpenMarkingPreparationSIChangesAsync()
    {
        var reports = _markingPreparationReports
            .Select(report =>
            {
                var lines = ReadJsonArray(report.Raw, "details").EnumerateArray()
                    .Where(IsSourceSiReviewPending)
                    .Select(line => new MarkingPreparationSIChangeLine(
                        ReadString(line, "id"),
                        ReadString(line, "jointContractName"),
                        ReadSiRevisionLabel(line, "sourceSiRevision", "R00"),
                        ReadSiRevisionLabel(line, "sourceSiCurrentRevision", "Missing"),
                        ReadString(line, "sourceSiChangeFingerprint"),
                        ReadJsonArray(line, "sourceSiChanges").EnumerateArray()
                            .Select(change => new MarkingPreparationSIChangeField(
                                FormatSIFieldName(ReadString(change, "field")),
                                ReadString(change, "previousValue"),
                                ReadString(change, "currentValue")))
                            .ToList()))
                    .Where(line => line.Id.Length > 0)
                    .ToList();

                return new MarkingPreparationSIChangeReport(
                    report.Id,
                    report.MonthLabel,
                    report.RevisionLabel,
                    lines);
            })
            .Where(report => report.Lines.Count > 0)
            .ToList();

        if (reports.Count == 0)
        {
            MessageBox.Show(this, "There are no SI changes waiting for review.",
                "Review SI changes", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadMarkingPreparationReportsAsync();
            return;
        }

        var reviewWindow = new MarkingPreparationSIChangeReviewWindow(reports) { Owner = this };
        if (reviewWindow.ShowDialog() != true)
            return;

        if (reviewWindow.ReviewDecision is { } decision && reviewWindow.LinesToReview.Count > 0)
        {
            var reportId = reviewWindow.ReviewReportId;
            if (reportId is null)
                return;

            var payload = JsonSerializer.Serialize(new
            {
                decision,
                lines = reviewWindow.LinesToReview.Select(line => new
                {
                    lineId = line.Id,
                    sourceFingerprint = line.Fingerprint
                })
            });
            var result = await _apiClient.PostJsonAsync(
                $"{MarkingPreparationRoute}/{Uri.EscapeDataString(reportId)}/si-change-reviews",
                payload,
                _session.Token);
            if (!result.Success)
            {
                MessageBox.Show(this,
                    string.IsNullOrWhiteSpace(result.Error)
                        ? "Could not save the SI review. Refresh the report and try again."
                        : result.Error,
                    "Review SI changes", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await LoadMarkingPreparationReportsAsync();
            MessageBox.Show(this,
                $"Recorded the review for {reviewWindow.LinesToReview.Count} line(s).",
                "Review SI changes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (reviewWindow.OpenReportId is { } selectedReportId)
        {
            var report = _markingPreparationReports.FirstOrDefault(item => item.Id == selectedReportId);
            if (report is not null && _markingPreparationReportGrid is not null)
            {
                _markingPreparationReportGrid.SelectedItem = report;
                _markingPreparationReportGrid.ScrollIntoView(report);
                await EditMarkingPreparationAsync(report, isReadOnly: false);
            }
        }
    }

    private static string ReadSiRevisionLabel(JsonElement line, string property, string missingLabel)
    {
        if (!line.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return missingLabel;
        return value.TryGetInt32(out var revision) ? $"R{revision:D2}" : value.ToString();
    }

    private static string FormatSIFieldName(string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return "SI field";
        var result = new System.Text.StringBuilder(field.Length + 8);
        for (var index = 0; index < field.Length; index++)
        {
            var character = field[index];
            if (index > 0 && char.IsUpper(character) && !char.IsUpper(field[index - 1]))
                result.Append(' ');
            result.Append(character);
        }
        return char.ToUpperInvariant(result[0]) + result.ToString()[1..];
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
            MarkingPreparationCatalogHelper.CleanValue(ReadString(line, "code")),
            MarkingPreparationCatalogHelper.CleanValue(ReadString(line, "destination")),
            ReadString(line, "grade"),
            ReadString(line, "unit"),
            FormatWeight(line, "grossWeight"),
            FormatWeight(line, "netWeight"),
            ReadString(line, "packageType"),
            FormatDueDate(line, "markingDueDate"),
            ReadString(line, "markingSign"),
            FormatDate(line, "statusDate"),
            FormatDate(line, "etdDate"),
            GetMarkingSiSourceLabel(line),
            IsSourceSiReviewPending(line),
            GetMarkingSiReviewTooltip(line))).ToList();
    }

    private static string GetMarkingSiSourceLabel(JsonElement line)
    {
        if (ReadBoolean(line, "sourceSiChanged"))
        {
            if (IsSourceSiReviewPending(line)) return "SI changed · needs review";
            var decision = ReadString(line, "sourceSiReviewDecision");
            if (string.Equals(decision, "UPDATED", StringComparison.OrdinalIgnoreCase))
                return "SI changed · reviewed (updated)";
            if (string.Equals(decision, "KEPT", StringComparison.OrdinalIgnoreCase))
                return "SI changed · reviewed (kept)";
            return "SI changed · reviewed";
        }
        var status = ReadString(line, "sourceSiLinkStatus");
        if (string.Equals(status, "LINKED", StringComparison.OrdinalIgnoreCase))
        {
            var revision = ReadInt(line, "sourceSiRevision");
            return $"SI R{revision:D2}";
        }
        if (string.Equals(status, "NEEDS_REVIEW", StringComparison.OrdinalIgnoreCase)) return "Review match";
        return "Legacy";
    }

    private static bool IsSourceSiReviewPending(JsonElement line)
    {
        if (line.TryGetProperty("sourceSiReviewPending", out var pending))
            return pending.ValueKind == JsonValueKind.True;
        return ReadBoolean(line, "sourceSiChanged");
    }

    private static string GetMarkingSiReviewTooltip(JsonElement line)
    {
        if (!ReadBoolean(line, "sourceSiChanged"))
        {
            var revision = ReadInt(line, "sourceSiRevision");
            return $"Linked to SI revision R{revision:D2}.";
        }

        var previousRevision = ReadInt(line, "sourceSiRevision");
        var currentRevision = ReadSiRevisionLabel(line, "sourceSiCurrentRevision", "Missing");
        if (IsSourceSiReviewPending(line))
            return $"SI changed from R{previousRevision:D2} to {currentRevision}. Review the changes and record a decision.";

        var decision = ReadString(line, "sourceSiReviewDecision");
        var reviewer = ReadString(line, "sourceSiReviewedBy");
        var reviewedAtText = ReadString(line, "sourceSiReviewedAt");
        var reviewedAt = DateTimeOffset.TryParse(
            reviewedAtText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed.ToLocalTime().ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture)
            : reviewedAtText;
        var review = string.IsNullOrWhiteSpace(decision) ? "Reviewed" : $"Reviewed as {decision}";
        var audit = new List<string>();
        if (!string.IsNullOrWhiteSpace(reviewer)) audit.Add($"by {reviewer}");
        if (!string.IsNullOrWhiteSpace(reviewedAt)) audit.Add($"on {reviewedAt}");
        return $"SI changed from R{previousRevision:D2} to {currentRevision}. {review}" +
            (audit.Count > 0 ? $" {string.Join(" ", audit)}." : ".");
    }

    private static bool ReadBoolean(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static int ReadInt(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string FormatWeight(JsonElement line, string property)
    {
        string[] candidates = property switch
        {
            "grossWeight" => ["grossWeight", "gross_weight", "GrossWeight", "gross", "Gross", "gross_weight_mt", "grossWeightMt"],
            "netWeight" => ["netWeight", "net_weight", "NetWeight", "net", "Net", "net_weight_mt", "netWeightMt"],
            _ => [property]
        };

        foreach (var key in candidates)
        {
            if (!line.TryGetProperty(key, out var value)) continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
                return number.ToString("N0", CultureInfo.InvariantCulture);

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed.ToString("N0", CultureInfo.InvariantCulture);
        }
        return "";
    }

    private static string FormatDate(JsonElement item, string property, string format = "dd/MM/yyyy")
        => ReadDate(item, property)?.ToString(format, CultureInfo.InvariantCulture) ?? "";

    private static string FormatDueDate(JsonElement item, string property)
        => FormatDate(item, property, "dd-MMM-yyyy");

    private void UpdateMarkingPreparationButtons()
    {
        var enabled = SelectedMarkingPreparation() is not null;
        if (_markingPreparationEditButton is not null) _markingPreparationEditButton.IsEnabled = enabled && !_session.IsPreview;
        if (_markingPreparationAddFromSiButton is not null) _markingPreparationAddFromSiButton.IsEnabled = enabled && !_session.IsPreview;
        if (_markingPreparationDeleteButton is not null) _markingPreparationDeleteButton.IsEnabled = enabled && !_session.IsPreview;
        if (_markingPreparationPreviewButton is not null) _markingPreparationPreviewButton.IsEnabled = enabled;
        if (_markingPreparationExportButton is not null) _markingPreparationExportButton.IsEnabled = enabled;
        if (_markingPreparationPrintButton is not null) _markingPreparationPrintButton.IsEnabled = enabled;
    }

    private static void AddMarkingPreparationActionColumn(DataGrid grid)
    {
        var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
        panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panelFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        panelFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        // Preview icon button
        var previewBtn = new FrameworkElementFactory(typeof(Button));
        previewBtn.SetValue(FrameworkElement.TagProperty, "Preview");
        previewBtn.SetValue(FrameworkElement.WidthProperty, 26d);
        previewBtn.SetValue(FrameworkElement.HeightProperty, 26d);
        previewBtn.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        previewBtn.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        previewBtn.SetValue(FrameworkElement.ToolTipProperty, "Preview A4 document");
        previewBtn.SetValue(Control.PaddingProperty, new Thickness(0));
        previewBtn.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
        previewBtn.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);

        var previewIcon = new FrameworkElementFactory(typeof(FluentIcon));
        previewIcon.SetValue(FluentIcon.IconProperty, FluentIcons.Common.Icon.DocumentBulletList);
        previewIcon.SetValue(FluentIcon.IconSizeProperty, IconSize.Size16);
        previewIcon.SetValue(FluentIcon.ForegroundProperty, CreateBrush("#0F766E"));
        previewIcon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        previewIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        previewBtn.AppendChild(previewIcon);

        // Print icon button
        var printBtn = new FrameworkElementFactory(typeof(Button));
        printBtn.SetValue(FrameworkElement.TagProperty, "Print");
        printBtn.SetValue(FrameworkElement.WidthProperty, 26d);
        printBtn.SetValue(FrameworkElement.HeightProperty, 26d);
        printBtn.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        printBtn.SetValue(FrameworkElement.ToolTipProperty, "Print report directly");
        printBtn.SetValue(Control.PaddingProperty, new Thickness(0));
        printBtn.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center);
        printBtn.SetValue(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center);

        var printIcon = new FrameworkElementFactory(typeof(FluentIcon));
        printIcon.SetValue(FluentIcon.IconProperty, FluentIcons.Common.Icon.Print);
        printIcon.SetValue(FluentIcon.IconSizeProperty, IconSize.Size16);
        printIcon.SetValue(FluentIcon.ForegroundProperty, CreateBrush("#475569"));
        printIcon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        printIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        printBtn.AppendChild(printIcon);

        panelFactory.AppendChild(previewBtn);
        panelFactory.AppendChild(printBtn);

        var template = new DataTemplate { VisualTree = panelFactory };

        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Actions",
            CellTemplate = template,
            Width = new DataGridLength(90),
            HeaderStyle = CreateMarkingPreparationHeaderStyle(centered: true)
        });
    }

    private async Task EditMarkingPreparationAsync(MarkingPreparationListItem? report, bool isReadOnly = false)
        => await EditMarkingPreparationAsync(report, isReadOnly, null);

    private async Task EditMarkingPreparationAsync(
        MarkingPreparationListItem? report,
        bool isReadOnly,
        IReadOnlyList<JsonElement>? selectedSiLines)
    {
        if (!isReadOnly && _session.IsPreview)
        {
            MessageBox.Show(this, "Connect with an account to create or edit reports.", "Marking Preparation",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var catalogResult = await _apiClient.GetAsync($"{MarkingPreparationRoute}/catalog", _session.Token);
        if (!catalogResult.Success || catalogResult.Data is not { ValueKind: JsonValueKind.Object } catalog)
        {
            MessageBox.Show(this,
                string.IsNullOrWhiteSpace(catalogResult.Error)
                    ? "Could not load the Code, Destination, Grade and Package choices from the backend."
                    : catalogResult.Error,
                "Marking Preparation catalog", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MarkingPreparationEditorWindow editor;
        try
        {
            var defaultUser = _session.User?.FirstName;
            if (string.IsNullOrWhiteSpace(defaultUser))
                defaultUser = _session.User?.FullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? _session.User?.Username ?? "admin";
            editor = new MarkingPreparationEditorWindow(
                report?.Raw,
                catalog,
                defaultCreatedBy: defaultUser,
                isReadOnly: isReadOnly,
                selectedSiLines: selectedSiLines);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Marking Preparation catalog",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        editor.Owner = this;
        if (editor.ShowDialog() != true || editor.Payload is null || isReadOnly) return;
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

    private async Task CreateMarkingPreparationFromSiAsync()
        => await SelectAndEditMarkingPreparationFromSiAsync(null);

    private async Task AddMarkingPreparationLinesFromSiAsync()
    {
        var report = SelectedMarkingPreparation();
        if (report is null || _session.IsPreview) return;
        await SelectAndEditMarkingPreparationFromSiAsync(report);
    }

    private async Task SelectAndEditMarkingPreparationFromSiAsync(MarkingPreparationListItem? report)
    {
        if (_session.IsPreview) return;
        var endpoint = $"{MarkingPreparationRoute}/si-candidates";
        if (report is not null)
            endpoint += $"?reportMonth={report.ReportMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var result = await _apiClient.GetAsync(endpoint, _session.Token);
        if (!result.Success || result.Data is not { ValueKind: JsonValueKind.Object } candidates)
        {
            MessageBox.Show(this,
                string.IsNullOrWhiteSpace(result.Error) ? "Could not load available SI lines." : result.Error,
                "Select SI lines", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var picker = new MarkingPreparationSISourceWindow(candidates, report?.ReportMonth);
        picker.Owner = this;
        if (picker.ShowDialog() != true || picker.SelectedLines.Count == 0) return;
        if (report is null && picker.SelectedLines.Count > 0)
        {
            var selectedMonth = ReadDate(picker.SelectedLines[0], "shipmentMonth");
            if (selectedMonth is { } month)
                report = _markingPreparationReports.FirstOrDefault(item =>
                    item.ReportMonth.Year == month.Year && item.ReportMonth.Month == month.Month);
            if (report is not null)
                MessageBox.Show(this,
                    $"A Marking report already exists for {report.MonthLabel}. The selected SI lines will be added as a new revision.",
                    "Add SI lines", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        await EditMarkingPreparationAsync(report, isReadOnly: false, selectedSiLines: picker.SelectedLines);
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
        PrintMarkingPreparationVisual(report);
    }

    private void LegacyPrintMarkingPreparation(MarkingPreparationListItem report)
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

    private Table BuildMarkingPreparationPrintHeader(JsonElement report)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 0, 0, 8) };
        table.Columns.Add(new TableColumn { Width = new GridLength(225) });
        table.Columns.Add(new TableColumn { Width = new GridLength(516) });
        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        var month = ReadDate(report, "reportMonth")?.ToString("MMM-yy", CultureInfo.InvariantCulture) ?? "";
        var creator = ExtractFirstName(ReadString(report, "createdBy"));
        if (string.IsNullOrWhiteSpace(creator)) creator = ExtractFirstName(ReadString(report, "printedBy"));
        if (string.IsNullOrWhiteSpace(creator)) creator = _session.User?.FirstName ?? ExtractFirstName(_session.User?.FullName) ?? _session.User?.Username ?? "admin";

        var left = new TableCell();
        left.BorderThickness = new Thickness(0);
        left.Blocks.Add(new Paragraph(new Run($"User : {creator}"))
        {
            FontSize = 6,
            Margin = new Thickness(0, 0, 0, 4)
        });
        left.Blocks.Add(new Paragraph(new Run($"Report Month :    {month}"))
        {
            Margin = new Thickness(0, 0, 4, 0),
            FontWeight = FontWeights.SemiBold,
            FontSize = 8
        });

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
            var gradeVal = ReadString(line, "grade");
            var row = new TableRow();
            row.Cells.Add(CreateMarkingPreparationPrintCell(no.ToString(CultureInfo.InvariantCulture), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(ReadString(line, "monthLabel"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(ReadString(line, "jointContractName"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(MarkingPreparationCatalogHelper.CleanValue(ReadString(line, "code")), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(MarkingPreparationCatalogHelper.CleanValue(ReadString(line, "destination")), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(gradeVal, false, GradeColorHelper.GetBackgroundBrush(gradeVal), GradeColorHelper.GetForegroundBrush(gradeVal)));
            row.Cells.Add(CreateMarkingPreparationPrintCell(ReadString(line, "unit"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(FormatWeight(line, "grossWeight"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(FormatWeight(line, "netWeight"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(ReadString(line, "packageType"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(FormatDueDate(line, "markingDueDate"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(ReadString(line, "markingSign"), false));
            row.Cells.Add(CreateMarkingPreparationPrintCell(FormatDate(line, "statusDate"), false));
            group.Rows.Add(row);
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

    private static TableCell CreateMarkingPreparationPrintCell(string value, bool header, Brush? background = null, Brush? foreground = null)
    {
        var run = new Run(value);
        if (foreground is not null) run.Foreground = foreground;
        var paragraph = new Paragraph(run)
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
            Background = background ?? Brushes.White,
            FontSize = header ? 6.8 : 6.5,
            FontWeight = (header || foreground is not null) ? FontWeights.SemiBold : FontWeights.Normal
        };
    }

    private void ExportMarkingPreparationToExcel(MarkingPreparationListItem report)
    {
        try
        {
            var defaultFileName = $"Marking_Preparation_Report_{report.ReportMonth:yyyyMM}.xlsx";
            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Marking Preparation Report",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx|CSV File (*.csv)|*.csv",
                FileName = defaultFileName
            };

            if (sfd.ShowDialog(this) == true)
            {
                var ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                if (ext == ".csv")
                {
                    MarkingPreparationExcelExportService.ExportToCsv(report.Raw, sfd.FileName);
                }
                else
                {
                    MarkingPreparationExcelExportService.ExportToXlsx(report.Raw, report.MonthLabel, sfd.FileName);
                }

                var result = MessageBox.Show(
                    this,
                    $"Exported successfully to:\n{sfd.FileName}\n\nWould you like to open the file now?",
                    "Export Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = sfd.FileName,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Could not open file: {ex.Message}", "Open File Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error exporting to Excel: {ex.Message}", "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}


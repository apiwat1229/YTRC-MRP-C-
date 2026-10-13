using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Data;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

internal sealed class MarkingPreparationEditorWindow : Window
{
    private const double EditorDatePickerWidth = UiTokens.DatePickerWidth;
    private const double EditorDatePickerHeight = UiTokens.DatePickerHeight;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly EditorState _state;
    private readonly ObservableCollection<RevisionDraft> _revisions;
    private readonly ObservableCollection<DetailDraft> _details;
    private readonly Dictionary<string, IReadOnlyList<string>> _codeDestinations;
    private readonly IReadOnlyList<string> _codeOptions;
    private readonly IReadOnlyList<string> _gradeOptions;
    private readonly IReadOnlyList<string> _packageOptions;
    private readonly bool _isNewReport;
    private readonly string _originalDetailFingerprint;
    private readonly HashSet<string> _originalRevisionNumbers;
    private readonly BookingQueueDatePicker _reportMonth;
    private readonly DataGrid _revisionGrid;
    private readonly DataGrid _detailGrid;
    private TextBlock _totalLinesText = null!;
    private TextBlock _totalGrossText = null!;
    private TextBlock _totalNetText = null!;
    private TextBlock _detailLineCountBadge = null!;

    public object? Payload { get; private set; }

    public MarkingPreparationEditorWindow(JsonElement? source, JsonElement catalog, string defaultCreatedBy = "", string defaultPrintedBy = "")
    {
        (_codeDestinations, _codeOptions, _gradeOptions, _packageOptions) = ReadCatalog(catalog);
        _isNewReport = source is null;
        _state = source is { ValueKind: JsonValueKind.Object } json
            ? JsonSerializer.Deserialize<EditorState>(json.GetRawText(), JsonOptions) ?? new EditorState()
            : new EditorState
            {
                ReportMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                CreatedBy = defaultCreatedBy,
                PrintedBy = defaultPrintedBy,
                Revisions = [new RevisionDraft { Revision = "00", ReportDate = DateTime.Today }]
            };
        if (string.IsNullOrWhiteSpace(_state.CreatedBy) && !string.IsNullOrWhiteSpace(defaultCreatedBy))
        {
            _state.CreatedBy = defaultCreatedBy;
        }
        _state.ReportMonth = _state.ReportMonth == default
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : new DateTime(_state.ReportMonth.Year, _state.ReportMonth.Month, 1);
        _state.Revisions ??= [];
        _state.Details ??= [];
        if (_state.Revisions.Count == 0)
        {
            _state.Revisions.Add(new RevisionDraft
            {
                Revision = "00",
                ReportDate = _isNewReport ? DateTime.Today : _state.CreatedAt?.Date ?? DateTime.Today
            });
        }
        _revisions = new ObservableCollection<RevisionDraft>(_state.Revisions);
        _details = new ObservableCollection<DetailDraft>(_state.Details);
        foreach (var detail in _details)
            detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
        if (_isNewReport && _details.Count == 0)
        {
            var firstDetail = new DetailDraft();
            firstDetail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
            _details.Add(firstDetail);
        }
        _originalDetailFingerprint = DetailFingerprint(_details);
        _originalRevisionNumbers = _revisions
            .Where(item => !string.IsNullOrWhiteSpace(item.Revision))
            .Select(item => item.Revision!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Title = source is null ? "New Marking Preparation Report" : "Edit Marking Preparation Report";
        Width = 1420;
        Height = 780;
        MinWidth = 1080;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = Brush("#F8FAFC");

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Top Header Card
        var headerCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var headerDock = new DockPanel();

        var rightControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rightControls, Dock.Right);

        _reportMonth = CreateDatePicker(_state.ReportMonth);
        _reportMonth.MonthSelectionOnly = true;
        _reportMonth.DateDisplayFormat = "MMM yyyy";
        _reportMonth.ToolTip = "Report month · first day of the month";
        _reportMonth.Margin = new Thickness(0);
        var monthStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        monthStack.Children.Add(new TextBlock
        {
            Text = "Report Month",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        monthStack.Children.Add(_reportMonth);
        rightControls.Children.Add(monthStack);
        headerDock.Children.Add(rightControls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBorder = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = UiTokens.CornerRadiusSmall,
            Background = UiTokens.Brushes.MintSurface,
            BorderBrush = UiTokens.Brushes.MintBorder,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new FluentIcon
            {
                Icon = FluentIcons.Common.Icon.DocumentBulletList,
                IconSize = IconSize.Size20,
                IconVariant = IconVariant.Regular,
                Foreground = UiTokens.Brushes.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        leftStack.Children.Add(iconBorder);

        var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBlock.Children.Add(new TextBlock
        {
            Text = _isNewReport ? "New Marking Preparation Report" : "Edit Marking Preparation Report",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#0F172A"),
            FontFamily = (FontFamily)FindResource("AppFontFamily")
        });
        leftStack.Children.Add(titleBlock);
        headerDock.Children.Add(leftStack);

        headerCard.Child = headerDock;
        root.Children.Add(headerCard);
        Grid.SetRow(headerCard, 0);

        // Row 1: Revision History Card
        var revisionCard = BuildRevisionSection(out _revisionGrid);
        root.Children.Add(revisionCard);
        Grid.SetRow(revisionCard, 1);

        // Row 2: Marking Details Card
        var detailCard = BuildDetailSection(out _detailGrid);
        root.Children.Add(detailCard);
        Grid.SetRow(detailCard, 2);

        // Row 3: Footer Bar
        var footerCard = BuildFooterSection();
        root.Children.Add(footerCard);
        Grid.SetRow(footerCard, 3);

        _details.CollectionChanged += (_, _) => UpdateFooterMetrics();
        _detailGrid.CellEditEnding += (_, _) => Dispatcher.InvokeAsync(UpdateFooterMetrics);
        UpdateFooterMetrics();

        Content = root;
    }

    private Border BuildRevisionSection(out DataGrid grid)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var gridLayout = new Grid();
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(controls, Dock.Right);

        var add = CreateButton("Add revision");
        add.Click += (_, _) => _revisions.Add(new RevisionDraft { Revision = NextRevision(_revisions), ReportDate = DateTime.Today });

        DataGrid? localGrid = null;
        var remove = CreateButton("Remove revision");
        remove.Click += (_, _) =>
        {
            if (localGrid?.SelectedItem is not RevisionDraft selected) return;
            if (selected.Revision == "00")
            {
                ShowValidation("The initial revision 00 must remain in the report.");
                return;
            }
            _revisions.Remove(selected);
        };
        controls.Children.Add(add);
        controls.Children.Add(remove);
        header.Children.Add(controls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftStack.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.History,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = Brush("#0F766E"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Revision History",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Report publication dates and modification logs",
            FontSize = 11,
            Foreground = Brush("#64748B"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(leftStack);

        gridLayout.Children.Add(header);
        Grid.SetRow(header, 0);

        grid = new DataGrid
        {
            ItemsSource = _revisions,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = Brush("#F8FAFC"),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = Brush("#F1F5F9"),
            VerticalGridLinesBrush = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            FontSize = 12,
            RowHeight = 30,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        localGrid = grid;
        AddRevisionColumns(grid);
        EnableSingleClickEditing(grid);

        gridLayout.Children.Add(grid);
        Grid.SetRow(grid, 1);

        card.Child = gridLayout;
        return card;
    }

    private Border BuildDetailSection(out DataGrid grid)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var gridLayout = new Grid();
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(controls, Dock.Right);

        _detailLineCountBadge = new TextBlock
        {
            Text = _details.Count == 1 ? "1 line" : $"{_details.Count} lines",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#475569"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeBorder = new Border
        {
            Background = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _detailLineCountBadge
        };
        controls.Children.Add(badgeBorder);

        var add = CreateButton("Add line");
        add.Click += (_, _) =>
        {
            var detail = new DetailDraft();
            detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
            _details.Add(detail);
        };

        DataGrid? localGrid = null;
        var remove = CreateButton("Remove line");
        remove.Click += (_, _) =>
        {
            if (localGrid?.SelectedItem is DetailDraft selected)
                _details.Remove(selected);
        };
        controls.Children.Add(add);
        controls.Children.Add(remove);
        header.Children.Add(controls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftStack.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Table,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = Brush("#0F766E"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Marking Details & Specifications",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(leftStack);

        gridLayout.Children.Add(header);
        Grid.SetRow(header, 0);

        grid = new DataGrid
        {
            ItemsSource = _details,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = Brush("#F8FAFC"),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = Brush("#F1F5F9"),
            VerticalGridLinesBrush = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            FontSize = 12,
            RowHeight = 34,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        localGrid = grid;
        grid.InitializingNewItem += (_, eventArgs) =>
        {
            if (eventArgs.NewItem is DetailDraft detail)
                detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
        };
        grid.LoadingRow += (_, eventArgs) => eventArgs.Row.Header = (eventArgs.Row.GetIndex() + 1).ToString(CultureInfo.InvariantCulture);
        AddDetailColumns(grid);
        EnableSingleClickEditing(grid);

        gridLayout.Children.Add(grid);
        Grid.SetRow(grid, 1);

        card.Child = gridLayout;
        return card;
    }

    private Border BuildFooterSection()
    {
        var footerCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(14, 10, 14, 10)
        };

        var dock = new DockPanel();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(buttons, Dock.Right);

        var cancel = CreateButton("Cancel");
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var save = CreateButton("Save report", primary: true);
        save.Click += (_, _) => SaveReport();

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        dock.Children.Add(buttons);

        var metrics = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _totalLinesText = new TextBlock { Text = _details.Count.ToString(CultureInfo.InvariantCulture), FontWeight = FontWeights.Bold, Foreground = Brush("#0F172A") };
        _totalGrossText = new TextBlock { Text = "0", FontWeight = FontWeights.Bold, Foreground = Brush("#0F172A") };
        _totalNetText = new TextBlock { Text = "0", FontWeight = FontWeights.Bold, Foreground = Brush("#0F766E") };

        metrics.Children.Add(CreateMetricPill("Lines", _totalLinesText));
        metrics.Children.Add(CreateMetricPill("Total Gross", _totalGrossText, " MT"));
        metrics.Children.Add(CreateMetricPill("Total Net", _totalNetText, " MT"));

        dock.Children.Add(metrics);
        footerCard.Child = dock;
        return footerCard;
    }

    private static Border CreateMetricPill(string label, TextBlock valueBlock, string suffix = "")
    {
        var border = new Border
        {
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 11.5,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(valueBlock);
        if (!string.IsNullOrEmpty(suffix))
        {
            stack.Children.Add(new TextBlock
            {
                Text = suffix,
                FontSize = 11,
                Foreground = Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        border.Child = stack;
        return border;
    }

    private void UpdateFooterMetrics()
    {
        var count = _details.Count;
        var totalGross = _details.Sum(d => d.GrossWeight ?? 0);
        var totalNet = _details.Sum(d => d.NetWeight ?? 0);

        if (_totalLinesText is not null)
            _totalLinesText.Text = count.ToString(CultureInfo.InvariantCulture);
        if (_totalGrossText is not null)
            _totalGrossText.Text = totalGross.ToString("N0", CultureInfo.InvariantCulture);
        if (_totalNetText is not null)
            _totalNetText.Text = totalNet.ToString("N0", CultureInfo.InvariantCulture);
        if (_detailLineCountBadge is not null)
            _detailLineCountBadge.Text = count == 1 ? "1 line" : $"{count} lines";
    }

    private static Style CreateHeaderStyle(HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var style = new Style(typeof(DataGridColumnHeader));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridColumnHeader)) is Style defaultStyle)
            style.BasedOn = defaultStyle;
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#F8FAFC")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#475569")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11.5d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, alignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 6, 5)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#E2E8F0")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        return style;
    }

    private static void AddRevisionColumns(DataGrid grid)
    {
        var binding = new Binding(nameof(RevisionDraft.Revision))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        };
        var textStyle = new Style(typeof(TextBlock));
        textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#0F766E")));
        textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        textStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var remarkTextStyle = new Style(typeof(TextBlock));
        remarkTextStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
        remarkTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
        remarkTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var remarkEditStyle = new Style(typeof(TextBox));
        remarkEditStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        remarkEditStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
        remarkEditStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        remarkEditStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        remarkEditStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var editStyle = new Style(typeof(TextBox));
        editStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
        editStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        var remarkCellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch);

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Revision",
            Binding = binding,
            Width = new DataGridLength(75),
            ElementStyle = textStyle,
            EditingElementStyle = editStyle,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Center),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            IsReadOnly = true
        });
        AddDateColumn(grid, "Date Report", nameof(RevisionDraft.ReportDate));
        AddTextColumn(grid, "Remark", nameof(RevisionDraft.Remark), new DataGridLength(1, DataGridLengthUnitType.Star),
            elementStyle: remarkTextStyle, editingElementStyle: remarkEditStyle, cellStyle: remarkCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Left));
    }

    private void AddDetailColumns(DataGrid grid)
    {
        var detailTextStyle = new Style(typeof(TextBlock));
        detailTextStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
        detailTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
        detailTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var centeredDetailTextStyle = new Style(typeof(TextBlock), detailTextStyle);
        centeredDetailTextStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        var detailEditStyle = new Style(typeof(TextBox));
        detailEditStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        detailEditStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
        detailEditStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        detailEditStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        detailEditStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var centeredDetailEditStyle = new Style(typeof(TextBox), detailEditStyle);
        centeredDetailEditStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
        var detailCellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch);

        AddRowNumberColumn(grid);
        AddMonthColumn(grid);
        AddTextColumn(grid, "Joint Contract Name", nameof(DetailDraft.JointContractName),
            new DataGridLength(1, DataGridLengthUnitType.Star), minWidth: 160,
            elementStyle: detailTextStyle, editingElementStyle: detailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Left));
        AddChoiceColumn(grid, "Code", nameof(DetailDraft.Code), nameof(DetailDraft.CodeOptions), 110);
        AddChoiceColumn(grid, "Dest.", nameof(DetailDraft.Destination), nameof(DetailDraft.DestinationOptions), 165);
        AddGradeChoiceColumn(grid, 120);
        AddTextColumn(grid, "Unit", nameof(DetailDraft.Unit), 65,
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddTextColumn(grid, "Gross (MT)", nameof(DetailDraft.GrossWeight), 95, "N0",
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddTextColumn(grid, "Net (MT)", nameof(DetailDraft.NetWeight), 95, "N0",
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddChoiceColumn(grid, "Package", nameof(DetailDraft.PackageType), nameof(DetailDraft.PackageOptions), 125);
        AddDateColumn(grid, "Marking Due", nameof(DetailDraft.MarkingDueDate));
    }

    private static void AddGradeChoiceColumn(DataGrid grid, double width)
    {
#pragma warning disable CS0618
        var boxFactory = new FrameworkElementFactory(typeof(ClearableComboBox));
        boxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
        boxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        boxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(DetailDraft.GradeBackground)));
        boxFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(DetailDraft.GradeBorderBrush)));
        boxFactory.SetBinding(ClearableComboBox.ItemForegroundProperty, new Binding(nameof(DetailDraft.GradeForeground)));
        boxFactory.SetValue(ClearableComboBox.ItemFontWeightProperty, FontWeights.Bold);
        boxFactory.SetBinding(ClearableComboBox.ItemsSourceProperty, new Binding(nameof(DetailDraft.GradeOptions)));
        boxFactory.SetBinding(ClearableComboBox.SelectedItemProperty, new Binding(nameof(DetailDraft.Grade))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        var template = new DataTemplate { VisualTree = boxFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Grade",
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(width),
            MinWidth = width
        });
    }

    private static void AddChoiceColumn(DataGrid grid, string title, string property,
        string itemsSourceProperty, double width)
    {
#pragma warning disable CS0618
        var boxFactory = new FrameworkElementFactory(typeof(ClearableComboBox));
        boxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
        boxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        boxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxFactory.SetValue(Border.BackgroundProperty, Brushes.White);
        boxFactory.SetValue(Border.BorderBrushProperty, Brush("#E2E8F0"));
        boxFactory.SetBinding(ClearableComboBox.ItemsSourceProperty, new Binding(itemsSourceProperty));
        boxFactory.SetBinding(ClearableComboBox.SelectedItemProperty, new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        var template = new DataTemplate { VisualTree = boxFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = title,
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(width),
            MinWidth = width
        });
    }

    private static (Dictionary<string, IReadOnlyList<string>> Destinations, IReadOnlyList<string> Codes,
        IReadOnlyList<string> Grades, IReadOnlyList<string> Packages) ReadCatalog(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The server returned an invalid marking preparation catalog.");

        var destinations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (catalog.TryGetProperty("codeDestinations", out var pairs) && pairs.ValueKind == JsonValueKind.Array)
        {
            foreach (var pair in pairs.EnumerateArray())
            {
                var code = ReadCatalogString(pair, "code");
                var destination = ReadCatalogString(pair, "destination");
                if (code.Length == 0 || destination.Length == 0) continue;
                if (!destinations.TryGetValue(code, out var values))
                    destinations[code] = values = [];
                if (!values.Contains(destination, StringComparer.OrdinalIgnoreCase)) values.Add(destination);
            }
        }

        string[] grades = catalog.TryGetProperty("grades", out var gradeRows) && gradeRows.ValueKind == JsonValueKind.Array
            ? gradeRows.EnumerateArray().Select(item => ReadCatalogString(item, "name"))
                .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        string[] packages = catalog.TryGetProperty("packageTypes", out var packageRows) && packageRows.ValueKind == JsonValueKind.Array
            ? packageRows.EnumerateArray().Select(item => ReadCatalogString(item, "code"))
                .Where(code => code.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        var orderedDestinations = destinations.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var codes = orderedDestinations.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        if (codes.Length == 0 || grades.Length == 0 || packages.Length == 0)
            throw new InvalidOperationException("The backend marking preparation catalog is empty. Check the Code, Destination, Grade and Package master data.");
        return (orderedDestinations, codes, grades, packages);
    }

    private static string ReadCatalogString(JsonElement item, string property)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static void AddMonthColumn(DataGrid grid)
    {
#pragma warning disable CS0618
        var pickerFactory = new FrameworkElementFactory(typeof(BookingQueueDatePicker));
        pickerFactory.SetValue(FrameworkElement.WidthProperty, 100d);
        pickerFactory.SetValue(FrameworkElement.HeightProperty, EditorDatePickerHeight);
        pickerFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        pickerFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        pickerFactory.SetValue(BookingQueueDatePicker.MonthSelectionOnlyProperty, true);
        pickerFactory.SetValue(UiControlProperties.ButtonCornerRadiusProperty, UiTokens.CornerRadiusSmall);
        pickerFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        pickerFactory.SetValue(BookingQueueDatePicker.DateDisplayFormatProperty, "MMM-yy");
        pickerFactory.SetBinding(BookingQueueDatePicker.SelectedDateProperty, new Binding(nameof(DetailDraft.MonthDate))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        var template = new DataTemplate { VisualTree = pickerFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Month",
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateDatePickerCellStyle(),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(106)
        });
    }

    private static void AddRowNumberColumn(DataGrid grid)
    {
#pragma warning disable CS0618
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.SetValue(TextBlock.ForegroundProperty, Brush("#64748B"));
        factory.SetBinding(TextBlock.TextProperty, new Binding(nameof(DataGridRow.Header))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
        });
        var template = new DataTemplate { VisualTree = factory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "No.",
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Center),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            IsReadOnly = true,
            Width = new DataGridLength(36)
        });
    }

    private static void AddTextColumn(DataGrid grid, string title, string property, double width, string? format = null,
        Style? elementStyle = null, Style? editingElementStyle = null, Style? cellStyle = null, Style? headerStyle = null)
        => AddTextColumn(grid, title, property, new DataGridLength(width), format,
            elementStyle: elementStyle, editingElementStyle: editingElementStyle, cellStyle: cellStyle, headerStyle: headerStyle);

    private static void AddTextColumn(DataGrid grid, string title, string property, DataGridLength width,
        string? format = null, double minWidth = 20, Style? elementStyle = null,
        Style? editingElementStyle = null, Style? cellStyle = null, Style? headerStyle = null)
    {
        var binding = new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            StringFormat = format is null ? null : $"{{0:{format}}}",
            ConverterCulture = format is "N0" or "N3"
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.CurrentCulture
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = title,
            Binding = binding,
            Width = width,
            MinWidth = minWidth,
            ElementStyle = elementStyle,
            EditingElementStyle = editingElementStyle,
            CellStyle = cellStyle,
            HeaderStyle = headerStyle ?? CreateHeaderStyle()
        });
    }

    private static void AddDateColumn(DataGrid grid, string title, string property)
    {
#pragma warning disable CS0618
        var datePickerFactory = new FrameworkElementFactory(typeof(BookingQueueDatePicker));
        datePickerFactory.SetValue(FrameworkElement.WidthProperty, 115d);
        datePickerFactory.SetValue(FrameworkElement.HeightProperty, EditorDatePickerHeight);
        datePickerFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        datePickerFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        datePickerFactory.SetValue(UiControlProperties.ButtonCornerRadiusProperty, UiTokens.CornerRadiusSmall);
        datePickerFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        datePickerFactory.SetBinding(BookingQueueDatePicker.SelectedDateProperty, new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        datePickerFactory.SetValue(BookingQueueDatePicker.DateDisplayFormatProperty, "dd-MMM-yyyy");
        if (property == nameof(DetailDraft.EtdDate))
        {
            datePickerFactory.AddHandler(BookingQueueDatePicker.DateSelectedEvent,
                new RoutedPropertyChangedEventHandler<DateTime?>((sender, args) =>
                {
                    if (sender is not BookingQueueDatePicker { DataContext: DetailDraft detail }) return;
                    if (detail.MarkingDueDate is null || detail.MarkingDueDate == args.OldValue?.AddDays(-20))
                        detail.MarkingDueDate = args.NewValue?.AddDays(-20);
                }));
        }

        var template = new DataTemplate { VisualTree = datePickerFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = title,
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateDatePickerCellStyle(),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(122)
        });
    }

    private static Style CreateDatePickerCellStyle()
    {
        var style = CreateGridCellStyle(HorizontalAlignment.Stretch);
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateGridCellStyle(HorizontalAlignment contentAlignment)
    {
        var style = new Style(typeof(DataGridCell));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridCell)) is Style defaultStyle)
            style.BasedOn = defaultStyle;
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2, 1, 2, 1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, contentAlignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#F1F5F9")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        return style;
    }

    private static void EnableSingleClickEditing(DataGrid grid)
    {
        grid.PreviewMouseLeftButtonDown += (sender, args) =>
        {
            if (args.OriginalSource is not DependencyObject dep) return;
            if (FindVisualParent<ComboBox>(dep) is not null ||
                FindVisualParent<Button>(dep) is not null ||
                FindVisualParent<ClearableComboBox>(dep) is not null ||
                FindVisualParent<BookingQueueDatePicker>(dep) is not null ||
                FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) is not null)
            {
                return;
            }

            var cell = FindVisualParent<DataGridCell>(dep);
            if (cell is null || cell.IsEditing || cell.IsReadOnly) return;
            if (cell.Column is not DataGridTextColumn) return;

            if (cell.DataContext is not null && !Equals(grid.SelectedItem, cell.DataContext))
            {
                grid.SelectedItem = cell.DataContext;
            }

            if (!cell.IsFocused) cell.Focus();
            grid.BeginEdit();

            grid.Dispatcher.BeginInvoke(() =>
            {
                if (cell.Content is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        };
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            DependencyObject? parent = null;
            if (current is Visual)
            {
                try { parent = VisualTreeHelper.GetParent(current); } catch { }
            }
            parent ??= LogicalTreeHelper.GetParent(current);
            current = parent;
        }
        return null;
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
        var details = _details.Where(IsMeaningful).ToList();
        var detailsChanged = !_isNewReport &&
                             !string.Equals(DetailFingerprint(details), _originalDetailFingerprint, StringComparison.Ordinal);
        var newRevisions = revisions
            .Where(item => !string.IsNullOrWhiteSpace(item.Revision) &&
                           !_originalRevisionNumbers.Contains(item.Revision.Trim()))
            .ToList();
        if (detailsChanged && newRevisions.Count == 0)
        {
            var revision = new RevisionDraft
            {
                Revision = NextRevision(revisions),
                ReportDate = DateTime.Today
            };
            _revisions.Add(revision);
            _revisionGrid.SelectedItem = revision;
            _revisionGrid.ScrollIntoView(revision);
            ShowValidation($"Marking details changed. Revision {revision.Revision} was added. Enter the reason in its Remark cell, then save again.");
            return;
        }
        if (newRevisions.Any(item => string.IsNullOrWhiteSpace(item.Remark)))
        {
            ShowValidation("Enter a reason in the Remark column for each new revision.");
            return;
        }
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

        if (details.Any(item => string.IsNullOrWhiteSpace(item.JointContractName)))
        {
            ShowValidation("Each detail row needs a Joint Contract Name.");
            return;
        }

        Payload = new
        {
            reportMonth = reportMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            createdBy = Optional(_state.CreatedBy),
            printedBy = Optional(_state.PrintedBy),
            issueBy = Optional(_state.IssueBy),
            issueDate = ToIso(_state.IssueDate),
            productionAdminSign = Optional(_state.ProductionAdminSign),
            productionAdminDate = ToIso(_state.ProductionAdminDate),
            qacSign = Optional(_state.QacSign),
            qacDate = ToIso(_state.QacDate),
            warehouseSign = Optional(_state.WarehouseSign),
            warehouseDate = ToIso(_state.WarehouseDate),
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

    private static string DetailFingerprint(IEnumerable<DetailDraft> details)
        => JsonSerializer.Serialize(details.Where(IsMeaningful).Select(item => new
        {
            MonthLabel = Optional(item.MonthLabel),
            JointContractName = Optional(item.JointContractName),
            Code = Optional(item.Code),
            Destination = Optional(item.Destination),
            Grade = Optional(item.Grade),
            item.Unit,
            item.GrossWeight,
            item.NetWeight,
            PackageType = Optional(item.PackageType),
            MarkingDueDate = item.MarkingDueDate?.Date,
            MarkingSign = Optional(item.MarkingSign),
            StatusDate = item.StatusDate?.Date,
            EtdDate = item.EtdDate?.Date
        }).ToArray(), JsonOptions);

    private void ShowValidation(string message)
        => MessageBox.Show(this, message, "Check report details", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ToIso(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NextRevision(IEnumerable<RevisionDraft> revisions)
    {
        var numbers = revisions.Select(item => int.TryParse(item.Revision, out var number) ? number : -1);
        return numbers.DefaultIfEmpty(-1).Max() is var next && next >= 0 ? (next + 1).ToString("D2", CultureInfo.InvariantCulture) : "00";
    }

    private static BookingQueueDatePicker CreateDatePicker(DateTime? value)
    {
        var datePicker = new BookingQueueDatePicker
        {
            SelectedDate = value?.Date,
            Width = EditorDatePickerWidth,
            Height = EditorDatePickerHeight
        };
        UiControlProperties.SetButtonCornerRadius(datePicker, UiTokens.CornerRadiusSmall);
        return datePicker;
    }

    private Button CreateButton(string text, bool primary = false)
    {
        if (!primary && text.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
        {
            var button = new Button
            {
                Content = new FluentIcon
                {
                    Icon = FluentIcons.Common.Icon.Delete,
                    IconSize = IconSize.Size16,
                    IconVariant = IconVariant.Regular,
                    Width = 16,
                    Height = 16,
                    Foreground = Brush("#EF4444"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Width = 28,
                MinWidth = 28,
                Height = 28,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                Background = Brush("#FEF2F2"),
                BorderBrush = Brush("#FECACA"),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = text
            };
            UiControlProperties.SetButtonCornerRadius(button, UiTokens.CornerRadiusSmall);
            System.Windows.Automation.AutomationProperties.SetName(button, text);
            return button;
        }

        if (text.StartsWith("Add", StringComparison.OrdinalIgnoreCase))
        {
            var icon = new FluentIcon
            {
                Icon = FluentIcons.Common.Icon.Add,
                IconSize = IconSize.Size16,
                IconVariant = IconVariant.Regular,
                Foreground = Brush("#0F766E"),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(icon);
            stack.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#0F766E"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var button = new Button
            {
                Content = stack,
                Height = 28,
                Padding = new Thickness(10, 2, 10, 2),
                Background = Brush("#F0FDF4"),
                BorderBrush = Brush("#99F6E4"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0)
            };
            UiControlProperties.SetButtonCornerRadius(button, UiTokens.CornerRadiusSmall);
            return button;
        }

        var btn = new Button
        {
            Content = text,
            Height = 34,
            MinWidth = primary ? 120 : 90,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 4, 14, 4),
            FontSize = 13,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
        };
        UiControlProperties.SetButtonCornerRadius(btn, UiTokens.CornerRadiusControl);
        return btn;
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);

    private sealed class EditorState
    {
        public DateTime? CreatedAt { get; set; }
        public DateTime ReportMonth { get; set; }
        public string? CreatedBy { get; set; }
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

    private sealed class DetailDraft : INotifyPropertyChanged
    {
        private string? _monthLabel;
        private DateTime? _markingDueDate;
        private string? _code;
        private string? _destination;
        private string? _grade;
        private Dictionary<string, IReadOnlyList<string>> _codeDestinations = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<string> _allCodes = [];
        private IReadOnlyList<string> _allDestinations = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? MonthLabel
        {
            get => _monthLabel;
            set
            {
                if (_monthLabel == value) return;
                _monthLabel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthDate)));
            }
        }
        public DateTime? MonthDate
        {
            get => ParseMonthLabel(MonthLabel);
            set => MonthLabel = value?.ToString("MMM-yy", CultureInfo.InvariantCulture);
        }
        public string? JointContractName { get; set; }
        public string? Code
        {
            get => _code;
            set
            {
                if (string.Equals(_code, value, StringComparison.Ordinal)) return;
                _code = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
                RefreshDestinationsForCode();
            }
        }
        public string? Destination
        {
            get => _destination;
            set
            {
                if (string.Equals(_destination, value, StringComparison.Ordinal)) return;
                _destination = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Destination)));
                if (string.IsNullOrWhiteSpace(value))
                {
                    if (string.IsNullOrWhiteSpace(Code))
                        SetCodeOptions(_allCodes);
                    else
                        RefreshDestinationsForCode();
                    return;
                }

                var clean = value.Trim();
                var matchingCodes = CodesForDestination(clean);
                if (matchingCodes.Count == 0 && clean.Length >= 2)
                {
                    var prefixMatches = _codeDestinations
                        .Where(item => item.Value.Any(d => d.Trim().StartsWith(clean, StringComparison.OrdinalIgnoreCase)))
                        .Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
                    if (prefixMatches.Length == 1)
                    {
                        matchingCodes = prefixMatches;
                    }
                }

                if (matchingCodes.Count > 0)
                {
                    if (!matchingCodes.Contains(Code ?? "", StringComparer.OrdinalIgnoreCase))
                    {
                        Code = matchingCodes[0];
                    }
                    SetCodeOptions(matchingCodes);
                }
            }
        }
        public string? Grade
        {
            get => _grade;
            set
            {
                if (string.Equals(_grade, value, StringComparison.Ordinal)) return;
                _grade = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Grade)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeBackground)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeForeground)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeBorderBrush)));
            }
        }
        public Brush GradeBackground => GradeColorHelper.GetBackgroundBrush(Grade);
        public Brush GradeForeground => GradeColorHelper.GetForegroundBrush(Grade);
        public Brush GradeBorderBrush => GradeColorHelper.GetBorderBrush(Grade);
        public ObservableCollection<string> DestinationOptions { get; } = [];
        public IReadOnlyList<string> CodeOptions { get; private set; } = [];
        public IReadOnlyList<string> GradeOptions { get; private set; } = [];
        public IReadOnlyList<string> PackageOptions { get; private set; } = [];
        public int? Unit { get; set; }
        [JsonPropertyName("grossWeight")]
        public decimal? GrossWeight { get; set; }
        [JsonPropertyName("gross_weight")]
        public decimal? GrossWeightSnake
        {
            get => GrossWeight;
            set => GrossWeight ??= value;
        }
        [JsonPropertyName("netWeight")]
        public decimal? NetWeight { get; set; }
        [JsonPropertyName("net_weight")]
        public decimal? NetWeightSnake
        {
            get => NetWeight;
            set => NetWeight ??= value;
        }
        private string? _packageType;
        public string? PackageType
        {
            get => _packageType;
            set
            {
                if (string.Equals(_packageType, value, StringComparison.Ordinal)) return;
                _packageType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageType)));
            }
        }
        public DateTime? MarkingDueDate
        {
            get => _markingDueDate;
            set
            {
                if (_markingDueDate == value) return;
                _markingDueDate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkingDueDate)));
            }
        }
        public string? MarkingSign { get; set; }
        public DateTime? StatusDate { get; set; }
        public DateTime? EtdDate { get; set; }

        public void ConfigureCatalog(Dictionary<string, IReadOnlyList<string>> codeDestinations,
            IReadOnlyList<string> codes, IReadOnlyList<string> grades, IReadOnlyList<string> packages)
        {
            _codeDestinations = codeDestinations;
            _allCodes = codes;
            _allDestinations = codeDestinations.Values.SelectMany(value => value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            GradeOptions = PreserveExistingValue(grades, Grade);

            // Normalize already-saved values as well as future selections. Without this,
            // a row loaded with a Code but no Destination stays incomplete when the user
            // selects that same Code again (the property setter correctly sees no change).
            if (!string.IsNullOrWhiteSpace(Code))
            {
                RefreshDestinationsForCode();
            }
            else if (!string.IsNullOrWhiteSpace(Destination))
            {
                var matchingCodes = CodesForDestination(Destination);
                if (matchingCodes.Count == 1)
                    Code = matchingCodes[0];
                else
                {
                    SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                    SetCodeOptions(matchingCodes.Count > 0
                        ? matchingCodes
                        : PreserveExistingValue(codes, Code));
                }
            }
            else
            {
                SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                SetCodeOptions(PreserveExistingValue(codes, Code));
            }
            PackageOptions = PreserveExistingValue(packages, PackageType);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageOptions)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeOptions)));
        }

        private static IReadOnlyList<string> PreserveExistingValue(IReadOnlyList<string> options, string? existing)
            => !string.IsNullOrWhiteSpace(existing) && !options.Contains(existing, StringComparer.OrdinalIgnoreCase)
                ? options.Append(existing).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
                : options;

        private void RefreshDestinationsForCode()
        {
            if (string.IsNullOrWhiteSpace(Code) || !_codeDestinations.TryGetValue(Code, out var matches))
            {
                SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                SetCodeOptions(PreserveExistingValue(_allCodes, Code));
                return;
            }

            if (string.IsNullOrWhiteSpace(Destination))
            {
                if (matches.Count == 1) Destination = matches[0];
            }
            else if (!matches.Contains(Destination, StringComparer.OrdinalIgnoreCase))
                Destination = matches.Count == 1 ? matches[0] : null;
            SetDestinationOptions(matches, preserveExistingValue: true);
            SetCodeOptions(PreserveExistingValue(_allCodes, Code));
        }

        private IReadOnlyList<string> CodesForDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) return Array.Empty<string>();
            var target = destination.Trim();
            return _codeDestinations
                .Where(item => item.Value.Any(d => string.Equals(d.Trim(), target, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void SetDestinationOptions(IEnumerable<string> values, bool preserveExistingValue)
        {
            var options = values.ToList();
            if (preserveExistingValue && !string.IsNullOrWhiteSpace(Destination) &&
                !options.Contains(Destination, StringComparer.OrdinalIgnoreCase))
                options.Add(Destination);
            DestinationOptions.Clear();
            foreach (var value in options.Distinct(StringComparer.OrdinalIgnoreCase))
                DestinationOptions.Add(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationOptions)));
        }

        private void SetCodeOptions(IEnumerable<string> values)
        {
            CodeOptions = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeOptions)));
        }

        private static DateTime? ParseMonthLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            var formats = new[] { "MMM-yy", "MMM-yyyy", "MMM yyyy", "MMMM yyyy", "yyyy-MM", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(label.Trim(), formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var month))
                return null;
            return new DateTime(month.Year, month.Month, 1);
        }
    }
}

internal sealed class BookingQueueDatePicker : Button
{
    public static readonly DependencyProperty MonthSelectionOnlyProperty = DependencyProperty.Register(
        nameof(MonthSelectionOnly), typeof(bool), typeof(BookingQueueDatePicker), new PropertyMetadata(false));

    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(BookingQueueDatePicker),
        new FrameworkPropertyMetadata(null, OnSelectedDateChanged));

    public static readonly DependencyProperty DateDisplayFormatProperty = DependencyProperty.Register(
        nameof(DateDisplayFormat), typeof(string), typeof(BookingQueueDatePicker),
        new FrameworkPropertyMetadata("dd MMM yyyy", OnDateDisplayFormatChanged));

    public static readonly RoutedEvent DateSelectedEvent = EventManager.RegisterRoutedEvent(
        nameof(DateSelected), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<DateTime?>),
        typeof(BookingQueueDatePicker));

    private readonly TextBlock _dateText;
    private readonly System.Windows.Controls.Calendar _calendar;
    private readonly Popup _popup;
    private readonly Border _popupBorder;
    private readonly Grid _monthPickerPanel;
    private readonly TextBlock _monthPickerYearText;
    private readonly Button[] _monthButtons = new Button[12];
    private int _monthPickerYear;
    private bool _initializingCalendar;

    public bool MonthSelectionOnly
    {
        get => (bool)GetValue(MonthSelectionOnlyProperty);
        set => SetValue(MonthSelectionOnlyProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public string DateDisplayFormat
    {
        get => (string)GetValue(DateDisplayFormatProperty);
        set => SetValue(DateDisplayFormatProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<DateTime?> DateSelected
    {
        add => AddHandler(DateSelectedEvent, value);
        remove => RemoveHandler(DateSelectedEvent, value);
    }

    public BookingQueueDatePicker()
    {
        Width = UiTokens.DatePickerWidth;
        Height = UiTokens.DatePickerHeight;
        Padding = new Thickness(6, 0, 6, 0);
        Background = Brushes.White;
        BorderBrush = Brush("#CFD9E5");
        BorderThickness = new Thickness(1);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
        ToolTip = "Choose date";
        if (System.Windows.Application.Current?.TryFindResource(typeof(Button)) is Style buttonStyle)
            Style = buttonStyle;

        _dateText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        Content = _dateText;

        _calendar = new System.Windows.Controls.Calendar
        {
            SelectionMode = CalendarSelectionMode.SingleDate,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
            Style = MainWindow.CreateBookingQueueCalendarStyle()
        };
        _monthPickerYearText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _monthPickerPanel = CreateMonthPickerPanel();
        _popupBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#D8E0EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusMedium,
            Padding = new Thickness(5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(15, 23, 42),
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = .16
            },
            Child = _calendar
        };
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = _popupBorder
        };
        _calendar.SelectedDatesChanged += (_, _) =>
        {
            if (_initializingCalendar || MonthSelectionOnly || _calendar.SelectedDate is not DateTime selectedDate) return;
            var previousDate = SelectedDate?.Date;
            var nextDate = selectedDate.Date;
            if (previousDate != nextDate)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousDate, nextDate, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, nextDate);
            _popup.IsOpen = false;
        };
        _calendar.DisplayModeChanged += (_, _) =>
        {
            if (_initializingCalendar || !_popup.IsOpen || !MonthSelectionOnly ||
                _calendar.DisplayMode != CalendarMode.Month) return;

            var selectedMonth = new DateTime(_calendar.DisplayDate.Year, _calendar.DisplayDate.Month, 1);
            var previousMonth = SelectedDate?.Date;
            if (previousMonth != selectedMonth)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousMonth, selectedMonth, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, selectedMonth);
            _popup.IsOpen = false;
        };
        Click += (_, _) => OpenCalendar();
        UpdateDateText();
    }

    private void OpenCalendar()
    {
        var initialDate = SelectedDate?.Date ?? DateTime.Today;
        if (MonthSelectionOnly)
        {
            _monthPickerYear = initialDate.Year;
            UpdateMonthPicker();
            _popupBorder.Child = _monthPickerPanel;
        }
        else
        {
            _calendar.ClearValue(FrameworkElement.WidthProperty);
            _calendar.ClearValue(FrameworkElement.HeightProperty);
            _calendar.ClearValue(Control.PaddingProperty);
            _popupBorder.Child = _calendar;
        }

        _initializingCalendar = true;
        if (!MonthSelectionOnly)
        {
            _calendar.DisplayDate = initialDate;
            _calendar.SelectedDate = SelectedDate?.Date;
            _calendar.DisplayMode = CalendarMode.Month;
        }
        _initializingCalendar = false;
        _popup.IsOpen = true;
    }

    private Grid CreateMonthPickerPanel()
    {
        // Month selection needs less space than the full day calendar while keeping
        // four readable columns and three evenly sized rows.
        var panel = new Grid { Width = 288, Height = 222 };
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        header.Children.Add(CreateMonthPickerButton("‹", () => ChangeMonthPickerYear(-1), isNavigation: true));
        Grid.SetColumn(_monthPickerYearText, 1);
        header.Children.Add(_monthPickerYearText);
        var nextYear = CreateMonthPickerButton("›", () => ChangeMonthPickerYear(1), isNavigation: true);
        Grid.SetColumn(nextYear, 2);
        header.Children.Add(nextYear);
        panel.Children.Add(header);

        var months = new Grid { Margin = new Thickness(2, 4, 2, 2) };
        for (var column = 0; column < 4; column++)
            months.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 3; row++)
            months.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (var month = 1; month <= 12; month++)
        {
            var monthNumber = month;
            var label = CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetAbbreviatedMonthName(month);
            if (month == 9) label = "Sept";
            var button = CreateMonthPickerButton(label, () => SelectMonth(monthNumber));
            _monthButtons[month - 1] = button;
            Grid.SetRow(button, (month - 1) / 4);
            Grid.SetColumn(button, (month - 1) % 4);
            months.Children.Add(button);
        }

        Grid.SetRow(months, 1);
        panel.Children.Add(months);
        return panel;
    }

    private static Button CreateMonthPickerButton(string label, Action onClick, bool isNavigation = false)
    {
        var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="{x:Type Button}">
                <Border x:Name="Chrome" Background="{TemplateBinding Background}" CornerRadius="7">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                      Margin="{TemplateBinding Padding}"
                                      RecognizesAccessKey="True"
                                      TextElement.Foreground="{TemplateBinding Foreground}" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="#EDF7F5" />
                        <Setter Property="Foreground" Value="#0F766E" />
                        <Setter Property="FontWeight" Value="SemiBold" />
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="#DDEEEB" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Foreground" Value="#B7C0CA" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#344054")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, isNavigation ? 20d : 15d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(isNavigation ? 1 : 2)));

        var button = new Button
        {
            Content = label,
            Style = style,
            Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void ChangeMonthPickerYear(int offset)
    {
        var nextYear = _monthPickerYear + offset;
        if (nextYear is < 1 or > 9999) return;
        _monthPickerYear = nextYear;
        UpdateMonthPicker();
    }

    private void UpdateMonthPicker()
    {
        _monthPickerYearText.Text = _monthPickerYear.ToString(CultureInfo.InvariantCulture);
        var selected = SelectedDate;
        for (var index = 0; index < _monthButtons.Length; index++)
        {
            var isSelected = selected is { } date && date.Year == _monthPickerYear && date.Month == index + 1;
            var button = _monthButtons[index];
            button.Background = isSelected ? Brush("#D7EDF2") : Brushes.Transparent;
            button.Foreground = isSelected ? Brush("#0F625B") : Brush("#344054");
            button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void SelectMonth(int month)
    {
        var selectedMonth = new DateTime(_monthPickerYear, month, 1);
        var previousMonth = SelectedDate?.Date;
        if (previousMonth != selectedMonth)
            RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousMonth, selectedMonth, DateSelectedEvent));
        SetCurrentValue(SelectedDateProperty, selectedMonth);
        _popup.IsOpen = false;
    }

    private void UpdateDateText()
        => _dateText.Text = SelectedDate?.ToString(DateDisplayFormat, CultureInfo.GetCultureInfo("en-GB")) ?? "Select a date";

    private static void OnSelectedDateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is BookingQueueDatePicker picker) picker.UpdateDateText();
    }

    private static void OnDateDisplayFormatChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is BookingQueueDatePicker picker) picker.UpdateDateText();
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}

internal sealed class ClearableComboBox : Border
{
    private static readonly ControlTemplate EditableComboBoxNoArrowTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type ComboBox}'>
    <Grid>
        <TextBox x:Name='PART_EditableTextBox'
                 FocusVisualStyle='{x:Null}'
                 Background='Transparent'
                 BorderThickness='0'
                 Padding='6,2,22,2'
                 VerticalContentAlignment='Center'
                 FontSize='{TemplateBinding FontSize}'
                 FontFamily='{TemplateBinding FontFamily}'
                 Foreground='{TemplateBinding Foreground}'
                 FontWeight='{TemplateBinding FontWeight}'
                 IsReadOnly='{TemplateBinding IsReadOnly}' />
        <Popup x:Name='PART_Popup'
               AllowsTransparency='True'
               IsOpen='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}'
               Placement='Bottom'
               PopupAnimation='Fade'
               Focusable='False'>
            <Border Background='White'
                    BorderBrush='#CBD5E1'
                    BorderThickness='1'
                    CornerRadius='4'
                    MinHeight='24'
                    MaxHeight='220'
                    MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
                    SnapsToDevicePixels='True'>
                <Border.Effect>
                    <DropShadowEffect Color='#0F172A' BlurRadius='10' ShadowDepth='3' Opacity='0.16' />
                </Border.Effect>
                <ScrollViewer SnapsToDevicePixels='True' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
                    <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained' />
                </ScrollViewer>
            </Border>
        </Popup>
    </Grid>
</ControlTemplate>");

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(string), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty ItemForegroundProperty = DependencyProperty.Register(
        nameof(ItemForeground), typeof(Brush), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, OnItemForegroundChanged));

    public static readonly DependencyProperty ItemFontWeightProperty = DependencyProperty.Register(
        nameof(ItemFontWeight), typeof(FontWeight), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(FontWeights.Normal, OnItemFontWeightChanged));

    private readonly ComboBox _comboBox;
    private readonly Button _clearButton;
    private bool _isUpdatingInternally;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedItem
    {
        get => (string?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public Brush? ItemForeground
    {
        get => (Brush?)GetValue(ItemForegroundProperty);
        set => SetValue(ItemForegroundProperty, value);
    }

    public FontWeight ItemFontWeight
    {
        get => (FontWeight)GetValue(ItemFontWeightProperty);
        set => SetValue(ItemFontWeightProperty, value);
    }

    public ClearableComboBox()
    {
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        BorderBrush = Brush("#E2E8F0");
        Background = Brushes.White;
        Height = 28;
        MinHeight = 26;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var grid = new Grid();

        _comboBox = new ComboBox
        {
            IsEditable = true,
            IsTextSearchEnabled = true,
            Template = EditableComboBoxNoArrowTemplate,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            FontSize = 12.5,
            FocusVisualStyle = null,
            MinHeight = 26,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsSynchronizedWithCurrentItem = false
        };

        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        _comboBox.ItemContainerStyle = itemStyle;

        _comboBox.SelectionChanged += (_, _) =>
        {
            if (_isUpdatingInternally) return;
            var selected = _comboBox.SelectedItem as string ?? _comboBox.Text;
            var currentVal = string.IsNullOrWhiteSpace(selected) ? null : selected.Trim();
            if (!string.Equals(SelectedItem, currentVal, StringComparison.Ordinal))
            {
                _isUpdatingInternally = true;
                try
                {
                    SetCurrentValue(SelectedItemProperty, currentVal);
                }
                finally
                {
                    _isUpdatingInternally = false;
                }
            }
            UpdateClearButtonVisibility();
        };

        _comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler((_, _) =>
            {
                if (_isUpdatingInternally) return;
                var text = _comboBox.Text;
                var currentVal = string.IsNullOrWhiteSpace(text) ? null : text;
                if (!string.Equals(SelectedItem, currentVal, StringComparison.Ordinal))
                {
                    _isUpdatingInternally = true;
                    try
                    {
                        SetCurrentValue(SelectedItemProperty, currentVal);
                    }
                    finally
                    {
                        _isUpdatingInternally = false;
                    }
                }
                UpdateClearButtonVisibility();
            }));

        _comboBox.PreviewMouseLeftButtonDown += (_, e) =>
        {
            SelectParentDataGridRow();
            if (!_comboBox.IsDropDownOpen)
            {
                _comboBox.IsDropDownOpen = true;
            }
        };

        _comboBox.GotFocus += (_, _) =>
        {
            SelectParentDataGridRow();
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = Brush("#0F766E");
            }
        };
        _comboBox.LostFocus += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = IsMouseOver ? Brush("#CBD5E1") : Brush("#E2E8F0");
            }
            if (!string.IsNullOrWhiteSpace(_comboBox.Text))
            {
                var trimmed = _comboBox.Text.Trim();
                if (!string.Equals(_comboBox.Text, trimmed, StringComparison.Ordinal))
                {
                    _comboBox.Text = trimmed;
                }
            }
        };
        _comboBox.DropDownOpened += (_, _) =>
        {
            SelectParentDataGridRow();
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = Brush("#0F766E");
            }
        };
        _comboBox.DropDownClosed += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = IsMouseOver ? Brush("#CBD5E1") : Brush("#E2E8F0");
            }
        };

        MouseEnter += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = Brush("#CBD5E1");
            }
        };
        MouseLeave += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = Brush("#E2E8F0");
            }
        };

        _clearButton = CreateClearButton();
        _clearButton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            SelectParentDataGridRow();
            e.Handled = true;
            ClearSelection();
        };
        _clearButton.Click += (_, e) =>
        {
            SelectParentDataGridRow();
            e.Handled = true;
            ClearSelection();
        };

        grid.Children.Add(_comboBox);
        grid.Children.Add(_clearButton);
        Child = grid;

        Loaded += (_, _) =>
        {
            SyncInternalState();
        };
    }

    private void SelectParentDataGridRow()
    {
        if (DataContext is not null)
        {
            var grid = FindVisualParent<DataGrid>(this);
            if (grid != null && !Equals(grid.SelectedItem, DataContext))
            {
                grid.SelectedItem = DataContext;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            DependencyObject? parent = null;
            if (current is Visual)
            {
                parent = VisualTreeHelper.GetParent(current);
            }
            current = parent ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ClearSelection()
    {
        _isUpdatingInternally = true;
        try
        {
            _comboBox.SelectedItem = null;
            _comboBox.Text = string.Empty;
            SetCurrentValue(SelectedItemProperty, null);
        }
        finally
        {
            _isUpdatingInternally = false;
        }
        UpdateClearButtonVisibility();
        _comboBox.Focus();
    }

    private void SyncInternalState()
    {
        if (ItemsSource is not null && _comboBox.ItemsSource != ItemsSource)
        {
            _comboBox.ItemsSource = ItemsSource;
        }

        var currentText = SelectedItem ?? string.Empty;
        if (!string.Equals(_comboBox.Text, currentText, StringComparison.Ordinal))
        {
            _isUpdatingInternally = true;
            try
            {
                _comboBox.Text = currentText;
                _comboBox.SelectedItem = SelectedItem;
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        if (ItemForeground is not null)
        {
            _comboBox.Foreground = ItemForeground;
        }

        _comboBox.FontWeight = ItemFontWeight;

        UpdateClearButtonVisibility();
    }

    private void UpdateClearButtonVisibility()
    {
        var hasValue = !string.IsNullOrWhiteSpace(SelectedItem) ||
                       !string.IsNullOrWhiteSpace(_comboBox.Text);
        _clearButton.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box)
        {
            box._comboBox.ItemsSource = e.NewValue as IEnumerable;
            box.SyncInternalState();
        }
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && !box._isUpdatingInternally)
        {
            var val = e.NewValue as string;
            box._isUpdatingInternally = true;
            try
            {
                var textVal = val ?? string.Empty;
                if (!string.Equals(box._comboBox.Text, textVal, StringComparison.Ordinal))
                {
                    box._comboBox.Text = textVal;
                }
                if (!string.Equals(box._comboBox.SelectedItem as string, val, StringComparison.Ordinal))
                {
                    box._comboBox.SelectedItem = val;
                }
            }
            finally
            {
                box._isUpdatingInternally = false;
            }
            box.UpdateClearButtonVisibility();
        }
    }

    private static void OnItemForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && e.NewValue is Brush brush)
        {
            box._comboBox.Foreground = brush;
        }
    }

    private static void OnItemFontWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && e.NewValue is FontWeight fw)
        {
            box._comboBox.FontWeight = fw;
        }
    }

    private static Button CreateClearButton()
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 3 3 L 9 9 M 9 3 L 3 9"),
            Stroke = Brush("#94A3B8"),
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 12,
            Height = 12
        };

        var border = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Child = path
        };

        var button = new Button
        {
            Width = 16,
            Height = 16,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = "Clear",
            Visibility = Visibility.Collapsed,
            Content = border
        };

        button.MouseEnter += (_, _) =>
        {
            border.Background = Brush("#FEE2E2");
            path.Stroke = Brush("#DC2626");
        };
        button.MouseLeave += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            path.Stroke = Brush("#94A3B8");
        };

        return button;
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}


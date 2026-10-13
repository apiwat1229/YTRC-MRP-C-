using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FluentIcons.Common;
using FluentIcons.Wpf;
using YTRC.ERP.Domain.Configuration;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const string TruckScalePageId = "RRC:TruckScale";
    private const string TruckScaleSummaryPageId = "RRC:QueueSummary";
    private static readonly CultureInfo TruckScaleCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly string[] TruckScaleTypes =
        ["Pickup", "6 Wheels", "10 Wheels", "10 Wheels Trailer", "18 Wheels Trailer", "Trailer"];

    private readonly List<TruckScaleBooking> _truckScaleBookings = [];
    private readonly List<TruckScaleChoice> _truckScaleRubberTypes = [];
    private readonly List<TruckScaleChoice> _truckScaleProvinces = [];
    private readonly DispatcherTimer _truckScaleRefreshTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private Grid? _truckScalePageHost;
    private bool _truckScaleTimerHooked;
    private DateTime _truckScaleDate = DateTime.Today;
    private DateTime _truckScaleLoadedDate = DateTime.MinValue;
    private string _truckScaleSearch = "";
    private string _truckScaleCategory = "all";
    private string _truckScaleStage = "all";
    private string _truckScaleRubberFilter = "all";
    private string _truckScaleTruckFilter = "all";
    private string _truckScaleSelectedId = "";
    private string _truckScaleMessage = "";
    private bool _truckScaleShowReturns;
    private bool _truckScaleLoading;
    private bool _truckScaleLookupsLoaded;
    private int _truckScaleLoadVersion;
    private TextBox? _truckScaleSearchBox;
    private TextBlock? _truckScaleResultCount;
    private DataGrid? _truckScaleGrid;
    private WrapPanel? _truckScaleActionPanel;
    private ContentControl? _truckScaleMetricsHost;
    private ContentControl? _truckScaleSummaryMetricsHost;

    private void OpenTruckScalePage(string module, string category = "all")
    {
        TrySelectRibbonTab("TruckScale");
        _truckScaleCategory = category;
        _truckScaleStage = "all";
        _truckScaleRubberFilter = "all";
        _truckScaleTruckFilter = "all";
        _truckScaleSearch = "";
        _currentPage = module == "queue-summary" ? TruckScaleSummaryPageId : TruckScalePageId;
        _pageMessage = "";
        if (!_truckScaleTimerHooked)
        {
            _truckScaleRefreshTimer.Tick += TruckScaleRefreshTimer_Tick;
            _truckScaleTimerHooked = true;
        }
        if (!_truckScaleRefreshTimer.IsEnabled)
        {
            _truckScaleRefreshTimer.Start();
        }
        RenderCurrentPage();
        _ = LoadTruckScaleDataAsync(forceLookups: !_truckScaleLookupsLoaded);
    }

    private UIElement BuildTruckScalePage(string module)
    {
        if (!_session.User.HasDepartmentAccess(DepartmentId.RRC) &&
            !_session.User.HasDepartmentAccess(DepartmentId.Receiving))
        {
            var restricted = PageStack();
            restricted.Children.Add(TruckScaleTitleBlock(TruckScalePageTitle(), TruckScaleOperationSubtitle()));
            restricted.Children.Add(Banner("Access restricted", "Your account does not have permission to view RRC.", "#DC2626"));
            return restricted;
        }

        if (_session.IsPreview)
        {
            var preview = PageStack();
            preview.Children.Add(TruckScaleTitleBlock(TruckScalePageTitle(), TruckScaleOperationSubtitle()));
            preview.Children.Add(Banner("Sign in required", "TruckScale operations are available after signing in to the ERP.", "#D97706"));
            return preview;
        }

        var view = module == "queue-summary" ? BuildTruckScaleSummaryView() : BuildTruckScaleOperationsView();
        _truckScalePageHost = view as Grid;
        return view;
    }

    private string TruckScaleCategoryName() => _truckScaleCategory switch
    {
        "cuplump" => "Cuplump",
        "uss" => "USS",
        _ => "All"
    };

    private string TruckScalePageTitle() => _truckScaleCategory == "all"
        ? "RRC · TruckScale"
        : $"RRC · {TruckScaleCategoryName()} TruckScale";

    private string TruckScaleOperationSubtitle() => _truckScaleCategory switch
    {
        "uss" => "USS check-in and weighing operations.",
        "cuplump" => "Cuplump check-in, draining and weighing operations.",
        _ => "Truck check-in, draining and weighing operations."
    };

    private UIElement TruckScaleTitleBlock(string title, string subtitle)
    {
        var stack = new StackPanel { Tag = StickyPageHeaderTag, Margin = new Thickness(0, 0, 0, 7) };
        stack.Children.Add(Label(title, 18, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        return stack;
    }

    private UIElement BuildTruckScaleOperationsView()
    {
        var root = new Grid
        {
            Margin = UiTokens.PageMargin,
            Height = GetTruckScaleViewportHeight(),
            MinHeight = 440
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _truckScaleSearchBox = new TextBox
        {
            Text = _truckScaleSearch,
            Width = 200,
            Height = 34,
            FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Search by queue, booking code, supplier or truck registration"
        };
        _truckScaleSearchBox.TextChanged += (_, _) =>
        {
            _truckScaleSearch = _truckScaleSearchBox?.Text ?? "";
            RefreshTruckScaleGridItems();
        };
        _truckScaleSearchBox.KeyDown += TruckScaleSearchBox_KeyDown;

        var dateButton = CreateTruckScaleDateButton(selected =>
        {
            _truckScaleDate = selected.Date;
            _truckScaleStage = "all";
            _truckScaleLoadedDate = DateTime.MinValue;
            _ = LoadTruckScaleDataAsync();
        });
        dateButton.Margin = new Thickness(0, 0, 8, 0);

        var refresh = TruckScaleButton("Refresh", "refresh", false);
        refresh.Height = 34;
        refresh.Margin = new Thickness(0);
        refresh.VerticalAlignment = VerticalAlignment.Bottom;

        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headerActions.Children.Add(FieldCompact("Search", _truckScaleSearchBox));
        headerActions.Children.Add(CreateTruckScaleFilter("Status", _truckScaleStage,
            GetStageChoices(), value => _truckScaleStage = value));
        headerActions.Children.Add(CreateTruckScaleFilter("Rubber type", _truckScaleRubberFilter,
            GetRubberFilterChoices(), value => _truckScaleRubberFilter = value));
        headerActions.Children.Add(FieldCompact("Date", dateButton));
        headerActions.Children.Add(refresh);

        var heroHeader = CreatePageHeroHeader(
            string.Empty,
            TruckScalePageTitle(),
            TruckScaleOperationSubtitle(),
            FluentIcons.Common.Icon.VehicleTruckProfile,
            headerActions);
        Grid.SetRow(heroHeader, 0);
        root.Children.Add(heroHeader);

        if (_truckScaleMessage.Length > 0)
        {
            var msgBanner = Banner(_truckScaleLoading ? "Loading" : "TruckScale unavailable", _truckScaleMessage, _truckScaleLoading ? "#0F766E" : "#DC2626");
            Grid.SetRow(msgBanner, 1);
            root.Children.Add(msgBanner);
        }

        _truckScaleMetricsHost = new ContentControl { Content = BuildTruckScaleMetrics() };
        Grid.SetRow(_truckScaleMetricsHost, 2);
        root.Children.Add(_truckScaleMetricsHost);

        var body = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var actionRow = new Grid { Margin = new Thickness(0, 0, 0, 7), VerticalAlignment = VerticalAlignment.Center };
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _truckScaleActionPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        actionRow.Children.Add(_truckScaleActionPanel);
        _truckScaleResultCount = Label("", 12, (Brush)FindResource("MutedBrush"));
        _truckScaleResultCount.Margin = new Thickness(12, 0, 2, 0);
        _truckScaleResultCount.HorizontalAlignment = HorizontalAlignment.Right;
        _truckScaleResultCount.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_truckScaleResultCount, 1);
        actionRow.Children.Add(_truckScaleResultCount);
        body.Children.Add(actionRow);

        _truckScaleGrid = CreateTruckScaleGrid();
        _truckScaleGrid.SelectionChanged += (_, _) =>
        {
            if (_truckScaleGrid.SelectedItem is TruckScaleGridRow row) _truckScaleSelectedId = row.Booking.Id;
            RefreshTruckScaleActionPanel();
        };
        _truckScaleGrid.MouseDoubleClick += async (_, _) =>
        {
            if (_truckScaleGrid.SelectedItem is TruckScaleGridRow row) await PerformNextTruckScaleActionAsync(row.Booking);
        };
        Grid.SetRow(_truckScaleGrid, 1);
        body.Children.Add(_truckScaleGrid);
        Grid.SetRow(body, 3);
        root.Children.Add(body);

        RefreshTruckScaleGridItems();
        RefreshTruckScaleActionPanel();
        return root;
    }

    private UIElement BuildTruckScaleSummaryView()
    {
        var page = new Grid { Margin = UiTokens.PageMargin, Height = GetTruckScaleViewportHeight(), MinHeight = 440 };
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var tools = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        tools.Children.Add(CreateTruckScaleDateButton(selected =>
        {
            _truckScaleDate = selected.Date;
            _truckScaleLoadedDate = DateTime.MinValue;
            _ = LoadTruckScaleDataAsync();
        }));
        var refreshButton = TruckScaleButton("Refresh", "refresh", false);
        refreshButton.Height = 34;
        refreshButton.Margin = new Thickness(8, 0, 0, 0);
        tools.Children.Add(refreshButton);
        var printButton = TruckScaleButton("Print", "print-summary", false);
        printButton.Height = 34;
        printButton.Margin = new Thickness(8, 0, 0, 0);
        tools.Children.Add(printButton);

        var summaryTitle = _truckScaleCategory == "all"
            ? "RRC · Queue Summary"
            : $"RRC · {TruckScaleCategoryName()} Queue Summary";

        var heroHeader = CreatePageHeroHeader(
            string.Empty,
            summaryTitle,
            "Historical queue volume, processing durations, and throughput analysis.",
            FluentIcons.Common.Icon.DataBarVertical,
            tools);
        Grid.SetRow(heroHeader, 0);
        page.Children.Add(heroHeader);

        var filterRow = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        var search = new TextBox { Text = _truckScaleSearch, Width = 250, Height = 34, FontSize = 13,
            VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(9, 4, 9, 4),
            Margin = new Thickness(0, 0, 8, 0) };
        search.TextChanged += (_, _) =>
        {
            _truckScaleSearch = search.Text;
            RefreshTruckScaleGridItems();
        };
        filterRow.Children.Add(FieldCompact("Search", search));
        filterRow.Children.Add(CreateTruckScaleFilter("Status", _truckScaleStage,
            GetStageChoices(), value => _truckScaleStage = value));
        filterRow.Children.Add(CreateTruckScaleFilter("Rubber type", _truckScaleRubberFilter,
            GetRubberFilterChoices(), value => _truckScaleRubberFilter = value));
        Grid.SetRow(filterRow, 1);
        page.Children.Add(filterRow);

        _truckScaleSummaryMetricsHost = new ContentControl
        {
            Content = BuildTruckScaleSummaryMetrics(),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(_truckScaleSummaryMetricsHost, 2);
        page.Children.Add(_truckScaleSummaryMetricsHost);

        _truckScaleGrid = CreateTruckScaleGrid();
        Grid.SetRow(_truckScaleGrid, 3);
        page.Children.Add(_truckScaleGrid);
        RefreshTruckScaleGridItems();
        return page;
    }

    private UIElement BuildTruckScaleMetrics()
    {
        var rows = GetVisibleTruckScaleBookings(applyStage: false, applySearch: false);
        var groups = new List<(string Key, string Label, string Color)>
        {
            ("all", "All queues", "#0F766E"),
            ("waiting", "Waiting", "#D97706"),
            ("arrived", "Checked in", "#0284C7")
        };
        if (_truckScaleCategory != "uss") groups.Add(("draining", "Draining", "#7C3AED"));
        groups.AddRange([
            ("pending_weight_in", "Weight in", "#2563EB"),
            ("pending_weight_out", "Weight out", "#0891B2"),
            ("completed", "Completed", "#16A34A")
        ]);
        var panel = new UniformGrid { Columns = groups.Count, Margin = new Thickness(-4, 1, -4, 7) };
        foreach (var group in groups)
        {
            var count = group.Key == "all" ? rows.Count : rows.Count(row => MatchesTruckScaleStage(row, group.Key));
            var accent = (Color)ColorConverter.ConvertFromString(group.Color)!;
            var isSelected = _truckScaleStage == group.Key;
            var content = new Grid { MinHeight = 54, VerticalAlignment = VerticalAlignment.Center };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = Label(group.Label, 12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
            var number = Label(count.ToString(CultureInfo.InvariantCulture), 25,
                new SolidColorBrush(accent), FontWeights.Bold);
            number.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(number, 1);
            content.Children.Add(label);
            content.Children.Add(number);

            var stripe = new Border
            {
                Width = 4,
                Background = new SolidColorBrush(accent),
                CornerRadius = UiTokens.CornerRadiusSmall,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var tileContent = new DockPanel();
            DockPanel.SetDock(stripe, Dock.Left);
            tileContent.Children.Add(stripe);
            tileContent.Children.Add(content);
            var card = new Border
            {
                Child = tileContent,
                Background = isSelected ? new SolidColorBrush(Color.FromArgb(20, accent.R, accent.G, accent.B)) : Brushes.White,
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromArgb(105, accent.R, accent.G, accent.B)) : (Brush)FindResource("BorderBrushSoft"),
                BorderThickness = new Thickness(1),
                CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
                Padding = new Thickness(10, 7, 11, 7),
                Margin = new Thickness(4, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var button = new Button
            {
                Content = card,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Tag = group.Key,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                ToolTip = "Filter by " + group.Label
            };
            button.Click += (_, _) => { _truckScaleStage = (string)button.Tag; RenderCurrentPage(); };
            panel.Children.Add(button);
        }
        return panel;
    }

    private UIElement BuildTruckScaleSummaryMetrics()
    {
        var rows = GetVisibleTruckScaleBookings(applyStage: false, applySearch: false);
        var cards = new (string Title, string Value, string Color)[]
        {
            ("Bookings", rows.Count.ToString("N0", TruckScaleCulture), "#0F766E"),
            ("Weight In", rows.Sum(row => row.WeightIn).ToString("N1", TruckScaleCulture) + " kg", "#2563EB"),
            ("Weight Out", rows.Sum(row => row.WeightOut).ToString("N1", TruckScaleCulture) + " kg", "#0891B2"),
            ("Gross total", rows.Sum(row => row.NetWeight).ToString("N1", TruckScaleCulture) + " kg", "#16A34A")
        };
        var panel = new UniformGrid { Columns = cards.Length, Margin = new Thickness(-4, 1, -4, 0) };
        foreach (var card in cards)
        {
            var content = new StackPanel();
            var title = Label(card.Title, 12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
            title.Margin = new Thickness(0);
            content.Children.Add(title);
            var value = Label(card.Value, 21, (Brush)FindResource("TextBrush"), FontWeights.Bold);
            value.Margin = new Thickness(0, 3, 0, 0);
            content.Children.Add(value);
            var tile = Card(content, card.Color);
            tile.Height = 60;
            if (tile.Child is DockPanel body && body.Children.Count > 1 && body.Children[1] is Border cardContent)
                cardContent.Padding = new Thickness(13, 5, 10, 5);
            tile.Margin = new Thickness(4, 0, 4, 0);
            panel.Children.Add(tile);
        }
        return panel;
    }

    private DataGrid CreateTruckScaleGrid()
    {
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeight = 39,
            FontSize = 13,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader), (Style)FindResource(typeof(DataGridColumnHeader)))
            {
                Setters =
                {
                    new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
                    new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center)
                }
            },
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalGridLinesBrush = (Brush)FindResource("BorderBrushSoft"),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AddTruckScaleColumn(grid, "Code", nameof(TruckScaleGridRow.SupplierCode), .55, 65, true);
        AddTruckScaleColumn(grid, "Name", nameof(TruckScaleGridRow.SupplierName), 1.6, 170);
        AddTruckScaleColumn(grid, "License plate", nameof(TruckScaleGridRow.TruckRegister), 1.45, 175);
        AddTruckScaleColumn(grid, "Truck type", nameof(TruckScaleGridRow.TruckType), 1.05, 120);
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Rubber type",
            CellTemplate = CreateTruckScaleRubberTypeCellTemplate(),
            Width = new DataGridLength(1.2, DataGridLengthUnitType.Star),
            MinWidth = 140
        });
        AddTruckScaleColumn(grid, "Rubber condition", nameof(TruckScaleGridRow.RubberCondition), .85, 95, true);
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Status",
            CellTemplate = CreateBadgeCellTemplate(nameof(TruckScaleGridRow.StatusBadge), 11, centered: true),
            Width = new DataGridLength(.95, DataGridLengthUnitType.Star),
            MinWidth = 110
        });
        AddTruckScaleColumn(grid, "Weight In", nameof(TruckScaleGridRow.WeightIn), .8, 75, true);
        AddTruckScaleColumn(grid, "Weight Out", nameof(TruckScaleGridRow.WeightOut), .8, 75, true);
        AddTruckScaleColumn(grid, "Gross", nameof(TruckScaleGridRow.NetWeight), .8, 75, true);
        AddTruckScaleColumn(grid, "Drain", nameof(TruckScaleGridRow.DrainDuration), .75, 70, true);
        if (_truckScaleShowReturns)
        {
            AddTruckScaleColumn(grid, "Return In", nameof(TruckScaleGridRow.ReturnWeightIn), .8, 75, true);
            AddTruckScaleColumn(grid, "Return Out", nameof(TruckScaleGridRow.ReturnWeightOut), .8, 75, true);
        }
        return grid;
    }

    private static void AddTruckScaleColumn(DataGrid grid, string header, string path, double starWeight, double minWidth, bool center = false)
    {
        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        elementStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        elementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        elementStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(center ? 4 : 8, 1, 4, 1)));
        elementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, center ? TextAlignment.Center : TextAlignment.Left));
        elementStyle.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(path)));

        var column = new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(path),
            Width = new DataGridLength(starWeight, DataGridLengthUnitType.Star),
            MinWidth = minWidth,
            ElementStyle = elementStyle
        };
        grid.Columns.Add(column);
    }

    private static DataTemplate CreateTruckScaleRubberTypeCellTemplate() =>
        (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <ItemsControl ItemsSource="{Binding RubberTypeBadges}" HorizontalAlignment="Center"
                              VerticalAlignment="Center" SnapsToDevicePixels="True">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <WrapPanel Orientation="Horizontal" HorizontalAlignment="Center" />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Margin="2,1" Padding="6,1" Background="{Binding Background}"
                                    BorderBrush="{Binding Border}" BorderThickness="1" CornerRadius="__SMALL_RADIUS__"
                                    HorizontalAlignment="Center" VerticalAlignment="Center">
                                <TextBlock Text="{Binding Label}" FontSize="11" FontWeight="SemiBold"
                                           Foreground="{Binding Foreground}" VerticalAlignment="Center" />
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </DataTemplate>
            """.Replace("__SMALL_RADIUS__", UiTokens.CornerRadiusSmallValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));

    private UIElement CreateTruckScaleFilter(string title, string selected, IReadOnlyList<TruckScaleChoice> choices, Action<string> assign)
    {
        var combo = new ComboBox { Width = title == "Rubber type" ? 150 : 130, Height = 34, Margin = new Thickness(0, 0, 8, 0),
            FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var choice in choices)
            combo.Items.Add(new ComboBoxItem { Content = choice.Label, Tag = choice.Value });
        combo.SelectedIndex = Math.Max(0, choices.ToList().FindIndex(choice => choice.Value == selected));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string value && value != selected)
            {
                assign(value);
                RenderCurrentPage();
            }
        };
        return FieldCompact(title, combo);
    }

    private Button CreateTruckScaleDateButton(Action<DateTime> onDateSelected)
    {
        var dateText = new TextBlock
        {
            Text = _truckScaleDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB")),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var button = new Button
        {
            Content = dateText,
            Width = UiTokens.DatePickerWidth,
            Height = UiTokens.DatePickerHeight,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = "Choose date",
            Cursor = Cursors.Hand
        };
        var calendar = new System.Windows.Controls.Calendar
        {
            DisplayDate = _truckScaleDate,
            SelectedDate = _truckScaleDate,
            SelectionMode = CalendarSelectionMode.SingleDate,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
            Style = CreateBookingQueueCalendarStyle()
        };
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Fade,
            Child = new Border
            {
                Background = Brushes.White,
                BorderBrush = CreateBrush("#D8E0EA"),
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
                Child = calendar
            }
        };
        button.Click += (_, _) =>
        {
            calendar.DisplayDate = _truckScaleDate;
            calendar.SelectedDate = _truckScaleDate;
            popup.IsOpen = true;
        };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not DateTime selected) return;
            dateText.Text = selected.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
            popup.IsOpen = false;
            if (selected.Date != _truckScaleDate.Date) onDateSelected(selected.Date);
        };
        return button;
    }

    private IReadOnlyList<TruckScaleChoice> GetStageChoices()
    {
        var choices = new List<TruckScaleChoice>
        {
            new("all", "All statuses"), new("waiting", "Waiting"), new("arrived", "Checked in")
        };
        if (_truckScaleCategory != "uss") choices.Add(new("draining", "Draining"));
        choices.AddRange([
            new("pending_weight_in", "Pending weight in"), new("pending_weight_out", "Pending weight out"),
            new("completed", "Completed"), new("cancelled", "Cancelled")
        ]);
        return choices;
    }

    private IReadOnlyList<TruckScaleChoice> GetRubberFilterChoices()
    {
        var items = new List<TruckScaleChoice> { new("all", "All rubber types") };
        items.AddRange(_truckScaleRubberTypes.Where(item => item.Value.Length > 0)
            .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase).Select(group => group.First()));
        if (items.All(item => item.Value != _truckScaleRubberFilter) && _truckScaleRubberFilter != "all")
            items.Add(new(_truckScaleRubberFilter, _truckScaleRubberFilter));
        return items;
    }

    private IReadOnlyList<TruckScaleChoice> GetTruckFilterChoices()
    {
        var items = new List<TruckScaleChoice> { new("all", "All truck types") };
        items.AddRange(_truckScaleBookings.Select(row => NormalizeTruckType(row.TruckType)).Where(value => value.Length > 0 && value != "-")
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(value => new TruckScaleChoice(value, value)));
        if (items.All(item => !item.Value.Equals(_truckScaleTruckFilter, StringComparison.OrdinalIgnoreCase)) && _truckScaleTruckFilter != "all")
            items.Add(new(_truckScaleTruckFilter, _truckScaleTruckFilter));
        return items;
    }

    private Button TruckScaleButton(string label, string action, bool primary)
    {
        var icon = CommonToolbarActionIcon(label);
        var button = icon is { } iconName
            ? IconOnlyActionButton(label, iconName)
            : new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 7, 0),
                Padding = new Thickness(12, 6, 12, 6),
                MinHeight = 32,
                Cursor = Cursors.Hand,
                Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
            };
        button.Tag = action;
        button.Click += TruckScaleButton_Click;
        return button;
    }

    private async void TruckScaleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string action) return;
        if (action == "refresh") { await LoadTruckScaleDataAsync(); return; }
        if (action == "print-summary") { PrintTruckScaleSummary(); return; }
        if (_truckScaleGrid?.SelectedItem is not TruckScaleGridRow row)
        {
            MessageBox.Show(this, "Select a queue first.", "TruckScale", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        switch (action)
        {
            case "next": await PerformNextTruckScaleActionAsync(row.Booking); break;
            case "return-weight-in": await SaveTruckScaleReturnWeightAsync(row.Booking, inbound: true); break;
            case "return-weight-out": await SaveTruckScaleReturnWeightAsync(row.Booking, inbound: false); break;
        }
    }

    private void RefreshTruckScaleActionPanel()
    {
        if (_truckScaleActionPanel is null) return;
        _truckScaleActionPanel.Children.Clear();
        var selected = _truckScaleGrid?.SelectedItem is TruckScaleGridRow row ? row.Booking : null;
        if (selected is null)
        {
            _truckScaleActionPanel.Children.Add(Label("Select a queue to see its available steps. Double-click a row to run its next step.",
                11, (Brush)FindResource("MutedBrush")));
            return;
        }

        _truckScaleActionPanel.Children.Add(Label($"Selected · {selected.Queue} · {selected.SupplierName} · {FormatTruckScaleLicensePlate(selected.TruckRegister)}",
            12, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        if (selected.IsCancelled)
            _truckScaleActionPanel.Children.Add(CreateTruckScaleStatusBadgeElement(selected.DisplayStatus));
        else if (selected.IsCompleted)
        {
            _truckScaleActionPanel.Children.Add(CreateTruckScaleStatusBadgeElement(selected.DisplayStatus));
            var showReturns = new CheckBox { Content = "Show return rubber weighing", IsChecked = _truckScaleShowReturns,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0), FontSize = 12 };
            showReturns.Checked += (_, _) => SetTruckScaleReturnWeighingVisible(true);
            showReturns.Unchecked += (_, _) => SetTruckScaleReturnWeighingVisible(false);
            _truckScaleActionPanel.Children.Add(showReturns);
            var returnWeightIn = TruckScaleButton("Return Weight In", "return-weight-in", false);
            var returnWeightOut = TruckScaleButton("Return Weight Out", "return-weight-out", false);
            returnWeightIn.Visibility = _truckScaleShowReturns ? Visibility.Visible : Visibility.Collapsed;
            returnWeightOut.Visibility = _truckScaleShowReturns ? Visibility.Visible : Visibility.Collapsed;
            _truckScaleActionPanel.Children.Add(returnWeightIn);
            _truckScaleActionPanel.Children.Add(returnWeightOut);
        }
        else
        {
            _truckScaleActionPanel.Children.Add(TruckScaleButton("Next step", "next", true));
        }
    }

    private void SetTruckScaleReturnWeighingVisible(bool visible)
    {
        _truckScaleShowReturns = visible;
        if (_truckScaleActionPanel is not null)
        {
            foreach (var button in _truckScaleActionPanel.Children.OfType<Button>()
                         .Where(button => button.Tag is string action && action.StartsWith("return-weight-", StringComparison.Ordinal)))
                button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_truckScaleGrid is null) return;
        foreach (var column in _truckScaleGrid.Columns.Where(column =>
                     column.Header?.ToString() is "Return In" or "Return Out").ToList())
            _truckScaleGrid.Columns.Remove(column);
        if (visible)
        {
            AddTruckScaleColumn(_truckScaleGrid, "Return In", nameof(TruckScaleGridRow.ReturnWeightIn), .8, 75, true);
            AddTruckScaleColumn(_truckScaleGrid, "Return Out", nameof(TruckScaleGridRow.ReturnWeightOut), .8, 75, true);
        }
    }

    private void RefreshTruckScaleGridItems()
    {
        if (_truckScaleGrid is null) return;
        var scrollViewer = FindVisualChild<ScrollViewer>(_truckScaleGrid);
        var verticalOffset = scrollViewer?.VerticalOffset ?? 0;
        var rows = GetVisibleTruckScaleBookings().Select(booking => new TruckScaleGridRow(booking)
        {
            MainRubberType = LookupRubberType(booking.RubberType),
            TrailerRubberType = booking.TrailerRubberType.Length > 0 ? LookupRubberType(booking.TrailerRubberType) : "",
            RubberCondition = TranslateTruckScaleCondition(booking.RubberCondition),
            ReturnWeightInValue = booking.ReturnWeightIn,
            ReturnWeightOutValue = booking.ReturnWeightOut
        }).ToList();
        _truckScaleGrid.ItemsSource = rows;
        if (_truckScaleSelectedId.Length > 0)
            _truckScaleGrid.SelectedItem = rows.FirstOrDefault(row => row.Booking.Id == _truckScaleSelectedId);
        if (_truckScaleResultCount is not null)
            _truckScaleResultCount.Text = $"{rows.Count} queue(s) · double-click to continue";
        RefreshTruckScaleActionPanel();
        if (scrollViewer is not null && verticalOffset > 0)
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => scrollViewer.ScrollToVerticalOffset(verticalOffset));
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) return match;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static UIElement CreateTruckScaleStatusBadgeElement(string status)
    {
        var badge = CreateTruckScaleStatusBadge(status);
        return new Border
        {
            Background = badge.Background,
            BorderBrush = badge.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            Padding = new Thickness(6, 1, 6, 1),
            Margin = new Thickness(6, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badge.Label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = badge.Foreground,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private static TruckScaleBadgeData CreateTruckScaleStatusBadge(string status)
    {
        var palette = status.Trim().ToUpperInvariant() switch
        {
            "COMPLETED" => ("#16804A", "#EAF7EF", "#B9E3C8"),
            "CANCELLED" => ("#B42318", "#FEF0EF", "#F4C7C3"),
            "WAITING" => ("#9A5B00", "#FFF5E6", "#F0D4A6"),
            "CHECKED IN" or "WEIGHED IN" or "PENDING WEIGHT IN" => ("#2457A6", "#EAF1FF", "#C5D5F6"),
            "DRAINING" => ("#7042A5", "#F3ECFB", "#D9C9EE"),
            _ => ("#475569", "#F1F5F9", "#CBD5E1")
        };
        return new(status, CreateBrush(palette.Item1), CreateBrush(palette.Item2), CreateBrush(palette.Item3));
    }

    private static TruckScaleBadgeData CreateTruckScaleRubberTypeBadge(string name)
    {
        var badge = CreateRubberTypeBadge(name);
        return new(badge.Label, badge.Foreground, badge.Background, badge.Border);
    }

    private List<TruckScaleBooking> GetVisibleTruckScaleBookings(bool applyStage = true, bool applySearch = true)
    {
        IEnumerable<TruckScaleBooking> rows = _truckScaleBookings;
        if (_truckScaleCategory == "uss") rows = rows.Where(row => row.IsUss);
        else if (_truckScaleCategory == "cuplump") rows = rows.Where(row => !row.IsUss);
        if (applyStage && _truckScaleStage != "all") rows = rows.Where(row => MatchesTruckScaleStage(row, _truckScaleStage));
        if (_truckScaleRubberFilter != "all") rows = rows.Where(row =>
            row.RubberType.Equals(_truckScaleRubberFilter, StringComparison.OrdinalIgnoreCase) ||
            row.TrailerRubberType.Equals(_truckScaleRubberFilter, StringComparison.OrdinalIgnoreCase));
        if (_truckScaleTruckFilter != "all") rows = rows.Where(row => NormalizeTruckType(row.TruckType)
            .Equals(_truckScaleTruckFilter, StringComparison.OrdinalIgnoreCase));
        var search = applySearch ? NormalizeTruckScaleSearch(_truckScaleSearch) : "";
        if (search.Length > 0)
            rows = rows.Where(row => new[] { row.Queue, row.BookingCode, row.SupplierCode, row.SupplierName, row.TruckRegister, row.TruckType, row.DateText }
                .Any(value => NormalizeTruckScaleSearch(value).Contains(search, StringComparison.OrdinalIgnoreCase)));
        return rows.OrderBy(row => row.SlotSort).ThenBy(row => row.QueueSort).ToList();
    }

    private static bool MatchesTruckScaleStage(TruckScaleBooking row, string stage) => stage switch
    {
        "waiting" => !row.IsArrived && !row.IsCancelled,
        "arrived" => row.IsArrived && !row.IsDraining && !row.HasDrainStopped && !row.HasWeightIn && !row.IsCompleted,
        "draining" => row.IsDraining,
        "pending_weight_in" => row.IsArrived && !row.HasWeightIn && (row.IsUss || row.HasDrainStopped),
        "pending_weight_out" => row.HasWeightIn && !row.HasWeightOut,
        "completed" => row.IsCompleted,
        "cancelled" => row.IsCancelled,
        _ => true
    };

    private static string NormalizeTruckScaleSearch(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string FormatTruckScaleLicensePlate(string registration)
    {
        var value = string.Join(' ', registration.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        for (var index = 1; index < value.Length - 1; index++)
        {
            if (char.IsWhiteSpace(value[index]) && char.IsDigit(value[index - 1]) && char.IsDigit(value[index + 1]))
                return string.Concat(value.AsSpan(0, index), "-", value.AsSpan(index + 1));
        }
        return value;
    }

    private async Task LoadTruckScaleDataAsync(bool forceLookups = false, bool quietRefresh = false)
    {
        if (_session.IsPreview || !_currentPage.StartsWith("RRC:", StringComparison.Ordinal)) return;
        var version = ++_truckScaleLoadVersion;
        var date = _truckScaleDate.Date;
        _truckScaleLoading = true;
        if (!quietRefresh)
        {
            _truckScaleMessage = "Loading bookings…";
            RenderCurrentPage();
        }

        var bookingsTask = _apiClient.GetAsync($"/api/bookings?date={date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", _session.Token);
        Task<ApiResponse<JsonElement>>? rubberTask = null;
        Task<ApiResponse<JsonElement>>? provinceTask = null;
        if (forceLookups || !_truckScaleLookupsLoaded)
        {
            rubberTask = _apiClient.GetAsync("/api/rubber-types", _session.Token);
            provinceTask = _apiClient.GetAsync("/api/master/provinces", _session.Token);
        }
        await bookingsTask;
        if (rubberTask is not null && provinceTask is not null) await Task.WhenAll(rubberTask, provinceTask);
        if (version != _truckScaleLoadVersion) return;

        var refreshVisibleData = true;
        var result = await bookingsTask;
        if (!result.Success || result.Data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            _truckScaleMessage = result.Error.Length > 0 ? result.Error : "Could not load the booking queue.";
            if (quietRefresh)
            {
                if (_truckScaleResultCount is not null)
                    _truckScaleResultCount.ToolTip = $"Automatic refresh failed: {_truckScaleMessage}";
                refreshVisibleData = false;
            }
            else _truckScaleBookings.Clear();
        }
        else
        {
            var refreshedBookings = ExtractArray(result.Data)
                .Select(TruckScaleBooking.From)
                .Where(booking => booking.Id.Length > 0)
                .ToList();
            var bookingsChanged = !AreTruckScaleBookingsEqual(_truckScaleBookings, refreshedBookings);
            if (!quietRefresh || bookingsChanged)
            {
                _truckScaleBookings.Clear();
                _truckScaleBookings.AddRange(refreshedBookings);
            }
            _truckScaleMessage = "";
            if (_truckScaleResultCount is not null) _truckScaleResultCount.ToolTip = null;
            refreshVisibleData = bookingsChanged;
        }

        if (rubberTask is not null && provinceTask is not null)
        {
            var rubberResult = await rubberTask;
            if (!rubberResult.Success || rubberResult.Data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined || ExtractArray(rubberResult.Data).Count == 0)
                rubberResult = await _apiClient.GetAsync("/api/master/rubber-types", _session.Token);
            _truckScaleRubberTypes.Clear();
            if (rubberResult.Success && rubberResult.Data.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                foreach (var item in ExtractArray(rubberResult.Data))
                {
                    var code = ReadProperty(item, "code", "rubberTypeCode", "rubber_type_code", "id");
                    var name = ReadProperty(item, "name", "name_th", "rubberTypeName", "rubber_type_name", "label");
                    if (code.Length > 0) _truckScaleRubberTypes.Add(new(code, name.Length > 0 ? name : code));
                }
            }
            var provinceResult = await provinceTask;
            _truckScaleProvinces.Clear();
            if (provinceResult.Success && provinceResult.Data.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
            {
                foreach (var item in ExtractArray(provinceResult.Data))
                {
                    var name = ReadProperty(item, "name_th", "nameTh", "name", "province_name");
                    var value = FirstNonEmpty(ReadProperty(item, "name_en", "nameEn"), name);
                    if (name.Length > 0) _truckScaleProvinces.Add(new(value, name));
                }
            }
            _truckScaleLookupsLoaded = true;
        }

        _truckScaleLoading = false;
        _truckScaleLoadedDate = date;
        if (quietRefresh && !refreshVisibleData) return;
        if (_currentPage is TruckScalePageId or TruckScaleSummaryPageId)
        {
            if (quietRefresh) RefreshTruckScalePageDataInPlace();
            else RenderCurrentPage();
        }
    }

    private void RefreshTruckScalePageDataInPlace()
    {
        if (_currentPage == TruckScalePageId && _truckScaleMetricsHost is not null)
            _truckScaleMetricsHost.Content = BuildTruckScaleMetrics();
        if (_currentPage == TruckScaleSummaryPageId && _truckScaleSummaryMetricsHost is not null)
            _truckScaleSummaryMetricsHost.Content = BuildTruckScaleSummaryMetrics();
        RefreshTruckScaleGridItems();
    }

    private static bool AreTruckScaleBookingsEqual(
        IReadOnlyCollection<TruckScaleBooking> current,
        IReadOnlyCollection<TruckScaleBooking> refreshed)
    {
        if (current.Count != refreshed.Count) return false;
        var currentPayloads = current
            .Select(booking => (booking.Id, Payload: booking.Raw.GetRawText()))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var refreshedPayloads = refreshed
            .Select(booking => (booking.Id, Payload: booking.Raw.GetRawText()))
            .OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return currentPayloads.SequenceEqual(refreshedPayloads);
    }

    private async void TruckScaleRefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_currentPage is TruckScalePageId or TruckScaleSummaryPageId && !_truckScaleLoading)
            await LoadTruckScaleDataAsync(quietRefresh: true);
    }

    private void TruckScaleSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        var query = (_truckScaleSearchBox?.Text ?? "").Trim();
        if (query.Length == 0) return;
        var normalized = NormalizeTruckScaleSearch(query);
        var match = GetVisibleTruckScaleBookings(applyStage: false, applySearch: false).FirstOrDefault(row =>
                                                              NormalizeTruckScaleSearch(row.BookingCode) == normalized ||
                                                              NormalizeTruckScaleSearch(row.Id) == normalized ||
                                                              NormalizeTruckScaleSearch(row.Queue) == normalized);
        if (match is null)
        {
            MessageBox.Show(this, "No booking matches that scanned code.", "TruckScale", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _truckScaleStage = "all";
        _truckScaleSearch = "";
        _truckScaleSelectedId = match.Id;
        RefreshTruckScaleGridItems();
        if (_truckScaleGrid?.SelectedItem is TruckScaleGridRow row) _ = PerformNextTruckScaleActionAsync(row.Booking);
    }

    private void PrintTruckScaleSummary()
    {
        var rows = GetVisibleTruckScaleBookings();
        var date = _truckScaleDate;
        var category = TruckScaleCategoryName();

        AppPrintDialog.ShowPrint(
            owner: Window.GetWindow(this),
            documentTitle: $"RRC · {category} TruckScale Queue Summary",
            flowDocFactory: () =>
            {
                var document = new System.Windows.Documents.FlowDocument
                {
                    PagePadding = new Thickness(28),
                    FontFamily = new FontFamily("Segoe UI, Arial, sans-serif")
                };
                document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"RRC · {category} TruckScale Queue Summary"))
                { FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 4) });
                document.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Date: {date:dd MMM yyyy}    Queues: {rows.Count}    Gross weight: {rows.Sum(row => row.NetWeight):N1} kg"))
                { FontSize = 10, Margin = new Thickness(0, 0, 0, 14) });
                var table = new System.Windows.Documents.Table { CellSpacing = 0 };
                var widths = new[] { 52d, 80d, 155d, 105d, 95d, 62d, 62d, 62d };
                foreach (var width in widths) table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(width) });
                var group = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(group);
                var headers = new[] { "Queue", "Slot", "Supplier", "License plate", "Type", "In kg", "Out kg", "Gross kg" };
                group.Rows.Add(CreateTruckScalePrintRow(headers, true));
                foreach (var row in rows) group.Rows.Add(CreateTruckScalePrintRow([
                    row.Queue, row.TimeSlot, row.SupplierName, FormatTruckScaleLicensePlate(row.TruckRegister), row.TruckType,
                    row.WeightIn.ToString("N1"), row.WeightOut.ToString("N1"), row.NetWeight.ToString("N1")
                ], false));
                document.Blocks.Add(table);
                return document;
            });
    }

    private static System.Windows.Documents.TableRow CreateTruckScalePrintRow(IReadOnlyList<string> values, bool header)
    {
        var row = new System.Windows.Documents.TableRow();
        foreach (var value in values)
        {
            var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(value)))
            {
                Padding = new Thickness(4, 3, 4, 3),
                BorderBrush = CreateBrush("#CBD5E1"),
                BorderThickness = new Thickness(0, 0, 0.5, 0.5),
                Background = header ? CreateBrush("#EAF0F6") : Brushes.White,
                FontSize = 8,
                FontWeight = header ? FontWeights.Bold : FontWeights.Normal
            };
            row.Cells.Add(cell);
        }
        return row;
    }

    private double GetTruckScaleViewportHeight()
    {
        var viewport = WorkspaceScrollViewer.ViewportHeight;
        if (!double.IsFinite(viewport) || viewport < 100) viewport = WorkspaceScrollViewer.ActualHeight;
        return double.IsFinite(viewport) && viewport >= 100 ? Math.Max(420, viewport - (UiTokens.PageMargin.Top + UiTokens.PageMargin.Bottom)) : 660;
    }

    private void UpdateTruckScalePageHeight()
    {
        if (_truckScalePageHost is not null) _truckScalePageHost.Height = GetTruckScaleViewportHeight();
    }

    private void RefreshTruckScalePage() => _ = LoadTruckScaleDataAsync();

    private sealed record TruckScaleChoice(string Value, string Label);
    private sealed record TruckScaleBadgeData(string Label, Brush Foreground, Brush Background, Brush Border);

    private sealed record TruckScaleGridRow(TruckScaleBooking Booking)
    {
        public string MainRubberType { get; init; } = "—";
        public string TrailerRubberType { get; init; } = "";
        public string RubberCondition { get; init; } = "—";
        public double ReturnWeightInValue { get; init; }
        public double ReturnWeightOutValue { get; init; }
        public string Queue => Booking.Queue;
        public string TimeSlot => Booking.TimeSlot;
        public string SupplierCode => Booking.SupplierCode;
        public string SupplierName => Booking.SupplierName;
        public string TruckRegister => FormatTruckScaleLicensePlate(Booking.TruckRegister);
        public string TruckType => NormalizeTruckType(Booking.TruckType);
        public IReadOnlyList<TruckScaleBadgeData> RubberTypeBadges => new[] { MainRubberType, TrailerRubberType }
            .Where(value => value.Length > 0)
            .Select(value => CreateTruckScaleRubberTypeBadge(value.Replace('_', ' ')))
            .ToArray();
        public string Status => Booking.DisplayStatus;
        public TruckScaleBadgeData StatusBadge => CreateTruckScaleStatusBadge(Status);
        public string WeightIn => Booking.WeightIn > 0 ? Booking.WeightIn.ToString("N1", TruckScaleCulture) : "—";
        public string WeightOut => Booking.WeightOut > 0 ? Booking.WeightOut.ToString("N1", TruckScaleCulture) : "—";
        public string NetWeight => Booking.NetWeight > 0 ? Booking.NetWeight.ToString("N1", TruckScaleCulture) : "—";
        public string DrainDuration => Booking.DrainDurationText;
        public string ReturnWeightIn => ReturnWeightInValue > 0 ? ReturnWeightInValue.ToString("N1", TruckScaleCulture) : "—";
        public string ReturnWeightOut => ReturnWeightOutValue > 0 ? ReturnWeightOutValue.ToString("N1", TruckScaleCulture) : "—";
        public string Note => Booking.Note.Length > 0 ? Booking.Note : "—";
    }

    private sealed class TruckScaleBooking
    {
        public required JsonElement Raw { get; init; }
        public string Id => ReadProperty(Raw, "id", "bookingId", "booking_id");
        public string BookingCode => ReadProperty(Raw, "bookingCode", "booking_code");
        public string QueueNo => ReadProperty(Raw, "queueNo", "queue_no");
        public string Queue
        {
            get
            {
                var value = QueueNo;
                if (value.Length == 0) return BookingCode.Length > 0 ? BookingCode : "—";
                if (value.StartsWith("C", StringComparison.OrdinalIgnoreCase) || value.StartsWith("U", StringComparison.OrdinalIgnoreCase)) return value;
                var prefix = BookingCode.StartsWith("U", StringComparison.OrdinalIgnoreCase) ? "U" : "C";
                return prefix + value;
            }
        }
        public string DateText => ReadProperty(Raw, "date");
        public string SupplierCode => ReadProperty(Raw, "supplierCode", "supplier_code");
        public string SupplierName => FirstNonEmpty(ReadProperty(Raw, "supplierName", "supplier_name"), ReadNestedProperty(Raw, "supplier", "name"), "—");
        public string TimeSlot
        {
            get
            {
                var start = ReadProperty(Raw, "startTime", "start_time", "slot");
                var end = ReadProperty(Raw, "endTime", "end_time");
                if (start.Contains('-')) return start;
                return start.Length > 0 && end.Length > 0 ? $"{start}-{end}" : FirstNonEmpty(start, "—");
            }
        }
        public string TruckType => ReadProperty(Raw, "truckType", "truck_type");
        public string TruckRegister => ReadProperty(Raw, "truckRegister", "truck_register");
        public string TrailerRegister => ReadProperty(Raw, "trailerRegister", "trailer_register");
        public string RubberType => ReadProperty(Raw, "rubberType", "rubber_type");
        public string TrailerRubberType => ReadProperty(Raw, "trailerRubberType", "trailer_rubber_type");
        public string RubberSource => ReadProperty(Raw, "rubberSource", "rubber_source");
        public string TrailerRubberSource => ReadProperty(Raw, "trailerRubberSource", "trailer_rubber_source");
        public string RubberCondition => ReadProperty(Raw, "rubberCondition", "rubber_condition");
        public string Note => ReadProperty(Raw, "note");
        public string Status => ReadProperty(Raw, "status");
        public string SplitType => ReadProperty(Raw, "splitType", "split_type");
        public DateTime? CheckInAt => ParseDate(ReadProperty(Raw, "checkinAt", "checkin_at"));
        public DateTime? StartDrainAt => ParseDate(ReadProperty(Raw, "startDrainAt", "start_drain_at"));
        public DateTime? StopDrainAt => ParseDate(ReadProperty(Raw, "stopDrainAt", "stop_drain_at"));
        public DateTime? WeightInAt => ParseDate(ReadProperty(Raw, "weightInAt", "weight_in_at"));
        public DateTime? WeightOutAt => ParseDate(ReadProperty(Raw, "weightOutAt", "weight_out_at"));
        public double WeightIn => Number("weightIn", "weight_in") + Number("trailerWeightIn", "trailer_weight_in");
        public double WeightOut => Number("weightOut", "weight_out") + Number("trailerWeightOut", "trailer_weight_out");
        public double ReturnWeightIn => Number("returnWeightIn", "return_weight_in") + Number("returnTrailerWeightIn", "return_trailer_weight_in");
        public double ReturnWeightOut => Number("returnWeightOut", "return_weight_out") + Number("returnTrailerWeightOut", "return_trailer_weight_out");
        public double NetWeight => WeightIn > 0 && WeightOut > 0 ? Math.Abs(WeightIn - WeightOut) : 0;
        public bool HasWeightIn => WeightIn > 0;
        public bool HasWeightOut => WeightOut > 0;
        public bool IsCancelled => Status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase);
        public bool IsCompleted => HasWeightOut || Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
        public bool IsUss => BookingCode.StartsWith("U", StringComparison.OrdinalIgnoreCase) ||
            RubberType.Contains("USS", StringComparison.OrdinalIgnoreCase) ||
            ReadProperty(Raw, "category", "bookingType", "booking_type").Contains("USS", StringComparison.OrdinalIgnoreCase);
        public bool IsArrived => CheckInAt is not null || Status.Equals("CHECKED_IN", StringComparison.OrdinalIgnoreCase) ||
            Status.Equals("DRAINING", StringComparison.OrdinalIgnoreCase) || Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
        public bool IsDraining => StartDrainAt is not null && StopDrainAt is null;
        public bool HasDrainStopped => StopDrainAt is not null;
        public int SlotSort => TimeSlot.Length >= 5 && TimeSpan.TryParse(TimeSlot[..5], CultureInfo.InvariantCulture, out var time) ? (int)time.TotalMinutes : int.MaxValue;
        public int QueueSort => int.TryParse(new string(Queue.Where(char.IsDigit).ToArray()), out var number) ? number : int.MaxValue;
        public string DisplayStatus => IsCancelled ? "Cancelled" : IsCompleted ? "Completed" : HasWeightIn ? "Weighed In" :
            IsDraining ? "Draining" : HasDrainStopped ? "Pending Weight In" : IsArrived ? "Checked In" : "Waiting";

        public string DrainDurationText
        {
            get
            {
                if (StartDrainAt is null) return "—";
                var end = StopDrainAt ?? DateTime.Now;
                var duration = end - StartDrainAt.Value;
                return $"{Math.Max(0, (int)duration.TotalHours):00}:{duration.Minutes:00}" + (IsDraining ? " · live" : "");
            }
        }

        private double Number(params string[] keys) => double.TryParse(ReadProperty(Raw, keys), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : 0;
        private static DateTime? ParseDate(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var date) ? (date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date) : null;
        public static TruckScaleBooking From(JsonElement raw) => new() { Raw = raw.Clone() };
    }

    private string LookupRubberType(string code)
    {
        if (code.Length == 0) return "—";
        return _truckScaleRubberTypes.FirstOrDefault(item => item.Value.Equals(code, StringComparison.OrdinalIgnoreCase) ||
            item.Label.Equals(code, StringComparison.OrdinalIgnoreCase))?.Label ?? code;
    }

    private static string TranslateTruckScaleCondition(string value) => value.Trim().ToUpperInvariant() switch
    {
        "DRY" or "แห้ง" => "แห้ง", "DAMP" or "หมาด" => "หมาด", "WET" or "เปียก" => "เปียก", _ => value.Length > 0 ? value : "—"
    };

    private static string NormalizeTruckType(string value)
    {
        var normalized = value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "pickup" => "Pickup", "6-wheel" => "6 Wheels", "10-wheel" => "10 Wheels",
            "18-wheel" => "10 Wheels Trailer", "trailer" => "Trailer", _ => normalized
        };
    }
}

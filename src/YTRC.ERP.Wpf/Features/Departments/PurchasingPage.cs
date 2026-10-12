using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using FluentIcons.Common;
using FluentIcons.Wpf;
using Symbol = FluentIcons.Common.Icon;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private static readonly (string Key, string Label, string Description, string? ApiRoute)[] PurchasingSections =
    [
        ("Cuplump", "Cuplump", "Cuplump purchasing workspace.", null),
        ("USS", "USS", "USS purchasing workspace.", null),
        ("Supplier", "Supplier", "Supplier directory and purchasing relationships.", "/api/suppliers"),
        ("Rubber Type", "Rubber Type", "Rubber type master data and purchasing specifications.", "/api/rubber-types"),
        ("Order", "Order", "Purchase orders and requisitions.", "/api/orders"),
        ("Marking Preparation", "Marking Preparation", "Marking preparation workspace.", null)
    ];

    private static readonly (string Section, string Key, string Label, string Description)[] PurchasingActions =
    [
        ("Cuplump", "CreateBooking", "Create Booking", "Start a new Cuplump booking."),
        ("Cuplump", "ManageBookings", "Manage Bookings", "Review and maintain Cuplump bookings."),
        ("Cuplump", "Dashboard", "Dashboard", "Review Cuplump booking, slot, and lot activity."),
        ("USS", "CreateBooking", "Create Booking", "Start a new USS booking."),
        ("USS", "ManageBookings", "Manage Bookings", "Review and maintain USS bookings."),
        ("USS", "Dashboard", "Dashboard", "Review USS bookings, slots, and weight activity."),
        ("Supplier", "AddSupplier", "Add Supplier", "Add a supplier to the purchasing directory."),
        ("Supplier", "ManageSuppliers", "Manage Suppliers", "Review and maintain supplier records."),
        ("Supplier", "SuppliersSummary", "Suppliers Summary", "Review supplier purchasing information."),
        ("Rubber Type", "AddRubberType", "Add Rubber Type", "Add a rubber type to purchasing master data."),
        ("Rubber Type", "ManageRubberTypes", "Manage Rubber Types", "Review and maintain rubber type records."),
        ("Order", "CreateOrder", "Create Order", "Start a new purchase order."),
        ("Order", "ManageOrders", "Manage Orders", "Review and maintain purchase orders."),
        ("Order", "OrdersSummary", "Orders Summary", "Review purchase order activity.")
    ];

    private sealed record PurchasingRecord(
        JsonElement Raw, string Id, string Code, string Name, string Category, string Province, string Contact,
        string RubberTypes, string OrderNo, string Supplier, string GrossWeight, string Drc,
        string Price, string Status, string Date, bool IsDeleted)
    {
        public string SupplierCode { get; init; } = "";

        public IReadOnlyList<PurchasingBadge> RubberTypeBadges => string.IsNullOrWhiteSpace(RubberTypes)
            ? [CreateBadge("—", "#64748B", "#F1F5F9", "#CBD5E1")]
            : RubberTypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(CreateRubberTypeBadge).ToArray();

        public PurchasingBadge RubberTypeBadge => CreateRubberTypeBadge(Name);
        public PurchasingBadge CategoryBadge => CreateRubberCategoryBadge(Category);
        public PurchasingBadge StatusBadge => CreateStatusBadge(Status);
    }

    private sealed record PurchasingBadge(string Label, Brush Foreground, Brush Background, Brush Border);

    private static PurchasingBadge CreateRubberTypeBadge(string label)
    {
        var key = label.Replace('_', ' ').Trim();
        var palette = key.ToUpperInvariant() switch
        {
            "REGULAR CL" => ("#0F766E", "#EAF4F2", "#C4DCD6"),
            "REGULAR USS" => ("#9A5B00", "#FFF5E6", "#F0D4A6"),
            "EUDR CL" => ("#2457A6", "#EAF1FF", "#C5D5F6"),
            "EUDR USS" => ("#7042A5", "#F3ECFB", "#D9C9EE"),
            "NORTH EAST CL" => ("#475569", "#F1F5F9", "#CBD5E1"),
            _ => GetFallbackBadgePalette(key)
        };
        return CreateBadge(label, palette.Item1, palette.Item2, palette.Item3);
    }

    private static PurchasingBadge CreateStatusBadge(string status)
    {
        var key = status.Trim().ToUpperInvariant();
        var palette = key switch
        {
            "ACTIVE" => ("#16804A", "#EAF7EF", "#B9E3C8"),
            "DELETED" or "REJECTED" or "CANCELLED" => ("#B42318", "#FEF0EF", "#F4C7C3"),
            "INACTIVE" => ("#64748B", "#F1F5F9", "#CBD5E1"),
            "SUSPENDED" or "ON HOLD" => ("#A15C00", "#FFF5E6", "#F0D4A6"),
            "WAITINGAPPROVAL" or "PENDING" => ("#2457A6", "#EAF1FF", "#C5D5F6"),
            "APPROVED" or "COMPLETED" => ("#0F766E", "#EAF4F2", "#C4DCD6"),
            _ => GetFallbackBadgePalette(key)
        };
        return CreateBadge(status, palette.Item1, palette.Item2, palette.Item3);
    }

    private static PurchasingBadge CreateRubberCategoryBadge(string category)
    {
        var palette = category.Trim().ToUpperInvariant() switch
        {
            "CUPLUMP" => ("#0F766E", "#EAF4F2", "#C4DCD6"),
            "USS" => ("#2457A6", "#EAF1FF", "#C5D5F6"),
            _ => GetFallbackBadgePalette(category)
        };
        return CreateBadge(category, palette.Item1, palette.Item2, palette.Item3);
    }

    private static PurchasingBadge CreateBadge(string label, string foreground, string background, string border)
        => new(label, CreateBrush(foreground), CreateBrush(background), CreateBrush(border));

    private static DataTemplate CreateBadgeCellTemplate(string badgePath, double fontSize = 11.5,
        bool centered = false) =>
        (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Border Padding="6,1" Background="{Binding __BADGE__.Background}"
                        BorderBrush="{Binding __BADGE__.Border}" BorderThickness="1" CornerRadius="__SMALL_RADIUS__"
                        HorizontalAlignment="__ALIGN__" VerticalAlignment="Center">
                    <TextBlock Text="{Binding __BADGE__.Label}" FontSize="__FONTSIZE__" FontWeight="SemiBold"
                               Foreground="{Binding __BADGE__.Foreground}" VerticalAlignment="Center" />
                </Border>
            </DataTemplate>
            """.Replace("__BADGE__", badgePath, StringComparison.Ordinal)
            .Replace("__SMALL_RADIUS__", UiTokens.CornerRadiusSmallValue.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__FONTSIZE__", fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__ALIGN__", centered ? "Center" : "Left", StringComparison.Ordinal));

    private static Brush CreateBrush(string color)
        => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);

    private static (string Foreground, string Background, string Border) GetFallbackBadgePalette(string value)
    {
        string[][] palette =
        [
            ["#0F766E", "#EAF4F2", "#C4DCD6"],
            ["#2457A6", "#EAF1FF", "#C5D5F6"],
            ["#7042A5", "#F3ECFB", "#D9C9EE"],
            ["#9A5B00", "#FFF5E6", "#F0D4A6"],
            ["#B42318", "#FEF0EF", "#F4C7C3"],
            ["#475569", "#F1F5F9", "#CBD5E1"]
        ];
        var hash = 0;
        foreach (var character in value.ToUpperInvariant())
            hash = unchecked(hash * 31 + character);
        var selected = palette[(int)((uint)hash % (uint)palette.Length)];
        return (selected[0], selected[1], selected[2]);
    }

    private UIElement BuildPurchasingPage()
    {
        var info = DepartmentCatalog.Get(DepartmentId.Purchase);
        var page = PageStack();
        if (!_session.User.HasDepartmentAccess(DepartmentId.Purchase))
        {
            page.Children.Add(TitleBlock(info.Name, info.Description));
            page.Children.Add(Banner("Access restricted", "Your account does not have permission to view this department. Contact your administrator.", "#DC2626"));
            return page;
        }

        var section = PurchasingSections.FirstOrDefault(item => item.Key == _purchasingSection);
        if (section == default) section = PurchasingSections[0];
        if (section.Key is "Cuplump" or "USS")
        {
            page.Margin = new Thickness(28, 8, 28, 30);
            page.Children.Add(BuildBookingQueuePage(section.Key));
            return page;
        }
        if (section.Key == "Marking Preparation")
        {
            page.Margin = new Thickness(28, 8, 28, 30);
            page.Children.Add(BuildMarkingPreparationPage());
            return page;
        }        if (section.Key == "Rubber Type")
        {
            page.Margin = new Thickness(28, 12, 28, 30);
            page.Children.Add(BuildRubberTypesStudio());
            return page;
        }
        if (section.Key is "Supplier" or "Order")
        {
            if (section.Key == "Supplier")
                page.Margin = new Thickness(28, 12, 28, 30);
            page.Children.Add(BuildPurchasingManagement(section));
            return page;
        }

        page.Children.Add(TitleBlock(info.Name, info.Description));

        var action = PurchasingActions.FirstOrDefault(item =>
            item.Section == section.Key && item.Key == _purchasingAction);
        if (action != default)
        {
            page.Children.Add(SectionHeading($"{section.Label} · {action.Label}"));
            page.Children.Add(Label(action.Description, 13, (Brush)FindResource("MutedBrush")));
            page.Children.Add(Banner(_apiOnline ? "Workflow is not connected yet" : "ERP service is not connected",
                "The API contract for this purchasing workflow is not available in the current client.", _apiOnline ? info.Accent : "#526F91"));
            page.Children.Add(SectionHeading("Workspace"));
            page.Children.Add(EmptyState($"{action.Label} is not available yet",
                "The menu and workspace are ready. Live data and transaction forms will appear here when the ERP workflow contract is implemented."));
            return page;
        }

        page.Children.Add(SectionHeading(section.Label));
        page.Children.Add(Label(section.Description, 13, (Brush)FindResource("MutedBrush")));
        page.Children.Add(Banner(_apiOnline ? "ERP service connected" : "ERP service is not connected",
                "An API route for this section is not defined in the current client contract.", _apiOnline ? info.Accent : "#526F91"));
        page.Children.Add(SectionHeading("Records & activity"));
        page.Children.Add(EmptyState($"No live {section.Label} records are available",
            "This section is ready for future ERP workflows. Its data route and read/write forms have not been defined yet."));
        return page;
    }

    private UIElement BuildRubberTypesStudio()
    {
        _purchasingProvinceFilter = null;
        _purchasingRubberTypeFilter = null;
        var panel = new StackPanel();

        // 1. Top Header Row
        var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };

        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var activeCount = _purchasingAllRecords.Count(r => !r.IsDeleted);
        var countBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var cbStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        cbStack.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        cbStack.Children.Add(new TextBlock { Text = $"{activeCount} Active Grades", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87)), VerticalAlignment = VerticalAlignment.Center });
        countBadge.Child = cbStack;
        headerActions.Children.Add(countBadge);

        var addBtn = new Button
        {
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("PrimaryButton"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var addStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        addStack.Children.Add(new FluentIcon { Icon = Symbol.Add, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        addStack.Children.Add(new TextBlock { Text = "Add Rubber Type", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        addBtn.Content = addStack;
        addBtn.Click += async (_, _) => await OpenPurchasingEditorAsync("Rubber Type", null);
        headerActions.Children.Add(addBtn);

        var refreshBtn = new Button
        {
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var refStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        refStack.Children.Add(new FluentIcon { Icon = Symbol.ArrowClockwise, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        refStack.Children.Add(new TextBlock { Text = "Refresh", VerticalAlignment = VerticalAlignment.Center });
        refreshBtn.Content = refStack;
        refreshBtn.Click += async (_, _) => await LoadPurchasingRecordsAsync();
        headerActions.Children.Add(refreshBtn);

        DockPanel.SetDock(headerActions, Dock.Right);
        headerDock.Children.Add(headerActions);

        var titleLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBox.Child = new FluentIcon { Icon = Symbol.Tag, IconSize = IconSize.Size20, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        titleLeft.Children.Add(iconBox);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = "Rubber Types Management", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });
        textStack.Children.Add(new TextBlock { Text = "Configure raw rubber classifications, category mappings (Cuplump & USS), and active grading rules.", FontSize = 12.5, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 2, 0, 0) });
        titleLeft.Children.Add(textStack);
        headerDock.Children.Add(titleLeft);
        panel.Children.Add(headerDock);

        // 2. Metrics Bar
        var cuplumpCount = _purchasingAllRecords.Count(r => !r.IsDeleted && r.Category.Equals("Cuplump", StringComparison.OrdinalIgnoreCase));
        var ussCount = _purchasingAllRecords.Count(r => !r.IsDeleted && r.Category.Equals("USS", StringComparison.OrdinalIgnoreCase));
        var metricsGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
        metricsGrid.Children.Add(CreatePurchasingMetricCard("Configured Rubber Types", activeCount.ToString(), Symbol.Tag, "#0F766E", "#F0FDFA", "#CCFBF1"));
        metricsGrid.Children.Add(CreatePurchasingMetricCard("Cuplump Grade Types", cuplumpCount.ToString(), Symbol.Box, "#059669", "#F0FDF4", "#BBF7D0"));
        metricsGrid.Children.Add(CreatePurchasingMetricCard("USS Grade Types", ussCount.ToString(), Symbol.Document, "#2563EB", "#EFF6FF", "#BFDBFE"));
        metricsGrid.Children.Add(CreatePurchasingMetricCard("All Grades Ready", "100%", Symbol.ShieldCheckmark, "#7C3AED", "#FAF5FF", "#E9D5FF"));
        panel.Children.Add(metricsGrid);

        // 3. Master-Detail Studio Grid
        var studioGrid = new Grid();
        studioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
        studioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        studioGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });

        // Left Panel: Directory Card
        var dirCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true
        };
        var dirDock = new DockPanel();

        // Card Header
        var dirHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 11, 16, 11)
        };
        var dhDock = new DockPanel();
        var dhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        dhTitle.Children.Add(new FluentIcon { Icon = Symbol.DocumentBulletList, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        dhTitle.Children.Add(new TextBlock { Text = "Rubber Types Directory", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        dhDock.Children.Add(dhTitle);

        var listPill = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        var listCountText = new TextBlock { Text = $"{activeCount} types", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("MutedBrush") };
        listPill.Child = listCountText;
        DockPanel.SetDock(listPill, Dock.Right);
        dhDock.Children.Add(listPill);
        dirHeader.Child = dhDock;
        DockPanel.SetDock(dirHeader, Dock.Top);
        dirDock.Children.Add(dirHeader);

        // Filter Bar
        var filterBar = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var fbDock = new DockPanel();

        _purchasingShowDeletedCheckBox = new CheckBox
        {
            Content = "Include deleted",
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Foreground = (Brush)FindResource("MutedBrush")
        };
        _purchasingShowDeletedCheckBox.Checked += (_, _) => ApplyPurchasingFilters();
        _purchasingShowDeletedCheckBox.Unchecked += (_, _) => ApplyPurchasingFilters();
        DockPanel.SetDock(_purchasingShowDeletedCheckBox, Dock.Right);
        fbDock.Children.Add(_purchasingShowDeletedCheckBox);

        var filterInputs = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _purchasingSearchBox = new TextBox
        {
            Width = 160, Height = 30, Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 2, 6, 2),
            ToolTip = "Filter by code, grade name or category"
        };
        _purchasingSearchBox.TextChanged += (_, _) => ApplyPurchasingFilters();
        filterInputs.Children.Add(_purchasingSearchBox);

        _purchasingCategoryFilter = new ComboBox
        {
            Width = 130, Height = 30,
            ItemsSource = new[] { "All categories", "Cuplump", "USS" },
            SelectedIndex = 0,
            ToolTip = "Filter by rubber category"
        };
        _purchasingCategoryFilter.SelectionChanged += (_, _) => ApplyPurchasingFilters();
        filterInputs.Children.Add(_purchasingCategoryFilter);
        fbDock.Children.Add(filterInputs);
        filterBar.Child = fbDock;
        DockPanel.SetDock(filterBar, Dock.Top);
        dirDock.Children.Add(filterBar);

        // DataGrid
        var columns = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 280,
            MaxHeight = 440,
            FontSize = 13.5,
            Background = Brushes.White,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            VerticalGridLinesBrush = Brushes.Transparent,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        AddPurchasingColumns(columns, "Rubber Type");

        // Detail Inspector Card setup
        var inspectorCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var inspDock = new DockPanel();

        var inspHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 11, 16, 11)
        };
        var ihDock = new DockPanel();
        var ihTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        ihTitle.Children.Add(new FluentIcon { Icon = Symbol.Tag, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        ihTitle.Children.Add(new TextBlock { Text = "Grade Specification", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        ihDock.Children.Add(ihTitle);

        var inspActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _rubberTypeEditBtn = new Button
        {
            Content = "Edit Grade",
            Height = 28,
            Padding = new Thickness(12, 0, 12, 0),
            Margin = new Thickness(0, 0, 6, 0),
            Style = (Style)FindResource("PrimaryButton"),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = false
        };
        _rubberTypeEditBtn.Click += async (_, _) =>
        {
            if (_purchasingSelectedRecord is not null)
                await OpenPurchasingEditorAsync("Rubber Type", _purchasingSelectedRecord);
        };
        inspActions.Children.Add(_rubberTypeEditBtn);

        _rubberTypeDeleteBtn = new Button
        {
            Content = "Move to Deleted",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = false
        };
        _rubberTypeDeleteBtn.Click += async (_, _) => await DeleteSelectedPurchasingRecordAsync("Rubber Type");
        inspActions.Children.Add(_rubberTypeDeleteBtn);

        _rubberTypeRestoreBtn = new Button
        {
            Content = "Restore",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        _rubberTypeRestoreBtn.Click += async (_, _) => await RestoreSelectedPurchasingRecordAsync("Rubber Type");
        inspActions.Children.Add(_rubberTypeRestoreBtn);

        DockPanel.SetDock(inspActions, Dock.Right);
        ihDock.Children.Add(inspActions);
        inspHeader.Child = ihDock;
        DockPanel.SetDock(inspHeader, Dock.Top);
        inspDock.Children.Add(inspHeader);

        _rubberTypeInspectorHost = new ContentControl();
        inspDock.Children.Add(_rubberTypeInspectorHost);
        inspectorCard.Child = inspDock;

        columns.SelectionChanged += (_, _) =>
        {
            _purchasingSelectedRecord = columns.SelectedItem as PurchasingRecord;
            UpdateRubberTypeInspector(_rubberTypeInspectorHost, _purchasingSelectedRecord, _rubberTypeEditBtn, _rubberTypeDeleteBtn, _rubberTypeRestoreBtn);
        };
        columns.MouseDoubleClick += async (_, _) =>
        {
            if (_purchasingSelectedRecord is { IsDeleted: false } selected)
                await OpenPurchasingEditorAsync("Rubber Type", selected);
        };

        _purchasingGrid = columns;
        ApplyPurchasingFilters();

        if (columns.SelectedItem is null && _purchasingFilteredRecords.Count > 0)
            columns.SelectedItem = _purchasingFilteredRecords[0];

        dirDock.Children.Add(columns);
        dirCard.Child = dirDock;

        Grid.SetColumn(dirCard, 0);
        studioGrid.Children.Add(dirCard);

        Grid.SetColumn(inspectorCard, 2);
        studioGrid.Children.Add(inspectorCard);

        panel.Children.Add(studioGrid);
        return panel;
    }

    private void UpdateRubberTypeInspector(
        ContentControl host, PurchasingRecord? record,
        Button? editBtn, Button? delBtn, Button? restoreBtn)
    {
        if (editBtn is not null) editBtn.IsEnabled = record is not null && !record.IsDeleted;
        if (delBtn is not null) delBtn.IsEnabled = record is not null && !record.IsDeleted;
        if (restoreBtn is not null)
        {
            restoreBtn.Visibility = record is { IsDeleted: true } ? Visibility.Visible : Visibility.Collapsed;
            restoreBtn.IsEnabled = record is { IsDeleted: true };
        }

        if (record is null)
        {
            var empty = new StackPanel { Margin = new Thickness(24, 40, 24, 40), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(new FluentIcon { Icon = Symbol.Tag, IconSize = IconSize.Size32, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("MutedBrush"), HorizontalAlignment = HorizontalAlignment.Center });
            empty.Children.Add(new TextBlock { Text = "No Rubber Grade Selected", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(0, 8, 0, 4), TextAlignment = TextAlignment.Center });
            empty.Children.Add(new TextBlock { Text = "Select a rubber grade from the directory list on the left to inspect parameters and specifications.", FontSize = 12, Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 280 });
            host.Content = empty;
            return;
        }

        var isCuplump = record.Category.Equals("Cuplump", StringComparison.OrdinalIgnoreCase);
        var isEudr = record.Name.Contains("EUDR", StringComparison.OrdinalIgnoreCase) || record.Code.Contains("EUDR", StringComparison.OrdinalIgnoreCase);
        var isFsc = record.Name.Contains("FSC", StringComparison.OrdinalIgnoreCase) || record.Code.Contains("FSC", StringComparison.OrdinalIgnoreCase);

        var stack = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };

        // 1. Hero Summary Box
        var heroBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var hbStack = new StackPanel();
        var hbTopRow = new DockPanel();
        var badgesStack = new StackPanel { Orientation = Orientation.Horizontal };

        // Category Badge
        var catBorder = new Border
        {
            Background = isCuplump ? new SolidColorBrush(Color.FromRgb(234, 244, 242)) : new SolidColorBrush(Color.FromRgb(234, 241, 255)),
            BorderBrush = isCuplump ? new SolidColorBrush(Color.FromRgb(196, 220, 214)) : new SolidColorBrush(Color.FromRgb(197, 213, 246)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(0, 0, 6, 0)
        };
        catBorder.Child = new TextBlock
        {
            Text = record.Category,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = isCuplump ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(36, 87, 166))
        };
        badgesStack.Children.Add(catBorder);

        // Status Badge
        var statusBorder = new Border
        {
            Background = record.IsDeleted ? new SolidColorBrush(Color.FromRgb(254, 240, 239)) : new SolidColorBrush(Color.FromRgb(234, 247, 239)),
            BorderBrush = record.IsDeleted ? new SolidColorBrush(Color.FromRgb(244, 199, 195)) : new SolidColorBrush(Color.FromRgb(185, 227, 200)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(7, 2, 7, 2)
        };
        statusBorder.Child = new TextBlock
        {
            Text = record.Status,
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = record.IsDeleted ? new SolidColorBrush(Color.FromRgb(180, 35, 24)) : new SolidColorBrush(Color.FromRgb(22, 128, 74))
        };
        badgesStack.Children.Add(statusBorder);
        DockPanel.SetDock(badgesStack, Dock.Right);
        hbTopRow.Children.Add(badgesStack);

        var gradeTitle = new TextBlock
        {
            Text = record.Name,
            FontSize = 17, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        hbTopRow.Children.Add(gradeTitle);
        hbStack.Children.Add(hbTopRow);

        var subInfo = new TextBlock
        {
            Text = $"Code: {record.Code} · System ID: {record.Id}",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        hbStack.Children.Add(subInfo);
        heroBox.Child = hbStack;
        stack.Children.Add(heroBox);

        // 2. Specifications Matrix Box
        var specBox = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var specStack = new StackPanel();
        specStack.Children.Add(new TextBlock
        {
            Text = "Material Classification & Workflow",
            FontSize = 12, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        });

        void AddSpecRow(string label, string val, bool highlight = false)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
            var valBlock = new TextBlock
            {
                Text = val,
                FontSize = 12,
                FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = (Brush)FindResource("TextBrush")
            };
            DockPanel.SetDock(valBlock, Dock.Right);
            row.Children.Add(valBlock);
            var lblBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedBrush")
            };
            row.Children.Add(lblBlock);
            specStack.Children.Add(row);
        }

        AddSpecRow("Raw Material Category", isCuplump ? "Cuplump (น้ำยางถ้วย)" : "USS (ยางแผ่นดิบ)", true);
        AddSpecRow("Booking Intake Queue", "Enabled for Supplier Reservations");
        AddSpecRow("Weighbridge Scaled At", "Truck Scale 1 & Scale 2 (Inbound/Outbound)");
        AddSpecRow("Sampling & DRC", "Mandatory Laboratory Testing");
        AddSpecRow("Active Assignment", record.IsDeleted ? "Decommissioned / Inactive" : "Fully Operational", true);

        specBox.Child = specStack;
        stack.Children.Add(specBox);

        // 3. Certification & Compliance Box
        var certBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var certStack = new StackPanel();
        var certHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        certHead.Children.Add(new FluentIcon
        {
            Icon = isEudr || isFsc ? Symbol.ShieldCheckmark : Symbol.DocumentBulletList,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = isEudr ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        certHead.Children.Add(new TextBlock
        {
            Text = isEudr ? "EUDR Certified (European Deforestation Regulation)"
                 : isFsc ? "FSC Certified Sustainable Forestry Sourcing"
                 : "Standard Industrial Natural Rubber Grade",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        certStack.Children.Add(certHead);
        certStack.Children.Add(new TextBlock
        {
            Text = isEudr ? "Requires verified plot geolocation polygon data, supplier certificate, and supply chain due diligence documentation."
                 : isFsc ? "Complies with chain of custody forest management standards and certified grower documentation."
                 : "Standard quality grading, factory receiving procedures, and moisture deduction rules apply.",
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 15
        });
        certBox.Child = certStack;
        stack.Children.Add(certBox);

        // 4. Operational Notice
        stack.Children.Add(new TextBlock
        {
            Text = "Modifications to this grade are updated in real-time across supplier queues, weighbridge indicators, and receiving inspection samples.",
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 15
        });

        host.Content = stack;
    }

    private Border CreatePurchasingMetricCard(string title, string value, Symbol icon, string accentHex, string bgHex, string borderHex)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 10, 0)
        };
        var dock = new DockPanel();
        var iconBox = new Border
        {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)!),
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)!),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBox.Child = new FluentIcon
        {
            Icon = icon, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)!),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(iconBox, Dock.Right);
        dock.Children.Add(iconBox);

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 19, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextBrush")
        });
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        dock.Children.Add(stack);
        card.Child = dock;
        return card;
    }

    private UIElement BuildPurchasingManagement((string Key, string Label, string Description, string? ApiRoute) section)
    {
        var isSummary = _purchasingAction is "SuppliersSummary" or "OrdersSummary";
        var isSupplier = section.Key == "Supplier";
        var title = (section.Key, isSummary) switch
        {
            ("Supplier", true) => "Suppliers Summary",
            ("Order", true) => "Orders Summary",
            ("Supplier", _) => "Supplier Management",
            _ => "Order Management"
        };
        var subtitle = isSupplier ? "Manage registered rubber suppliers, contact information, and supplied rubber categories."
                     : "Manage forward and spot contracts, supplier orders, rubber grades, pricing, and approval status.";

        _purchasingProvinceFilter = null;
        _purchasingRubberTypeFilter = null;
        _purchasingCategoryFilter = null;
        var panel = new StackPanel();

        // 1. Top Header Row
        var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var activeCount = _purchasingAllRecords.Count(r => !r.IsDeleted);
        var countBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var cbStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        cbStack.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        cbStack.Children.Add(new TextBlock { Text = $"{activeCount} Active Records", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(4, 120, 87)), VerticalAlignment = VerticalAlignment.Center });
        countBadge.Child = cbStack;
        headerActions.Children.Add(countBadge);

        var addButton = new Button
        {
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Style = (Style)FindResource("PrimaryButton"),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = isSummary ? Visibility.Collapsed : Visibility.Visible
        };
        var addStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        addStack.Children.Add(new FluentIcon { Icon = Symbol.Add, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        addStack.Children.Add(new TextBlock { Text = isSupplier ? "Add Supplier" : "New Order", FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        addButton.Content = addStack;
        addButton.Click += async (_, _) => await OpenPurchasingEditorAsync(section.Key, null);
        headerActions.Children.Add(addButton);

        var refreshBtn = new Button
        {
            Height = 32,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var refStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        refStack.Children.Add(new FluentIcon { Icon = Symbol.ArrowClockwise, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        refStack.Children.Add(new TextBlock { Text = "Refresh", VerticalAlignment = VerticalAlignment.Center });
        refreshBtn.Content = refStack;
        refreshBtn.Click += async (_, _) => await LoadPurchasingRecordsAsync();
        headerActions.Children.Add(refreshBtn);

        DockPanel.SetDock(headerActions, Dock.Right);
        headerDock.Children.Add(headerActions);

        var titleLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 42, Height = 42, CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBox.Child = new FluentIcon { Icon = isSupplier ? Symbol.People : Symbol.Cart, IconSize = IconSize.Size20, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        titleLeft.Children.Add(iconBox);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });
        textStack.Children.Add(new TextBlock { Text = subtitle, FontSize = 12.5, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 2, 0, 0) });
        titleLeft.Children.Add(textStack);
        headerDock.Children.Add(titleLeft);
        panel.Children.Add(headerDock);

        // 2. Metrics Bar
        if (isSupplier)
        {
            var provCount = _purchasingAllRecords.Select(r => r.Province).Where(p => !string.IsNullOrEmpty(p)).Distinct().Count();
            var supMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
            supMetrics.Children.Add(CreatePurchasingMetricCard("Registered Suppliers", activeCount.ToString(), Symbol.People, "#0F766E", "#F0FDFA", "#CCFBF1"));
            supMetrics.Children.Add(CreatePurchasingMetricCard("Active Suppliers", _purchasingAllRecords.Count(r => !r.IsDeleted && r.Status == "ACTIVE").ToString(), Symbol.Checkmark, "#059669", "#F0FDF4", "#BBF7D0"));
            supMetrics.Children.Add(CreatePurchasingMetricCard("Provinces Covered", provCount.ToString(), Symbol.Location, "#2563EB", "#EFF6FF", "#BFDBFE"));
            supMetrics.Children.Add(CreatePurchasingMetricCard("Deleted / Inactive", _purchasingAllRecords.Count(r => r.IsDeleted).ToString(), Symbol.Delete, "#DC2626", "#FEF2F2", "#FECACA"));
            panel.Children.Add(supMetrics);
        }
        else
        {
            var ordMetrics = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
            ordMetrics.Children.Add(CreatePurchasingMetricCard("Total Purchase Orders", _purchasingAllRecords.Count.ToString(), Symbol.Cart, "#0F766E", "#F0FDFA", "#CCFBF1"));
            ordMetrics.Children.Add(CreatePurchasingMetricCard("Forward Orders (FW)", _purchasingAllRecords.Count(r => r.Category == "FW").ToString(), Symbol.Calendar, "#059669", "#F0FDF4", "#BBF7D0"));
            ordMetrics.Children.Add(CreatePurchasingMetricCard("Spot Orders (SP)", _purchasingAllRecords.Count(r => r.Category == "SP").ToString(), Symbol.Document, "#2563EB", "#EFF6FF", "#BFDBFE"));
            ordMetrics.Children.Add(CreatePurchasingMetricCard("Pending Approval", _purchasingAllRecords.Count(r => r.Status == "WaitingApproval").ToString(), Symbol.Clock, "#D97706", "#FFFBEB", "#FDE68A"));
            panel.Children.Add(ordMetrics);
        }

        // 3. Container Table Card
        var tableCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true
        };
        var tableDock = new DockPanel();

        // Card Header
        var thBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 11, 16, 11)
        };
        var thDock = new DockPanel();
        var thTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        thTitle.Children.Add(new FluentIcon { Icon = isSupplier ? Symbol.People : Symbol.Cart, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        thTitle.Children.Add(new TextBlock { Text = isSupplier ? "Registered Suppliers Directory" : "Purchase Orders Directory", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        thDock.Children.Add(thTitle);

        var countPill = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        countPill.Child = new TextBlock { Text = $"{activeCount} records", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("MutedBrush") };
        DockPanel.SetDock(countPill, Dock.Right);
        thDock.Children.Add(countPill);
        thBorder.Child = thDock;
        DockPanel.SetDock(thBorder, Dock.Top);
        tableDock.Children.Add(thBorder);

        // Filter & Toolbar Bar
        var toolbar = new WrapPanel
        {
            Margin = new Thickness(14, 10, 14, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        _purchasingSearchBox = new TextBox
        {
            Width = 220, Height = 32, Margin = new Thickness(0, 0, 9, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(6, 2, 6, 2),
            ToolTip = "Search records"
        };
        _purchasingSearchBox.TextChanged += (_, _) => ApplyPurchasingFilters();
        toolbar.Children.Add(FieldCompact("Search", _purchasingSearchBox));

        if (isSupplier)
        {
            var provinces = _purchasingProvinces.Select(item => item.Label)
                .Concat(_purchasingAllRecords.Select(item => item.Province))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _purchasingProvinceFilter = new ComboBox
            {
                Width = 140, Height = 32, Margin = new Thickness(0, 0, 9, 0),
                ItemsSource = new[] { "All provinces" }.Concat(provinces).ToList(),
                SelectedIndex = 0,
                ToolTip = "Filter suppliers by province"
            };
            _purchasingProvinceFilter.SelectionChanged += (_, _) => ApplyPurchasingFilters();
            toolbar.Children.Add(FieldCompact("Province", _purchasingProvinceFilter));

            var rubberTypes = _purchasingAllRecords
                .SelectMany(item => item.RubberTypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _purchasingRubberTypeFilter = new ComboBox
            {
                Width = 140, Height = 32, Margin = new Thickness(0, 0, 9, 0),
                ItemsSource = new[] { "All rubber types" }.Concat(rubberTypes).ToList(),
                SelectedIndex = 0,
                ToolTip = "Filter suppliers by rubber type"
            };
            _purchasingRubberTypeFilter.SelectionChanged += (_, _) => ApplyPurchasingFilters();
            toolbar.Children.Add(FieldCompact("Rubber type", _purchasingRubberTypeFilter));

            _purchasingShowDeletedCheckBox = new CheckBox
            {
                Content = "Include deleted",
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 12, 0),
                Foreground = (Brush)FindResource("MutedBrush")
            };
            _purchasingShowDeletedCheckBox.Checked += (_, _) => ApplyPurchasingFilters();
            _purchasingShowDeletedCheckBox.Unchecked += (_, _) => ApplyPurchasingFilters();
            toolbar.Children.Add(_purchasingShowDeletedCheckBox);
        }
        else
        {
            _purchasingShowDeletedCheckBox = null;
        }

        Button? editButton = null;
        Button? deleteButton = null;
        Button? restoreButton = null;
        Button? approveButton = null;

        if (!isSummary)
        {
            editButton = PurchasingButton("Edit selected");
            editButton.IsEnabled = false;
            editButton.Click += async (_, _) =>
            {
                if (_purchasingSelectedRecord is not null)
                    await OpenPurchasingEditorAsync(section.Key, _purchasingSelectedRecord);
            };
            toolbar.Children.Add(editButton);

            deleteButton = PurchasingButton(section.Key == "Order" ? "Delete selected" : "Move to Deleted");
            deleteButton.IsEnabled = false;
            deleteButton.Click += async (_, _) => await DeleteSelectedPurchasingRecordAsync(section.Key);
            toolbar.Children.Add(deleteButton);

            if (isSupplier)
            {
                restoreButton = PurchasingButton("Restore selected");
                restoreButton.IsEnabled = false;
                restoreButton.Click += async (_, _) => await RestoreSelectedPurchasingRecordAsync(section.Key);
                toolbar.Children.Add(restoreButton);
            }
            if (section.Key == "Order")
            {
                approveButton = PurchasingButton("Approve selected");
                approveButton.IsEnabled = false;
                approveButton.Click += async (_, _) => await ApproveSelectedOrderAsync();
                toolbar.Children.Add(approveButton);
            }
        }

        DockPanel.SetDock(toolbar, Dock.Top);
        tableDock.Children.Add(toolbar);

        var columns = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 320,
            MaxHeight = 560,
            FontSize = 13.5,
            Background = Brushes.White,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
            VerticalGridLinesBrush = Brushes.Transparent,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        AddPurchasingColumns(columns, section.Key);
        columns.SelectionChanged += (_, _) =>
        {
            _purchasingSelectedRecord = columns.SelectedItem as PurchasingRecord;
            var selected = _purchasingSelectedRecord;
            if (editButton is not null) editButton.IsEnabled = selected is not null && !selected.IsDeleted;
            if (deleteButton is not null) deleteButton.IsEnabled = selected is not null && (section.Key == "Order" || !selected.IsDeleted);
            if (restoreButton is not null) restoreButton.IsEnabled = selected is { IsDeleted: true };
            if (approveButton is not null) approveButton.IsEnabled = selected is not null && ReadProperty(selected.Raw, "status") == "WaitingApproval";
        };
        columns.MouseDoubleClick += async (_, _) =>
        {
            if (!isSummary && _purchasingSelectedRecord is { IsDeleted: false } selected)
                await OpenPurchasingEditorAsync(section.Key, selected);
        };
        _purchasingGrid = columns;
        ApplyPurchasingFilters();

        tableDock.Children.Add(columns);
        tableCard.Child = tableDock;
        panel.Children.Add(tableCard);
        return panel;
    }

    private void MainWindow_PurchasingGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_currentPage is TruckScalePageId or TruckScaleSummaryPageId)
        {
            Dispatcher.BeginInvoke(new Action(UpdateTruckScalePageHeight), System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        if (_currentPage != DepartmentId.Purchase.ToString())
            return;

        if (_purchasingSection is "Cuplump" or "USS")
        {
            if (_bookingQueueGrid is null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentPage == DepartmentId.Purchase.ToString() &&
                    _purchasingSection is "Cuplump" or "USS" && _bookingQueueGrid is not null)
                    _bookingQueueGrid.Height = BookingQueueGridHeight();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }

        if (_purchasingSection is not ("Supplier" or "Rubber Type") || _purchasingGrid is null)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_currentPage == DepartmentId.Purchase.ToString() &&
                (_purchasingSection is "Supplier" or "Rubber Type") && _purchasingGrid is not null)
                _purchasingGrid.Height = PurchasingGridHeight();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private double PurchasingGridHeight()
    {
        if (_purchasingGrid is null || !_purchasingGrid.IsLoaded || !MainLayoutGrid.IsLoaded)
            return 280;

        const double footerHeight = 38;
        const double pageBottomAndCardPadding = 38;
        var gridTop = _purchasingGrid.TransformToAncestor(MainLayoutGrid)
            .Transform(new Point(0, 0)).Y;
        return Math.Max(280, MainLayoutGrid.ActualHeight - footerHeight - gridTop - pageBottomAndCardPadding);
    }

    private UIElement BuildPurchasingSummary(string section)
    {
        var records = _purchasingAllRecords;
        var deleted = records.Count(item => item.IsDeleted);
        var active = records.Count - deleted;
        var cards = new UniformGrid
        {
            Columns = section == "Order" ? 4 : 3,
            Margin = new Thickness(0, 10, 0, 14),
            Width = section == "Supplier" ? 510 : double.NaN,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var metrics = section switch
        {
            "Supplier" => new[]
            {
                ("Total suppliers", active.ToString()),
                ("Active", records.Count(item => !item.IsDeleted && item.Status == "ACTIVE").ToString()),
                ("Deleted", deleted.ToString())
            },
            _ => new[]
            {
                ("Total orders", records.Count.ToString()),
                ("Forward (FW)", records.Count(item => item.Category == "FW").ToString()),
                ("Spot (SP)", records.Count(item => item.Category == "SP").ToString()),
                ("Waiting approval", records.Count(item => item.Status == "WaitingApproval").ToString())
            }
        };
        foreach (var (label, value) in metrics)
        {
            var accent = section == "Supplier" && label == "Active" ? "#2E8B62"
                : section == "Supplier" && label == "Deleted" ? "#C2413B"
                : "#0F766E";
            var valueBrush = section == "Supplier" && (label is "Active" or "Deleted")
                ? (Brush)new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)!)
                : (Brush)FindResource("TextBrush");
            var metric = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var compactSupplierSummary = section == "Supplier";
            metric.Children.Add(Label(value, compactSupplierSummary ? 18 : 23, valueBrush, FontWeights.SemiBold));
            var metricLabel = Label(label, compactSupplierSummary ? 11 : 12, (Brush)FindResource("MutedBrush"));
            metricLabel.Margin = compactSupplierSummary ? new Thickness(6, 0, 0, 0) : new Thickness(8, 2, 0, 2);
            metricLabel.VerticalAlignment = VerticalAlignment.Center;
            metric.Children.Add(metricLabel);
            var card = Card(metric, accent);
            if (compactSupplierSummary)
            {
                card.Height = 32;
                if (card.Child is DockPanel body && body.Children.Count > 1 && body.Children[1] is Border content)
                    content.Padding = new Thickness(9, 0, 6, 0);
            }
            card.Margin = new Thickness(0, 0, 10, 0);
            cards.Children.Add(card);
        }
        return cards;
    }

    private Button PurchasingButton(string title, bool primary = false)
    {
        var icon = CommonToolbarActionIcon(title);
        if (icon is { } iconName) return IconOnlyActionButton(title, iconName);
        return new Button
        {
            Content = title,
            Height = 32,
            Padding = new Thickness(11, 1, 11, 1),
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
        };
    }

    private static void AddPurchasingColumns(DataGrid grid, string section)
    {
        var isCompactList = section is "Supplier" or "Rubber Type";
        var supplierTextStyle = new Style(typeof(TextBlock));
        supplierTextStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Left));
        supplierTextStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        supplierTextStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        var centeredSupplierText = new Style(typeof(TextBlock));
        centeredSupplierText.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        centeredSupplierText.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        centeredSupplierText.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        var centeredSupplierHeader = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        centeredSupplierHeader.Setters.Add(new Setter(
            System.Windows.Controls.Primitives.DataGridColumnHeader.HorizontalContentAlignmentProperty,
            HorizontalAlignment.Center));

        void Add(string title, string path, double width = 1, bool centered = false)
        {
            var column = new DataGridTextColumn
            {
                Header = title,
                Binding = new System.Windows.Data.Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                ElementStyle = isCompactList ? centered ? centeredSupplierText : supplierTextStyle : null,
                HeaderStyle = centered ? centeredSupplierHeader : null
            };
            grid.Columns.Add(column);
        }

        if (section == "Supplier")
        {
            Add("Code", nameof(PurchasingRecord.Code), .55, centered: true);
            Add("Name", nameof(PurchasingRecord.Name), 1.75);
            Add("Province", nameof(PurchasingRecord.Province), .8);
            Add("Contact", nameof(PurchasingRecord.Contact), .65);
            var badgeCellTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <ItemsControl ItemsSource="{Binding RubberTypeBadges}" SnapsToDevicePixels="True">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <WrapPanel Orientation="Horizontal" HorizontalAlignment="Left" />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <Border Margin="2,2" Padding="6,1"
                                        Background="{Binding Background}" BorderBrush="{Binding Border}"
                                        BorderThickness="1" CornerRadius="__SMALL_RADIUS__"
                                        HorizontalAlignment="Left" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding Label}" FontSize="11.5"
                                               Foreground="{Binding Foreground}" FontWeight="SemiBold" />
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </DataTemplate>
                """.Replace("__SMALL_RADIUS__", UiTokens.CornerRadiusSmallValue.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Rubber types",
                CellTemplate = badgeCellTemplate,
                Width = new DataGridLength(2.45, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Status",
                CellTemplate = CreateBadgeCellTemplate(nameof(PurchasingRecord.StatusBadge), 10.5, centered: true),
                HeaderStyle = centeredSupplierHeader,
                Width = new DataGridLength(.6, DataGridLengthUnitType.Star)
            });
        }
        else if (section == "Rubber Type")
        {
            Add("Code", nameof(PurchasingRecord.Code), .8);
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Rubber type",
                CellTemplate = CreateBadgeCellTemplate(nameof(PurchasingRecord.RubberTypeBadge)),
                Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Category",
                CellTemplate = CreateBadgeCellTemplate(nameof(PurchasingRecord.CategoryBadge)),
                Width = new DataGridLength(.8, DataGridLengthUnitType.Star)
            });
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Status",
                CellTemplate = CreateBadgeCellTemplate(nameof(PurchasingRecord.StatusBadge), 10.5),
                Width = new DataGridLength(.8, DataGridLengthUnitType.Star)
            });
        }
        else
        {
            Add("Order no.", nameof(PurchasingRecord.OrderNo), 1);
            Add("Type", nameof(PurchasingRecord.Category), .55);
            Add("Code", nameof(PurchasingRecord.SupplierCode), .6);
            Add("Name", nameof(PurchasingRecord.Supplier), 1.2);
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Rubber type",
                CellTemplate = CreateBadgeCellTemplate(nameof(PurchasingRecord.RubberTypeBadge)),
                Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
            });
            Add("Gross weight", nameof(PurchasingRecord.GrossWeight), .8);
            Add("DRC", nameof(PurchasingRecord.Drc), .55);
            Add("Price / kg", nameof(PurchasingRecord.Price), .75);
            Add("Create date", nameof(PurchasingRecord.Date), .9);
            Add("Status", nameof(PurchasingRecord.Status), .9);
        }
    }

    private void ApplyPurchasingFilters()
    {
        if (_purchasingGrid is null) return;
        var includeDeleted = _purchasingShowDeletedCheckBox?.IsChecked == true;
        var query = _purchasingSearchBox?.Text.Trim() ?? "";
        var province = _purchasingProvinceFilter?.SelectedItem as string;
        var rubberType = _purchasingRubberTypeFilter?.SelectedItem as string;
        var category = _purchasingCategoryFilter?.SelectedItem as string;

        _purchasingFilteredRecords = _purchasingAllRecords
            .Where(item => includeDeleted || !item.IsDeleted)
            .Where(item => string.IsNullOrWhiteSpace(category) || category.StartsWith("All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(province) || province == "All provinces" ||
                string.Equals(item.Province, province, StringComparison.OrdinalIgnoreCase))
            .Where(item => string.IsNullOrWhiteSpace(rubberType) || rubberType == "All rubber types" ||
                item.RubberTypes.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Contains(rubberType, StringComparer.OrdinalIgnoreCase))
            .Where(item => query.Length == 0 || string.Join(" ", item.Code, item.Name, item.Category, item.Province, item.Contact,
                item.RubberTypes, item.OrderNo, item.SupplierCode, item.Supplier, item.Status).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _purchasingGrid.ItemsSource = _purchasingFilteredRecords;

        if (_purchasingSection == "Rubber Type" && _rubberTypeInspectorHost is not null)
        {
            if (_purchasingSelectedRecord is null || !_purchasingFilteredRecords.Contains(_purchasingSelectedRecord))
                _purchasingSelectedRecord = _purchasingFilteredRecords.FirstOrDefault();
            _purchasingGrid.SelectedItem = _purchasingSelectedRecord;
            UpdateRubberTypeInspector(_rubberTypeInspectorHost, _purchasingSelectedRecord, _rubberTypeEditBtn, _rubberTypeDeleteBtn, _rubberTypeRestoreBtn);
        }
    }

    private void SelectPurchasingSection(string section)
    {
        if (!PurchasingSections.Any(item => item.Key == section)) return;
        _purchasingSection = section;
        _purchasingAction = section switch
        {
            "Supplier" => "SuppliersSummary",
            "Cuplump" => "Dashboard",
            "USS" => "Dashboard",
            _ => null
        };
        _purchasingSelectedRecord = null;
        _currentPage = DepartmentId.Purchase.ToString();
        _pageMessage = "";
        _purchasingMessage = "";
        RenderCurrentPage();
        if (section is "Cuplump" or "USS") _ = LoadBookingQueueAsync();
        else if (section is "Supplier" or "Rubber Type" or "Order") _ = LoadPurchasingRecordsAsync();
        else if (section == "Marking Preparation") _ = LoadMarkingPreparationReportsAsync();
    }

    private void SelectPurchasingAction(string section, string action)
    {
        if (!PurchasingActions.Any(item => item.Section == section && item.Key == action)) return;
        _purchasingSection = section;
        _purchasingAction = action == "Dashboard" || action.EndsWith("Summary", StringComparison.Ordinal)
            ? action : null;
        _purchasingSelectedRecord = null;
        _currentPage = DepartmentId.Purchase.ToString();
        _pageMessage = "";
        _purchasingMessage = "";
        RenderCurrentPage();
        if (section is "Cuplump" or "USS") _ = LoadBookingQueueAsync();
        else if (section is "Supplier" or "Rubber Type" or "Order") _ = LoadPurchasingRecordsAsync();

        if (action is "AddSupplier" or "AddRubberType" or "CreateOrder")
            _ = OpenPurchasingEditorAsync(section, null);
        else if (action == "CreateBooking")
            _ = OpenBookingQueueEditorAsync(section, null);
    }
}

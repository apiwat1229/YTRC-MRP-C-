using System.Text.Json;

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
        ("USS", "DailySummary", "Daily Summary", "Review the daily USS purchasing summary."),
        ("Supplier", "AddSupplier", "Add Supplier", "Add a supplier to the purchasing directory."),
        ("Supplier", "ManageSuppliers", "Manage Suppliers", "Review and maintain supplier records."),
        ("Supplier", "SuppliersSummary", "Suppliers Summary", "Review supplier purchasing information."),
        ("Rubber Type", "AddRubberType", "Add Rubber Type", "Add a rubber type to purchasing master data."),
        ("Rubber Type", "ManageRubberTypes", "Manage Rubber Types", "Review and maintain rubber type records."),
        ("Rubber Type", "TypesSummary", "Types Summary", "Review rubber type purchasing information."),
        ("Order", "CreateOrder", "Create Order", "Start a new purchase order."),
        ("Order", "ManageOrders", "Manage Orders", "Review and maintain purchase orders."),
        ("Order", "OrdersSummary", "Orders Summary", "Review purchase order activity.")
    ];

    private sealed record PurchasingRecord(
        JsonElement Raw, string Id, string Code, string Name, string Category, string Province, string Contact,
        string RubberTypes, string OrderNo, string Supplier, string GrossWeight, string Drc,
        string Price, string Status, string Date, bool IsDeleted)
    {
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
                        BorderBrush="{Binding __BADGE__.Border}" BorderThickness="1" CornerRadius="3"
                        HorizontalAlignment="__ALIGN__" VerticalAlignment="Center">
                    <TextBlock Text="{Binding __BADGE__.Label}" FontSize="__FONTSIZE__" FontWeight="SemiBold"
                               Foreground="{Binding __BADGE__.Foreground}" VerticalAlignment="Center" />
                </Border>
            </DataTemplate>
            """.Replace("__BADGE__", badgePath, StringComparison.Ordinal)
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
        if (section.Key is "Supplier" or "Rubber Type" or "Order")
        {
            if (section.Key is "Supplier" or "Rubber Type")
                page.Margin = new Thickness(28, 8, 28, 30);
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

    private UIElement BuildPurchasingManagement((string Key, string Label, string Description, string? ApiRoute) section)
    {
        var isSummary = _purchasingAction is "SuppliersSummary" or "TypesSummary" or "OrdersSummary";
        var title = (section.Key, isSummary) switch
        {
            ("Supplier", true) => "Suppliers Summary",
            ("Rubber Type", true) => "Rubber Types Summary",
            ("Order", true) => "Orders Summary",
            ("Supplier", _) => "Supplier Management",
            ("Rubber Type", _) => "Rubber Types Management",
            _ => "Order Management"
        };

        var isCompactManagement = section.Key is "Supplier" or "Rubber Type";
        var panel = new StackPanel();
        var heading = SectionHeading(title);
        if (isCompactManagement)
        {
            heading.HorizontalAlignment = HorizontalAlignment.Right;
            heading.VerticalAlignment = VerticalAlignment.Bottom;
            heading.Margin = new Thickness(12, 0, 0, 0);
        }
        else
        {
            panel.Children.Add(heading);
        }
        if (!isCompactManagement)
        {
            panel.Children.Add(Label("Manage forward and spot contracts, supplier, rubber grade, pricing, and approval status.",
                13, (Brush)FindResource("MutedBrush")));

            var bannerText = _purchasingLoading ? "Loading records from the ERP service…"
                : string.IsNullOrWhiteSpace(_purchasingMessage)
                    ? $"Connected route: {section.ApiRoute} · {_purchasingAllRecords.Count} record(s) loaded."
                    : _purchasingMessage;
            panel.Children.Add(Banner(_purchasingLoading ? "Loading" : string.IsNullOrWhiteSpace(_purchasingMessage) ? "ERP service connected" : "Purchasing workspace status",
                bannerText, _purchasingMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E"));
        }

        var summaryContent = isSummary ? BuildPurchasingSummary(section.Key) : null;
        if (isCompactManagement && isSummary && summaryContent is FrameworkElement summaryElement)
            summaryElement.Margin = new Thickness(0);

        var toolbar = new WrapPanel
        {
            Margin = isCompactManagement ? new Thickness(0) : new Thickness(0, 0, 0, 9),
            VerticalAlignment = VerticalAlignment.Center
        };
        _purchasingSearchBox = new TextBox
        {
            Width = 245, Height = 32, Margin = new Thickness(0, 0, 9, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search by code, name, supplier, order number or status"
        };
        _purchasingSearchBox.TextChanged += (_, _) => ApplyPurchasingFilters();
        var searchField = FieldCompact("Search", _purchasingSearchBox);
        if (searchField is FrameworkElement searchFieldElement)
            searchFieldElement.VerticalAlignment = isCompactManagement ? VerticalAlignment.Bottom : VerticalAlignment.Center;
        toolbar.Children.Add(searchField);

        if (section.Key is "Supplier" or "Rubber Type")
        {
            _purchasingShowDeletedCheckBox = new CheckBox
            {
                Content = "Include deleted",
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 12, 0),
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
        var addButton = PurchasingButton(section.Key switch { "Supplier" => "Add Supplier", "Rubber Type" => "Add Rubber Type", _ => "New Order" }, primary: true);
        addButton.Visibility = isSummary ? Visibility.Collapsed : Visibility.Visible;
        addButton.Click += async (_, _) => await OpenPurchasingEditorAsync(section.Key, null);
        toolbar.Children.Add(addButton);

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

            if (section.Key is "Supplier" or "Rubber Type")
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

        var refreshButton = PurchasingButton("Refresh");
        refreshButton.Click += async (_, _) => await LoadPurchasingRecordsAsync();
        toolbar.Children.Add(refreshButton);
        if (isCompactManagement)
        {
            toolbar.VerticalAlignment = VerticalAlignment.Bottom;
            foreach (var child in toolbar.Children)
            {
                if (child is Button button)
                    button.VerticalAlignment = VerticalAlignment.Bottom;
                else if (child is CheckBox checkBox)
                    checkBox.VerticalAlignment = VerticalAlignment.Bottom;
            }

            var toolbarRow = new Grid { Margin = new Thickness(0, 0, 0, 9) };
            toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (isSummary && summaryContent is not null)
            {
                toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                toolbar.Margin = new Thickness(12, 0, 12, 0);
                Grid.SetColumn(summaryContent, 0);
                Grid.SetColumn(toolbar, 1);
                Grid.SetColumn(heading, 2);
                toolbarRow.Children.Add(summaryContent);
                toolbarRow.Children.Add(toolbar);
                toolbarRow.Children.Add(heading);
            }
            else
            {
                toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(heading, 1);
                toolbarRow.Children.Add(toolbar);
                toolbarRow.Children.Add(heading);
            }
            panel.Children.Add(toolbarRow);
        }
        else
        {
            if (summaryContent is not null) panel.Children.Add(summaryContent);
            panel.Children.Add(toolbar);
        }

        var columns = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 280,
            FontSize = 14,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(230, 235, 241)),
            VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(238, 242, 246)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        var isCompactList = section.Key is "Supplier" or "Rubber Type";
        if (isCompactList)
        {
            columns.Height = PurchasingGridHeight();
            columns.MaxHeight = double.PositiveInfinity;
            columns.Loaded += (_, _) => columns.Height = PurchasingGridHeight();

            var hoverBackground = (Brush)FindResource("Fluent.Ribbon.Brushes.Button.MouseOver.Background");
            var hoverBorder = (Brush)FindResource("Fluent.Ribbon.Brushes.Button.MouseOver.Border");
            var selectedBackground = (Brush)FindResource("Fluent.Ribbon.Brushes.Button.Pressed.Background");
            var selectedBorder = (Brush)FindResource("Fluent.Ribbon.Brushes.Button.Pressed.Border");
            var cellStyle = new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)FindResource("TextBrush")));
            cellStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            var hoveredRow = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(nameof(DataGridRow.IsMouseOver))
                {
                    RelativeSource = new System.Windows.Data.RelativeSource(
                        System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
                },
                Value = true
            };
            hoveredRow.Setters.Add(new Setter(Control.BackgroundProperty, hoverBackground));
            hoveredRow.Setters.Add(new Setter(Control.BorderBrushProperty, hoverBorder));
            cellStyle.Triggers.Add(hoveredRow);
            var selectedCell = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            selectedCell.Setters.Add(new Setter(Control.BackgroundProperty, selectedBackground));
            selectedCell.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)FindResource("TextBrush")));
            selectedCell.Setters.Add(new Setter(Control.BorderBrushProperty, selectedBorder));
            cellStyle.Triggers.Add(selectedCell);
            columns.CellStyle = cellStyle;
        }
        else
        {
            columns.MaxHeight = 560;
        }
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

        var gridCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
            Padding = new Thickness(8),
            Child = columns
        };
        panel.Children.Add(gridCard);
        return panel;
    }

    private void MainWindow_PurchasingGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
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
        var cards = new UniformGrid { Columns = section == "Order" ? 4 : 3, Margin = new Thickness(0, 10, 0, 14) };
        var metrics = section switch
        {
            "Supplier" => new[]
            {
                ("Total suppliers", active.ToString()),
                ("Active", records.Count(item => !item.IsDeleted && item.Status == "ACTIVE").ToString()),
                ("Deleted", deleted.ToString())
            },
            "Rubber Type" => new[]
            {
                ("Total types", active.ToString()),
                ("Cuplump", records.Count(item => !item.IsDeleted && item.Category.Equals("Cuplump", StringComparison.OrdinalIgnoreCase)).ToString()),
                ("USS / Deleted", $"{records.Count(item => !item.IsDeleted && item.Category.Equals("USS", StringComparison.OrdinalIgnoreCase))} / {deleted}")
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
            metric.Children.Add(Label(value, 23, valueBrush, FontWeights.SemiBold));
            var metricLabel = Label(label, 12, (Brush)FindResource("MutedBrush"));
            metricLabel.Margin = new Thickness(8, 2, 0, 2);
            metricLabel.VerticalAlignment = VerticalAlignment.Center;
            metric.Children.Add(metricLabel);
            var card = Card(metric, accent);
            card.Margin = new Thickness(0, 0, 10, 0);
            cards.Children.Add(card);
        }
        return cards;
    }

    private Button PurchasingButton(string title, bool primary = false) => new()
    {
        Content = title,
        Height = 32,
        Padding = new Thickness(11, 1, 11, 1),
        Margin = new Thickness(0, 0, 7, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
    };

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
            Add("Supplier code", nameof(PurchasingRecord.Code), .55, centered: true);
            Add("Supplier name", nameof(PurchasingRecord.Name), 1.75);
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
                                        BorderThickness="1" CornerRadius="3"
                                        HorizontalAlignment="Left" VerticalAlignment="Center">
                                    <TextBlock Text="{Binding Label}" FontSize="11.5"
                                               Foreground="{Binding Foreground}" FontWeight="SemiBold" />
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </DataTemplate>
                """);
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
            Add("Supplier", nameof(PurchasingRecord.Supplier), 1.2);
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
        _purchasingFilteredRecords = _purchasingAllRecords
            .Where(item => includeDeleted || !item.IsDeleted)
            .Where(item => query.Length == 0 || string.Join(" ", item.Code, item.Name, item.Category, item.Province, item.Contact,
                item.RubberTypes, item.OrderNo, item.Supplier, item.Status).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _purchasingGrid.ItemsSource = _purchasingFilteredRecords;
    }

    private void SelectPurchasingSection(string section)
    {
        if (!PurchasingSections.Any(item => item.Key == section)) return;
        _purchasingSection = section;
        _purchasingAction = section switch
        {
            "Supplier" => "SuppliersSummary",
            "Rubber Type" => "TypesSummary",
            "Cuplump" => "Dashboard",
            "USS" => "DailySummary",
            _ => null
        };
        _purchasingSelectedRecord = null;
        _currentPage = DepartmentId.Purchase.ToString();
        _pageMessage = "";
        _purchasingMessage = "";
        RenderCurrentPage();
        if (section is "Cuplump" or "USS") _ = LoadBookingQueueAsync();
        else if (section is "Supplier" or "Rubber Type" or "Order") _ = LoadPurchasingRecordsAsync();
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

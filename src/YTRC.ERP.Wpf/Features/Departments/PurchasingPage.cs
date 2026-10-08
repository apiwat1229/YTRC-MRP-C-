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
        ("Cuplump", "DailySummary", "Daily Summary", "Review the daily Cuplump purchasing summary."),
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
        JsonElement Raw, string Id, string Code, string Name, string Category, string Contact,
        string RubberTypes, string OrderNo, string Supplier, string GrossWeight, string Drc,
        string Price, string Status, string Date, bool IsDeleted);

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
        page.Children.Add(TitleBlock(info.Name, info.Description));

        if (section.Key is "Supplier" or "Rubber Type" or "Order")
        {
            page.Children.Add(BuildPurchasingManagement(section));
            return page;
        }

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

        var panel = new StackPanel();
        panel.Children.Add(SectionHeading(title));
        panel.Children.Add(Label(section.Key switch
        {
            "Supplier" => "Search, maintain supplier details, and review supported rubber types and status.",
            "Rubber Type" => "Maintain rubber grade codes and their Cuplump or USS category.",
            _ => "Manage forward and spot contracts, supplier, rubber grade, pricing, and approval status."
        }, 13, (Brush)FindResource("MutedBrush")));

        var bannerText = _purchasingLoading ? "Loading records from the ERP service…"
            : string.IsNullOrWhiteSpace(_purchasingMessage)
                ? $"Connected route: {section.ApiRoute} · {_purchasingAllRecords.Count} record(s) loaded."
                : _purchasingMessage;
        panel.Children.Add(Banner(_purchasingLoading ? "Loading" : string.IsNullOrWhiteSpace(_purchasingMessage) ? "ERP service connected" : "Purchasing workspace status",
            bannerText, _purchasingMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E"));

        if (isSummary)
            panel.Children.Add(BuildPurchasingSummary(section.Key));

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 9), VerticalAlignment = VerticalAlignment.Center };
        _purchasingSearchBox = new TextBox
        {
            Width = 245, Height = 32, Margin = new Thickness(0, 0, 9, 7),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search by code, name, supplier, order number or status"
        };
        _purchasingSearchBox.TextChanged += (_, _) => ApplyPurchasingFilters();
        toolbar.Children.Add(FieldCompact("Search", _purchasingSearchBox));

        if (section.Key is "Supplier" or "Rubber Type")
        {
            _purchasingShowDeletedCheckBox = new CheckBox
            {
                Content = "Include deleted",
                IsChecked = false,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(5, 0, 12, 12),
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
        panel.Children.Add(toolbar);

        var columns = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 280,
            MaxHeight = 560,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
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
            var metric = new StackPanel();
            metric.Children.Add(Label(value, 23, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
            metric.Children.Add(Label(label, 12, (Brush)FindResource("MutedBrush")));
            var card = Card(metric, "#0F766E");
            card.Margin = new Thickness(0, 0, 10, 0);
            cards.Children.Add(card);
        }
        return cards;
    }

    private Button PurchasingButton(string title, bool primary = false) => new()
    {
        Content = title,
        Margin = new Thickness(0, 0, 7, 7),
        Padding = new Thickness(11, 5, 11, 5),
        Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
    };

    private static void AddPurchasingColumns(DataGrid grid, string section)
    {
        void Add(string title, string path, double width = 1)
            => grid.Columns.Add(new DataGridTextColumn
            {
                Header = title,
                Binding = new System.Windows.Data.Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            });

        if (section == "Supplier")
        {
            Add("Supplier code", nameof(PurchasingRecord.Code), .75);
            Add("Supplier name", nameof(PurchasingRecord.Name), 1.2);
            Add("Contact", nameof(PurchasingRecord.Contact), 1.1);
            Add("Rubber types", nameof(PurchasingRecord.RubberTypes), 1.2);
            Add("Status", nameof(PurchasingRecord.Status), .75);
        }
        else if (section == "Rubber Type")
        {
            Add("Code", nameof(PurchasingRecord.Code), .8);
            Add("Rubber type", nameof(PurchasingRecord.Name), 1.4);
            Add("Category", nameof(PurchasingRecord.Category), .8);
            Add("Status", nameof(PurchasingRecord.Status), .8);
        }
        else
        {
            Add("Order no.", nameof(PurchasingRecord.OrderNo), 1);
            Add("Type", nameof(PurchasingRecord.Category), .55);
            Add("Supplier", nameof(PurchasingRecord.Supplier), 1.2);
            Add("Rubber type", nameof(PurchasingRecord.Name), 1.1);
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
            .Where(item => query.Length == 0 || string.Join(" ", item.Code, item.Name, item.Category, item.Contact,
                item.RubberTypes, item.OrderNo, item.Supplier, item.Status).Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _purchasingGrid.ItemsSource = _purchasingFilteredRecords;
    }

    private void SelectPurchasingSection(string section)
    {
        if (!PurchasingSections.Any(item => item.Key == section)) return;
        _purchasingSection = section;
        _purchasingAction = null;
        _purchasingSelectedRecord = null;
        _currentPage = DepartmentId.Purchase.ToString();
        _pageMessage = "";
        _purchasingMessage = "";
        RenderCurrentPage();
        if (section is "Supplier" or "Rubber Type" or "Order") _ = LoadPurchasingRecordsAsync();
    }

    private void SelectPurchasingAction(string section, string action)
    {
        if (!PurchasingActions.Any(item => item.Section == section && item.Key == action)) return;
        _purchasingSection = section;
        _purchasingAction = action.EndsWith("Summary", StringComparison.Ordinal) ? action : null;
        _purchasingSelectedRecord = null;
        _currentPage = DepartmentId.Purchase.ToString();
        _pageMessage = "";
        _purchasingMessage = "";
        RenderCurrentPage();
        if (section is "Supplier" or "Rubber Type" or "Order") _ = LoadPurchasingRecordsAsync();

        if (action is "AddSupplier" or "AddRubberType" or "CreateOrder")
            _ = OpenPurchasingEditorAsync(section, null);
    }
}

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const decimal UssDefaultEstimatedWeightKg = 120_000m;
    private static readonly (string Slot, int? CuplumpLimit, int? UssLimit)[] BookingQueueSlots =
    [
        ("08:00-09:00", 4, 4),
        ("09:00-10:00", 4, 4),
        ("10:00-11:00", 4, 4),
        ("11:00-12:00", 4, 4),
        ("13:00-14:00", null, null)
    ];

    private sealed record BookingQueueRecord(
        JsonElement Raw, string Id, int QueueNo, string BookingCode, DateTime Date,
        string StartTime, string EndTime, string Slot, string SupplierId,
        string SupplierCode, string SupplierName, string TruckType, string TruckRegister,
        string RubberType, string RubberTypeName, string Category, decimal? EstimatedWeightKg,
        string Recorder, string Status, string CheckInAt, string Note)
    {
        public string WeightDisplay => Category == "USS"
            ? $"{((EstimatedWeightKg ?? UssDefaultEstimatedWeightKg) / 1000m).ToString("N1", CultureInfo.CurrentCulture)} Ton"
            : EstimatedWeightKg is decimal kg ? $"{kg.ToString("N0", CultureInfo.CurrentCulture)} kg" : "—";

        public string TruckRegisterDisplay => FormatTruckRegister(TruckRegister);

        public PurchasingBadge RubberTypeBadge => CreateRubberTypeBadge(
            FirstNonEmpty(RubberTypeName, RubberType.Replace('_', ' '), "—"));

        public string StatusLabel => Status switch
        {
            "PENDING" => "Waiting",
            "CHECKED_IN" => "Checked In",
            "DRAINING" => "Draining",
            "COMPLETED" => "Completed",
            "CANCELLED" => "Cancelled",
            _ => Status
        };

        public Brush StatusForeground => CreateBrush(Status switch
        {
            "PENDING" => "#9A5B00",
            "CHECKED_IN" => "#16804A",
            "DRAINING" => "#7042A5",
            "COMPLETED" => "#64748B",
            "CANCELLED" => "#B42318",
            _ => "#475569"
        });

        public Brush StatusBackground => CreateBrush(Status switch
        {
            "PENDING" => "#FFF5E6",
            "CHECKED_IN" => "#EAF7EF",
            "DRAINING" => "#F3ECFB",
            "COMPLETED" => "#F1F5F9",
            "CANCELLED" => "#FEF0EF",
            _ => "#F1F5F9"
        });

        public Brush StatusBorder => CreateBrush(Status switch
        {
            "PENDING" => "#F0D4A6",
            "CHECKED_IN" => "#B9E3C8",
            "DRAINING" => "#D9C9EE",
            "COMPLETED" => "#CBD5E1",
            "CANCELLED" => "#F4C7C3",
            _ => "#CBD5E1"
        });
    }

    private async Task LoadBookingQueueAsync()
    {
        var category = _purchasingSection;
        var date = _bookingQueueDate.Date;
        var version = ++_bookingQueueLoadVersion;
        if (category is not ("Cuplump" or "USS")) return;

        if (_session.IsPreview)
        {
            _bookingQueueRecords = [];
            _bookingQueueLoading = false;
            _bookingQueueMessage = "Sign in to load live booking queue records.";
            RenderCurrentPage();
            return;
        }

        _bookingQueueLoading = true;
        _bookingQueueMessage = "";
        RenderCurrentPage();
        var apiDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var bookingsTask = _apiClient.GetAsync($"/api/bookings?date={apiDate}", _session.Token);
        var supplierTask = _bookingQueueDependenciesLoaded
            ? null : _apiClient.GetAsync("/api/suppliers", _session.Token);
        var rubberTypesTask = _bookingQueueDependenciesLoaded
            ? null : _apiClient.GetAsync("/api/rubber-types?includeDeleted=true", _session.Token);

        var bookingsResult = await bookingsTask;
        if (supplierTask is not null && rubberTypesTask is not null)
        {
            await Task.WhenAll(supplierTask, rubberTypesTask);
            var suppliersResult = await supplierTask;
            var rubberTypesResult = await rubberTypesTask;
            if (suppliersResult.Success && suppliersResult.Data is JsonElement suppliersPayload)
                _purchasingSuppliers = ExtractArray(suppliersPayload)
                    .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt", "deleted_at")))
                    .ToList();
            if (rubberTypesResult.Success && rubberTypesResult.Data is JsonElement typesPayload)
                _purchasingRubberTypes = ExtractArray(typesPayload);
            _bookingQueueDependenciesLoaded = suppliersResult.Success && rubberTypesResult.Success;
        }

        if (version != _bookingQueueLoadVersion || category != _purchasingSection) return;
        _bookingQueueLoading = false;
        if (!bookingsResult.Success)
        {
            _bookingQueueRecords = [];
            _bookingQueueMessage = $"Failed to load booking queue: {bookingsResult.Error}";
        }
        else
        {
            _bookingQueueRecords = ExtractArray(bookingsResult.Data)
                .Select(MapBookingQueueRecord)
                .Where(item => item.Date.Date == date)
                .ToList();
            _bookingQueueMessage = $"Loaded {_bookingQueueRecords.Count} booking record(s) for {date:dd MMM yyyy}.";
        }
        _bookingQueueSelectedRecord = null;
        RenderCurrentPage();
    }

    private async Task EnsureBookingQueueDependenciesAsync()
    {
        if (_bookingQueueDependenciesLoaded) return;
        var suppliersTask = _apiClient.GetAsync("/api/suppliers", _session.Token);
        var typesTask = _apiClient.GetAsync("/api/rubber-types?includeDeleted=true", _session.Token);
        await Task.WhenAll(suppliersTask, typesTask);
        var suppliers = await suppliersTask;
        var types = await typesTask;
        if (suppliers.Success && suppliers.Data is JsonElement supplierPayload)
            _purchasingSuppliers = ExtractArray(supplierPayload)
                .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt", "deleted_at")))
                .ToList();
        if (types.Success && types.Data is JsonElement typePayload)
            _purchasingRubberTypes = ExtractArray(typePayload);
        _bookingQueueDependenciesLoaded = suppliers.Success && types.Success;
    }

    private UIElement BuildBookingQueuePage(string category)
    {
        var isSummary = _purchasingAction == "DailySummary";
        _bookingQueueSelectedRecord = null;
        _bookingQueueEditButton = null;
        var queue = _bookingQueueRecords.Where(item => item.Category == category)
            .OrderBy(item => item.Slot, StringComparer.Ordinal).ThenBy(item => item.QueueNo).ToList();
        var page = new StackPanel();

        var toolbar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var datePicker = new DatePicker
        {
            SelectedDate = _bookingQueueDate,
            Width = 150,
            Height = 32,
            FontSize = 14,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Select booking date",
            CalendarStyle = CreateBookingQueueCalendarStyle()
        };
        datePicker.SelectedDateChanged += (_, _) =>
        {
            if (datePicker.SelectedDate is not DateTime selectedDate) return;
            _bookingQueueDate = selectedDate.Date;
            _bookingQueueSlot = NormalizeSelectedSlot(_bookingQueueSlot, _bookingQueueDate);
            _ = LoadBookingQueueAsync();
        };
        toolbar.Children.Add(FieldCompact("Date", datePicker));

        _bookingQueueSearchBox = new TextBox
        {
            Text = _bookingQueueSearchText,
            Width = 205,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search queue, supplier, truck or rubber type"
        };
        _bookingQueueSearchBox.TextChanged += (_, _) =>
        {
            _bookingQueueSearchText = _bookingQueueSearchBox.Text.Trim();
            ApplyBookingQueueFilters(category, isSummary);
        };
        toolbar.Children.Add(FieldCompact("Search", _bookingQueueSearchBox));

        var createButton = PurchasingButton("New Booking", primary: true);
        createButton.Click += (_, _) => _ = OpenBookingQueueEditorAsync(category, null);
        toolbar.Children.Add(createButton);
        if (isSummary)
        {
            var manageButton = PurchasingButton("Manage Queue");
            manageButton.Click += (_, _) =>
            {
                _purchasingAction = null;
                RenderCurrentPage();
            };
            toolbar.Children.Add(manageButton);
        }
        else
        {
            _bookingQueueEditButton = PurchasingButton("Edit selected");
            _bookingQueueEditButton.IsEnabled = false;
            _bookingQueueEditButton.Click += (_, _) =>
            {
                if (_bookingQueueSelectedRecord is not null)
                    _ = OpenBookingQueueEditorAsync(category, _bookingQueueSelectedRecord);
            };
            toolbar.Children.Add(_bookingQueueEditButton);

            var summaryButton = PurchasingButton("Daily Summary");
            summaryButton.Click += (_, _) =>
            {
                _purchasingAction = "DailySummary";
                RenderCurrentPage();
            };
            toolbar.Children.Add(summaryButton);
        }
        var refreshButton = PurchasingButton("Refresh");
        refreshButton.Click += async (_, _) => await LoadBookingQueueAsync();
        toolbar.Children.Add(refreshButton);

        var toolbarRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(toolbar, 0);
        toolbarRow.Children.Add(toolbar);
        var heading = SectionHeading(isSummary ? $"{category} Daily Summary" : $"{category} Booking Management");
        heading.Margin = new Thickness(12, 0, 0, 0);
        heading.VerticalAlignment = VerticalAlignment.Center;
        heading.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(heading, 1);
        toolbarRow.Children.Add(heading);
        page.Children.Add(toolbarRow);

        if (_bookingQueueLoading)
            page.Children.Add(Banner("Loading booking queue", "Retrieving bookings for the selected date…", "#0F766E"));
        else if (_bookingQueueMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            page.Children.Add(Banner("Booking queue unavailable", _bookingQueueMessage, "#B42318"));

        if (isSummary)
        {
            page.Children.Add(BuildBookingQueueSlotPanel(category, true));
        }
        else
        {
            page.Children.Add(BuildBookingQueueSlotPanel(category, false));
        }

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 280,
            Height = 320,
            RowHeight = 30,
            FontSize = 14,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(230, 235, 241)),
            VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(238, 242, 246)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        AddBookingQueueColumns(grid, category);
        grid.AddHandler(Button.ClickEvent, new RoutedEventHandler(BookingQueueTicketButton_Click));
        grid.SelectionChanged += (_, _) =>
        {
            _bookingQueueSelectedRecord = grid.SelectedItem as BookingQueueRecord;
            UpdateBookingQueueActionButtons();
        };
        grid.Loaded += (_, _) => grid.Height = BookingQueueGridHeight();
        _bookingQueueGrid = grid;
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
            Padding = new Thickness(8),
            Child = grid
        };
        page.Children.Add(card);
        ApplyBookingQueueFilters(category, isSummary);
        return page;
    }

    private UIElement BuildBookingQueueSlotPanel(string category, bool summary)
    {
        var panel = new UniformGrid
        {
            Columns = summary ? BookingQueueSlots.Length : BookingQueueSlots.Length + 1,
            Margin = new Thickness(0, 0, 0, 12)
        };
        if (!summary)
            panel.Children.Add(BuildSlotButton("ALL", "All slots", category, false, summary));
        foreach (var config in BookingQueueSlots)
            panel.Children.Add(BuildSlotButton(config.Slot, config.Slot, category,
                IsBookingSlotClosed(config.Slot, _bookingQueueDate), summary));
        return panel;
    }

    private Button BuildSlotButton(string slot, string label, string category, bool closed, bool summary)
    {
        var count = slot == "ALL" ? _bookingQueueRecords.Count(item => item.Category == category)
            : _bookingQueueRecords.Count(item => item.Category == category && item.Slot == slot);
        var limit = slot == "ALL" ? null : GetBookingSlotLimit(slot, _bookingQueueDate, category);
        var isSelected = !summary && _bookingQueueSlot == slot;
        var isFull = limit is int capacity && count >= capacity;
        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(Label(label, 12, closed ? Brushes.Gray : (Brush)FindResource("TextBrush"), FontWeights.SemiBold, true));
        var capacityText = closed ? "Closed" : limit is int max ? $"{count} / {max}" : $"{count} / Unlimited";
        body.Children.Add(Label(capacityText, 11, closed ? Brushes.Gray : isFull ? Brushes.IndianRed : (Brush)FindResource("MutedBrush"), center: true));
        var button = new Button
        {
            Content = body,
            Height = 58,
            Margin = new Thickness(0, 0, 8, 0),
            IsEnabled = !closed,
            Background = isSelected ? (Brush)FindResource("ControlPressedBackground") : Brushes.White,
            BorderBrush = isSelected ? (Brush)FindResource("ControlPressedBorder") : (Brush)FindResource("BorderBrushSoft"),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = isFull ? "This slot is at capacity" : label
        };
        button.Click += (_, _) =>
        {
            _bookingQueueSlot = slot;
            if (summary) _purchasingAction = null;
            RenderCurrentPage();
        };
        return button;
    }

    private static void AddBookingQueueColumns(DataGrid grid, string category)
    {
        Style CenteredHeaderStyle()
        {
            var headerType = typeof(System.Windows.Controls.Primitives.DataGridColumnHeader);
            var style = new Style(headerType, grid.TryFindResource(headerType) as Style);
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            return style;
        }

        DataGridTextColumn Text(string header, string path, double width, bool center = false)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
            if (center)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                column.ElementStyle = style;
                column.HeaderStyle = CenteredHeaderStyle();
            }
            return column;
        }

        grid.Columns.Add(Text("Queue", nameof(BookingQueueRecord.QueueNo), .45, true));
        grid.Columns.Add(Text("Booking code", nameof(BookingQueueRecord.BookingCode), .9));
        grid.Columns.Add(Text("Time slot", nameof(BookingQueueRecord.Slot), .8));
        grid.Columns.Add(Text("Supplier code", nameof(BookingQueueRecord.SupplierCode), .7, true));
        grid.Columns.Add(Text("Supplier name", nameof(BookingQueueRecord.SupplierName), 1.4));
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Rubber type",
            CellTemplate = CreateBadgeCellTemplate(nameof(BookingQueueRecord.RubberTypeBadge), centered: true),
            HeaderStyle = CenteredHeaderStyle(),
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(Text("Truck", nameof(BookingQueueRecord.TruckRegisterDisplay), .9));
        if (category == "USS")
            grid.Columns.Add(Text("Estimate", nameof(BookingQueueRecord.WeightDisplay), .75, true));

        var statusTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Border Padding="7,1" Background="{Binding StatusBackground}"
                        BorderBrush="{Binding StatusBorder}" BorderThickness="1" CornerRadius="3"
                        HorizontalAlignment="Center" VerticalAlignment="Center">
                    <TextBlock Text="{Binding StatusLabel}" FontSize="11" FontWeight="SemiBold"
                               Foreground="{Binding StatusForeground}" />
                </Border>
            </DataTemplate>
            """);
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Status",
            CellTemplate = statusTemplate,
            HeaderStyle = CenteredHeaderStyle(),
            Width = new DataGridLength(.8, DataGridLengthUnitType.Star)
        });
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Ticket",
            CellTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <Button Content="Ticket" Tag="OpenTicket" Padding="8,3"
                            HorizontalAlignment="Center" VerticalAlignment="Center"
                            ToolTip="Open booking ticket" />
                </DataTemplate>
                """),
            HeaderStyle = CenteredHeaderStyle(),
            Width = new DataGridLength(.75, DataGridLengthUnitType.Star)
        });
    }

    private void BookingQueueTicketButton_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button { Tag: "OpenTicket", DataContext: BookingQueueRecord record })
            OpenBookingTicket(record);
    }

    private void ApplyBookingQueueFilters(string category, bool summary)
    {
        if (_bookingQueueGrid is null) return;
        var query = _bookingQueueSearchText.Trim();
        var records = _bookingQueueRecords.Where(item => item.Category == category);
        if (!summary && _bookingQueueSlot != "ALL")
            records = records.Where(item => item.Slot == _bookingQueueSlot);
        if (query.Length > 0)
        {
            records = records.Where(item => string.Join(" ", item.QueueNo, item.BookingCode,
                item.SupplierCode, item.SupplierName, item.TruckRegister, item.TruckType,
                item.RubberType, item.RubberTypeName, item.Slot, item.Status, item.StatusLabel)
                .Contains(query, StringComparison.OrdinalIgnoreCase));
        }
        _bookingQueueGrid.ItemsSource = records.OrderBy(item => item.Slot, StringComparer.Ordinal)
            .ThenBy(item => item.QueueNo).ToList();
        UpdateBookingQueueActionButtons();
    }

    private void UpdateBookingQueueActionButtons()
    {
        var selected = _bookingQueueSelectedRecord;
        if (_bookingQueueEditButton is not null)
            _bookingQueueEditButton.IsEnabled = selected is not null && selected.Status is not ("COMPLETED" or "CANCELLED");
    }

    private async Task OpenBookingQueueEditorAsync(string category, BookingQueueRecord? record)
    {
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Sign in to create or edit bookings.", "Sign in required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await EnsureBookingQueueDependenciesAsync();
        var date = record?.Date.Date ?? _bookingQueueDate.Date;
        var rawBookings = await LoadBookingQueueDayRawAsync(date);
        if (rawBookings is null) return;
        var supplierChoices = _purchasingSuppliers.Select(item => new PurchasingChoice(
            ReadProperty(item, "id"),
            $"{ReadProperty(item, "code", "supplierCode", "supplier_code")} : {ReadProperty(item, "name", "supplierName", "supplier_name")}".Trim(' ', ':')))
            .Where(item => item.Value.Length > 0 && item.Label.Length > 0).ToList();
        var typeChoices = _purchasingRubberTypes
            .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt", "deleted_at")) &&
                           GetCategoryForRubberType(item) == category)
            .Select(item => new PurchasingChoice(
                FirstNonEmpty(ReadProperty(item, "code"), ReadProperty(item, "name")),
                ReadProperty(item, "name")))
            .Where(item => item.Value.Length > 0 && item.Label.Length > 0).ToList();
        var initialSlot = record?.Slot ?? (_bookingQueueSlot == "ALL" ? "08:00-09:00" : _bookingQueueSlot);
        var slotChoices = BookingQueueSlots.Select(item =>
        {
            var current = item.Slot == initialSlot;
            var used = rawBookings.Count(raw =>
            {
                var mapped = MapBookingQueueRecord(raw);
                return mapped.Id != record?.Id && mapped.Category == category && mapped.Slot == item.Slot;
            });
            var limit = GetBookingSlotLimit(item.Slot, date, category);
            var text = limit is int capacity ? $"{used}/{capacity}" : $"{used}/Unlimited";
            var unavailable = IsBookingSlotClosed(item.Slot, date) || limit is int fullCapacity && used >= fullCapacity;
            if (IsBookingSlotClosed(item.Slot, date)) text = "Closed Saturday";
            else if (limit is int displayedCapacity && used >= displayedCapacity) text = "Full";
            if (unavailable && !current) return null;
            return new PurchasingChoice(item.Slot, $"{item.Slot}  ·  {text}");
        }).Where(item => item is not null).Select(item => item!).ToList();

        var fields = new List<PurchasingField>();
        if (record is null)
            fields.Add(new("date", "Booking date", PurchasingFieldKind.Date,
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Required: true));
        fields.Add(new("slot", "Time slot", PurchasingFieldKind.Choice, initialSlot, Required: true, Choices: slotChoices));
        fields.Add(new("supplierId", "Supplier", PurchasingFieldKind.Choice,
            record?.SupplierId ?? "", Choices: supplierChoices));
        fields.Add(new("truckRegister", "Truck plate / register", InitialValue: record?.TruckRegister ?? ""));
        fields.Add(new("truckType", "Truck type", PurchasingFieldKind.Choice, record?.TruckType ?? "", Choices:
        [
            new("6-wheel", "รถ 6 ล้อ"), new("10-wheel", "รถ 10 ล้อ"),
            new("18-wheel", "รถ 10 ล้อ (พ่วง)"), new("trailer", "รถเทรลเลอร์"), new("pickup", "รถกระบะ")
        ]));
        fields.Add(new("rubberType", "Rubber type", PurchasingFieldKind.Choice,
            record?.RubberType ?? "", Choices: typeChoices));
        if (category == "USS")
            fields.Add(new("estimatedWeight", "Estimated weight (kg)", PurchasingFieldKind.Number,
                record?.EstimatedWeightKg?.ToString(CultureInfo.InvariantCulture) ?? "",
                Required: true));
        fields.Add(new("note", "Note", PurchasingFieldKind.Multiline, record?.Note ?? ""));

        var payload = ShowPurchasingEditor(record is null ? $"New {category} Booking" : $"Edit {category} Booking #{record.QueueNo}", fields);
        if (payload is null) return;
        var slot = payload.GetValueOrDefault("slot")?.ToString() ?? "";
        if (!TryParseBookingSlot(slot, out var startTime, out var endTime))
        {
            MessageBox.Show(this, "Choose a valid time slot.", "Invalid time slot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dateValue = date;
        if (record is null && payload.GetValueOrDefault("date") is string rawDate &&
            DateTimeOffset.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate))
            dateValue = parsedDate.LocalDateTime.Date;
        if (IsBookingSlotClosed(slot, dateValue))
        {
            MessageBox.Show(this, "Bookings after 11:00 are closed on Saturdays. Choose another slot.",
                "Slot unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rubberType = payload.GetValueOrDefault("rubberType")?.ToString() ?? "";
        if (rubberType.Length > 0 && GetBookingQueueCategory(rubberType, _purchasingRubberTypes) != category)
        {
            MessageBox.Show(this, $"Select a {category} rubber type for this queue.", "Category mismatch",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        decimal? estimatedWeight = payload.GetValueOrDefault("estimatedWeight") is decimal weight ? weight : null;
        if (category == "USS" && (estimatedWeight is null or <= 0))
        {
            MessageBox.Show(this, "Enter an estimated weight greater than zero for USS.", "Weight required",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var currentDayRaw = await LoadBookingQueueDayRawAsync(dateValue);
        if (currentDayRaw is null) return;
        var limitForSlot = GetBookingSlotLimit(slot, dateValue, category);
        var usedCount = currentDayRaw.Count(raw =>
        {
            var mapped = MapBookingQueueRecord(raw);
            return mapped.Id != record?.Id && mapped.Category == category && mapped.Slot == slot;
        });
        if (limitForSlot is int maxBookings && usedCount >= maxBookings)
        {
            MessageBox.Show(this, $"Slot {slot} is full for {category} ({usedCount}/{maxBookings}). Choose another slot.",
                "Slot full", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var supplierId = payload.GetValueOrDefault("supplierId")?.ToString() ?? "";
        var supplier = _purchasingSuppliers.FirstOrDefault(item => ReadProperty(item, "id") == supplierId);
        var supplierCode = ReadProperty(supplier, "code", "supplierCode", "supplier_code");
        var supplierName = ReadProperty(supplier, "name", "supplierName", "supplier_name");
        var body = new Dictionary<string, object?>
        {
            ["supplierId"] = supplierId.Length == 0 ? null : supplierId,
            ["supplierCode"] = supplierCode.Length == 0 ? null : supplierCode,
            ["supplierName"] = supplierName.Length == 0 ? null : supplierName,
            ["truckType"] = payload.GetValueOrDefault("truckType"),
            ["truckRegister"] = payload.GetValueOrDefault("truckRegister"),
            ["rubberType"] = rubberType.Length == 0 ? null : rubberType,
            ["estimatedWeight"] = category == "USS" ? estimatedWeight : null,
            ["recorder"] = record?.Recorder ?? _session.User.FullName,
            ["note"] = payload.GetValueOrDefault("note")
        };
        ApiResponse<JsonElement> result;
        if (record is null)
        {
            body["date"] = dateValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            body["startTime"] = startTime;
            body["endTime"] = endTime;
            result = await _apiClient.PostJsonAsync("/api/bookings", body, _session.Token);
        }
        else
        {
            body["startTime"] = startTime;
            body["endTime"] = endTime;
            result = await _apiClient.PatchJsonAsync($"/api/bookings/{Uri.EscapeDataString(record.Id)}", body, _session.Token);
        }

        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Booking save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _bookingQueueSlot = slot;
        _bookingQueueDate = dateValue;
        _bookingQueueMessage = record is null ? "Booking created successfully." : "Booking updated successfully.";
        await LoadBookingQueueAsync();
    }

    private async Task<List<JsonElement>?> LoadBookingQueueDayRawAsync(DateTime date)
    {
        var apiDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var result = await _apiClient.GetAsync($"/api/bookings?date={apiDate}", _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Could not check slot availability", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
        return ExtractArray(result.Data);
    }

    private static BookingQueueRecord MapRawBookingQueueRecord(JsonElement raw)
    {
        var id = ReadProperty(raw, "id");
        var code = ReadProperty(raw, "bookingCode", "booking_code");
        var rubberType = ReadProperty(raw, "rubberType", "rubber_type");
        var start = ReadProperty(raw, "startTime", "start_time");
        var end = ReadProperty(raw, "endTime", "end_time");
        var slot = ReadProperty(raw, "slot");
        if (slot.Length == 0 && start.Length > 0 && end.Length > 0) slot = $"{start}-{end}";
        var dateValue = ReadProperty(raw, "date", "bookingDate", "booking_date");
        var date = DateTimeOffset.TryParse(dateValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDate)
            ? parsedDate.LocalDateTime.Date : DateTime.MinValue;
        decimal? estimatedWeight = decimal.TryParse(ReadProperty(raw, "estimatedWeight", "estimated_weight"),
            NumberStyles.Number, CultureInfo.InvariantCulture, out var weight) ? weight : null;
        _ = int.TryParse(ReadProperty(raw, "queueNo", "queue_no"), out var queueNo);
        var checkInAt = ReadProperty(raw, "checkinAt", "checkin_at", "checkInAt", "checked_in_at", "checkedInAt", "check_in_at");
        var status = ReadProperty(raw, "status").ToUpperInvariant();
        if (status.Length == 0) status = "PENDING";
        if (status != "CANCELLED" && status != "COMPLETED" &&
            decimal.TryParse(ReadProperty(raw, "weightOut", "weight_out", "trailerWeightOut", "trailer_weight_out"),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var weightOut) && weightOut > 0)
            status = "COMPLETED";
        else if (status == "PENDING" && checkInAt.Length > 0)
            status = "CHECKED_IN";

        var supplierId = ReadProperty(raw, "supplierId", "supplier_id");
        var supplierCode = ReadProperty(raw, "supplierCode", "supplier_code");
        var supplierName = ReadProperty(raw, "supplierName", "supplier_name");
        if (supplierCode.Length == 0) supplierCode = ReadNestedProperty(raw, "supplier", "code", "supplierCode");
        if (supplierName.Length == 0) supplierName = ReadNestedProperty(raw, "supplier", "name", "supplierName");
        var typeName = ReadProperty(raw, "rubberTypeName", "rubber_type_name");
        var recorder = ReadProperty(raw, "recorder", "createdByName", "created_by_name");
        var note = ReadProperty(raw, "note");
        var category = GetBookingQueueCategory(rubberType, [], code);
        return new BookingQueueRecord(raw.Clone(), id, queueNo, code, date, start, end, slot,
            supplierId, supplierCode, supplierName, ReadProperty(raw, "truckType", "truck_type"),
            ReadProperty(raw, "truckRegister", "truck_register"), rubberType, typeName, category,
            estimatedWeight, recorder, status, checkInAt, note);
    }

    private static string GetCategoryForRubberType(JsonElement rubberType)
    {
        var category = ReadProperty(rubberType, "category").Trim();
        if (category.Equals("Cuplump", StringComparison.OrdinalIgnoreCase)) return "Cuplump";
        if (category.Equals("USS", StringComparison.OrdinalIgnoreCase)) return "USS";
        return GetBookingQueueCategory(ReadProperty(rubberType, "code", "name"), [], "");
    }

    private static string GetBookingQueueCategory(string value, IReadOnlyList<JsonElement> rubberTypes, string bookingCode = "")
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            var found = rubberTypes.FirstOrDefault(item =>
                string.Equals(ReadProperty(item, "id"), value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "code"), value, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "name"), value, StringComparison.OrdinalIgnoreCase));
            var category = ReadProperty(found, "category");
            if (category.Equals("Cuplump", StringComparison.OrdinalIgnoreCase)) return "Cuplump";
            if (category.Equals("USS", StringComparison.OrdinalIgnoreCase)) return "USS";
            var upper = value.Replace('_', ' ').ToUpperInvariant();
            if (upper.Contains("USS", StringComparison.Ordinal)) return "USS";
            if (upper.Contains("CUPLUMP", StringComparison.Ordinal) || upper.Contains("CUP", StringComparison.Ordinal) ||
                upper.Contains("CL", StringComparison.Ordinal)) return "Cuplump";
        }
        var prefix = bookingCode.Trim().ToUpperInvariant();
        if (prefix.StartsWith('U')) return "USS";
        if (prefix.StartsWith('C')) return "Cuplump";
        return "";
    }

    private string ResolveBookingRubberTypeName(string value, string providedName)
    {
        if (!string.IsNullOrWhiteSpace(providedName)) return providedName;
        var found = _purchasingRubberTypes.FirstOrDefault(item =>
            string.Equals(ReadProperty(item, "id"), value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReadProperty(item, "code"), value, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReadProperty(item, "name"), value, StringComparison.OrdinalIgnoreCase));
        return FirstNonEmpty(ReadProperty(found, "name"), value, "—");
    }

    private BookingQueueRecord MapBookingQueueRecord(JsonElement raw)
    {
        var mapped = MapRawBookingQueueRecord(raw);
        var supplier = _purchasingSuppliers.FirstOrDefault(item =>
            ReadProperty(item, "id") == mapped.SupplierId ||
            ReadProperty(item, "code", "supplierCode", "supplier_code") == mapped.SupplierCode);
        var supplierCode = FirstNonEmpty(mapped.SupplierCode, ReadProperty(supplier, "code", "supplierCode", "supplier_code"));
        var supplierName = FirstNonEmpty(ReadSupplierDisplayName(supplier), mapped.SupplierName, "—");
        return mapped with
        {
            Category = GetBookingQueueCategory(mapped.RubberType, _purchasingRubberTypes, mapped.BookingCode),
            RubberTypeName = ResolveBookingRubberTypeName(mapped.RubberType, mapped.RubberTypeName),
            SupplierCode = supplierCode,
            SupplierName = supplierName
        };
    }

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";

    private static string FormatTruckRegister(string value)
    {
        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        var match = Regex.Match(normalized,
            @"^(?<series>\d{2})[\s-]+(?<number>\d{4})(?<province>\s+.*)?$",
            RegexOptions.CultureInvariant);
        return match.Success
            ? $"{match.Groups["series"].Value}-{match.Groups["number"].Value}{match.Groups["province"].Value}"
            : normalized;
    }

    private static int? GetBookingSlotLimit(string slot, DateTime date, string category)
    {
        if (slot == "10:00-11:00") return date.DayOfWeek == DayOfWeek.Saturday ? null : 4;
        if (date.DayOfWeek == DayOfWeek.Saturday && IsBookingSlotClosed(slot, date)) return 0;
        var config = BookingQueueSlots.FirstOrDefault(item => item.Slot == slot);
        return category == "USS" ? config.UssLimit : config.CuplumpLimit;
    }

    private static bool IsBookingSlotClosed(string slot, DateTime date)
        => date.DayOfWeek == DayOfWeek.Saturday &&
           int.TryParse(slot.Split(':', 2)[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour) && hour >= 11;

    private static string NormalizeSelectedSlot(string slot, DateTime date)
        => IsBookingSlotClosed(slot, date) ? "08:00-09:00" : slot;

    private static Style CreateBookingQueueCalendarStyle()
    {
        var dayButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.CalendarDayButton));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 38d));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 34d));
        dayButtonStyle.Setters.Add(new Setter(Control.FontSizeProperty, 15d));

        var monthButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.CalendarButton));
        monthButtonStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 42d));
        monthButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 34d));
        monthButtonStyle.Setters.Add(new Setter(Control.FontSizeProperty, 14d));

        var calendarStyle = new Style(typeof(System.Windows.Controls.Calendar));
        calendarStyle.Setters.Add(new Setter(Control.FontSizeProperty, 15d));
        calendarStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 310d));
        calendarStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        calendarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#D8E0EA")));
        calendarStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        calendarStyle.Setters.Add(new Setter(Control.TemplateProperty,
            System.Windows.Markup.XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                 xmlns:primitives="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework"
                                 TargetType="{x:Type Calendar}">
                    <Border x:Name="PART_Root" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            Padding="{TemplateBinding Padding}" CornerRadius="5"
                            SnapsToDevicePixels="True">
                        <primitives:CalendarItem x:Name="PART_CalendarItem"
                            Background="Transparent" BorderBrush="Transparent" BorderThickness="0"
                            Style="{TemplateBinding CalendarItemStyle}" />
                    </Border>
                </ControlTemplate>
                """)));
        calendarStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.CalendarDayButtonStyleProperty, dayButtonStyle));
        calendarStyle.Setters.Add(new Setter(System.Windows.Controls.Calendar.CalendarButtonStyleProperty, monthButtonStyle));
        return calendarStyle;
    }

    private static bool TryParseBookingSlot(string slot, out string start, out string end)
    {
        var pieces = slot.Split('-', 2, StringSplitOptions.TrimEntries);
        if (pieces.Length == 2 && TimeOnly.TryParseExact(pieces[0], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
            TimeOnly.TryParseExact(pieces[1], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            start = pieces[0];
            end = pieces[1];
            return true;
        }
        start = end = "";
        return false;
    }

    private double BookingQueueGridHeight()
    {
        if (_bookingQueueGrid is null || !_bookingQueueGrid.IsLoaded || !MainLayoutGrid.IsLoaded) return 280;
        const double footerHeight = 38;
        const double bottomAndPadding = 42;
        var gridTop = _bookingQueueGrid.TransformToAncestor(MainLayoutGrid).Transform(new Point(0, 0)).Y;
        return Math.Max(280, MainLayoutGrid.ActualHeight - footerHeight - gridTop - bottomAndPadding);
    }
}

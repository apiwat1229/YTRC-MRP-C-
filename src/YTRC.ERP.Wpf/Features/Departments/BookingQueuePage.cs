using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const decimal UssDefaultEstimatedWeightKg = 120_000m;
    private sealed record BookingQueueSlotRule(string Slot, int QueueStart, int? Limit, bool IsOpen);
    private List<BookingQueueSlotRule> _bookingQueueSlotRules = [];
    private string _bookingQueueSlotRuleError = "";

    private sealed record BookingQueueRecord(
        JsonElement Raw, string Id, int QueueNo, string BookingCode, DateTime Date,
        string StartTime, string EndTime, string Slot, string SupplierId,
        string SupplierCode, string SupplierName, string TruckType, string TruckRegister,
        string RubberType, string RubberTypeName, string Category, decimal? EstimatedWeightKg,
        string Recorder, string Status, string CheckInAt, string CheckOutAt,
        string RubberCondition, string Note)
    {
        public string QueueDisplay => $"{(Category == "USS" ? "U" : "C")}{QueueNo.ToString(CultureInfo.InvariantCulture)}";

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
            _bookingQueueSlotRules = [];
            _bookingQueueSlotRuleError = "";
            _bookingQueueLoading = false;
            _bookingQueueMessage = "Sign in to load live booking queue records.";
            RenderCurrentPage();
            return;
        }

        _bookingQueueLoading = true;
        _bookingQueueSlotRules = [];
        _bookingQueueSlotRuleError = "";
        _bookingQueueMessage = "";
        RenderCurrentPage();
        var isBookingDashboard = _purchasingAction == "Dashboard";
        var dashboardDateFrom = isBookingDashboard ? GetBookingDashboardDateFrom(category).Date : date;
        var dashboardDateTo = isBookingDashboard ? GetBookingDashboardDateTo(category).Date : date;
        var dashboardSupplierId = isBookingDashboard ? GetBookingDashboardSupplierId(category) : "";
        var dashboardRubberTypeId = isBookingDashboard ? GetBookingDashboardRubberTypeId(category) : "";
        var apiDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var apiDateFrom = dashboardDateFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var apiDateTo = dashboardDateTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dashboardUrl = dashboardDateFrom == dashboardDateTo
            ? $"/api/bookings/dashboard?date={apiDate}&dateFrom={apiDateFrom}&dateTo={apiDateTo}"
            : $"/api/bookings/dashboard?dateFrom={apiDateFrom}&dateTo={apiDateTo}";
        if (dashboardSupplierId.Length > 0)
            dashboardUrl += $"&supplierId={Uri.EscapeDataString(dashboardSupplierId)}";
        var isDashboardPayload = isBookingDashboard;
        var bookingsTask = _apiClient.GetAsync(
            isBookingDashboard
                ? dashboardUrl
                : $"/api/bookings?date={apiDate}",
            _session.Token);
        var slotConfigDate = isBookingDashboard ? dashboardDateTo : date;
        var slotConfigApiDate = slotConfigDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var slotConfigTask = _apiClient.GetAsync($"/api/bookings/queue-config?date={slotConfigApiDate}", _session.Token);
        var provincesTask = _bookingProvinceNames.Count == 0
            ? _apiClient.GetAsync("/api/master/provinces", _session.Token)
            : null;
        var supplierTask = _bookingQueueDependenciesLoaded
            ? null : _apiClient.GetAsync("/api/suppliers", _session.Token);
        var rubberTypesTask = _bookingQueueDependenciesLoaded
            ? null : _apiClient.GetAsync("/api/rubber-types?includeDeleted=true", _session.Token);

        await Task.WhenAll(bookingsTask, slotConfigTask);
        var bookingsResult = await bookingsTask;
        var dashboardCompatibilityMode = false;
        if (isBookingDashboard && !bookingsResult.Success && bookingsResult.StatusCode == 404 &&
            dashboardDateFrom == dashboardDateTo)
        {
            bookingsResult = await _apiClient.GetAsync($"/api/bookings?date={apiDate}", _session.Token);
            isDashboardPayload = false;
            dashboardCompatibilityMode = bookingsResult.Success;
        }
        var slotConfigResult = await slotConfigTask;
        if (provincesTask is not null)
        {
            var provincesResult = await provincesTask;
            if (provincesResult.Success)
            {
                _bookingProvinceNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var province in ExtractArray(provincesResult.Data))
                {
                    var thaiName = ReadProperty(province, "name_th", "name");
                    var englishName = ReadProperty(province, "name_en");
                    if (thaiName.Length == 0) continue;
                    var displayName = englishName.Length > 0 &&
                                      !englishName.Equals(thaiName, StringComparison.OrdinalIgnoreCase)
                        ? $"{thaiName} ({englishName})"
                        : thaiName;
                    if (englishName.Length > 0) _bookingProvinceNames[ProvinceLookupKey(englishName)] = displayName;
                    _bookingProvinceNames[ProvinceLookupKey(thaiName)] = displayName;
                }
            }
        }
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
        if (slotConfigResult.Success && slotConfigResult.Data.ValueKind == JsonValueKind.Object)
            _bookingQueueSlotRules = ParseBookingQueueSlotRules(slotConfigResult.Data);
        if (!slotConfigResult.Success)
            _bookingQueueSlotRuleError = $"HTTP {slotConfigResult.StatusCode}: {slotConfigResult.Error}";
        else if (slotConfigResult.Data.ValueKind != JsonValueKind.Object)
            _bookingQueueSlotRuleError = "The server returned an unexpected response for booking slot rules.";
        else if (_bookingQueueSlotRules.Count == 0)
            _bookingQueueSlotRuleError = "The server returned no booking slot rules.";
        _bookingQueueSlot = NormalizeSelectedSlot(_bookingQueueSlot, date);
        if (!bookingsResult.Success)
        {
            _bookingQueueRecords = [];
            _bookingQueueMessage = $"Failed to load booking queue: {bookingsResult.Error}";
        }
        else
        {
            var bookingPayload = isDashboardPayload
                ? ReadDashboardBookings(bookingsResult.Data)
                : bookingsResult.Data;
            _bookingQueueRecords = ExtractArray(bookingPayload)
                .Select(MapBookingQueueRecord)
                .Where(item => item.Category == category)
                .Where(item => isBookingDashboard
                    ? item.Date.Date >= dashboardDateFrom && item.Date.Date <= dashboardDateTo
                    : item.Date.Date == date)
                .Where(item => !isBookingDashboard || dashboardSupplierId.Length == 0 ||
                    item.SupplierId.Equals(dashboardSupplierId, StringComparison.OrdinalIgnoreCase))
                .Where(item => !isBookingDashboard ||
                    BookingMatchesDashboardRubberType(item, dashboardRubberTypeId))
                .ToList();
            _bookingQueueMessage = dashboardCompatibilityMode
                ? $"Compatibility: Dashboard data loaded for {dashboardDateFrom:dd MMM yyyy}. Update the backend to enable date range history."
                : isBookingDashboard
                    ? $"Loaded {_bookingQueueRecords.Count} booking record(s) from {dashboardDateFrom:dd MMM yyyy} to {dashboardDateTo:dd MMM yyyy}."
                    : $"Loaded {_bookingQueueRecords.Count} booking record(s) for {date:dd MMM yyyy}.";
        }
        _bookingQueueSelectedRecord = null;
        RenderCurrentPage();
    }

    private static JsonElement ReadDashboardBookings(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return payload;
        foreach (var property in payload.EnumerateObject())
            if (property.Name.Equals("bookings", StringComparison.OrdinalIgnoreCase))
                return property.Value.Clone();
        return payload;
    }

    private async Task<bool> LoadBookingQueueSlotRulesAsync(DateTime date)
    {
        var apiDate = date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var result = await _apiClient.GetAsync($"/api/bookings/queue-config?date={apiDate}", _session.Token);
        if (!result.Success || result.Data.ValueKind != JsonValueKind.Object)
        {
            _bookingQueueSlotRules = [];
            return false;
        }
        _bookingQueueSlotRules = ParseBookingQueueSlotRules(result.Data);
        return _bookingQueueSlotRules.Count > 0;
    }

    private static List<BookingQueueSlotRule> ParseBookingQueueSlotRules(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object) return [];
        var slotsElement = payload.EnumerateObject()
            .FirstOrDefault(property => property.Name.Equals("slots", StringComparison.OrdinalIgnoreCase))
            .Value;
        if (slotsElement.ValueKind != JsonValueKind.Array) return [];

        return slotsElement.EnumerateArray().Select(item =>
        {
            var slot = ReadProperty(item, "slot");
            var queueStart = int.TryParse(ReadProperty(item, "queueStart", "queue_start"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart) ? parsedStart : 1;
            var rawLimit = ReadProperty(item, "limit");
            int? limit = int.TryParse(rawLimit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLimit)
                ? parsedLimit : null;
            var isOpen = string.Equals(ReadProperty(item, "isOpen", "is_open"), "true", StringComparison.OrdinalIgnoreCase);
            return new BookingQueueSlotRule(slot, queueStart, limit, isOpen);
        }).Where(rule => rule.Slot.Length > 0).ToList();
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
        if (_purchasingAction == "Dashboard")
            return BuildBookingDashboardPage(category);

        var isSummary = _purchasingAction == "DailySummary";
        _bookingQueueSelectedRecord = null;
        _bookingQueueEditButton = null;
        _bookingQueueCancelButton = null;
        var queue = _bookingQueueRecords.Where(item => item.Category == category)
            .OrderBy(item => item.Slot, StringComparer.Ordinal).ThenBy(item => item.QueueNo).ToList();
        var page = new StackPanel();

        var toolbar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        void AlignActionButton(Button button)
        {
            button.VerticalAlignment = VerticalAlignment.Bottom;
            button.Margin = new Thickness(button.Margin.Left, 0, button.Margin.Right, 0);
        }

        var dateLabel = Label("Date", 11, CreateBrush("#68788D"), FontWeights.SemiBold);
        var dateText = new TextBlock
        {
            Text = _bookingQueueDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB")),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var dateButtonContent = new Grid();
        dateButtonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        dateButtonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dateButtonContent.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Calendar,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            FontSize = 20,
            Width = 20,
            Height = 20,
            Foreground = CreateBrush("#0F766E"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        Grid.SetColumn(dateText, 1);
        dateButtonContent.Children.Add(dateText);

        var dateButton = new Button
        {
            Content = dateButtonContent,
            Width = UiTokens.DatePickerWidth,
            Height = UiTokens.DatePickerHeight,
            Padding = new Thickness(6, 0, 6, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = "Choose booking date",
            Cursor = Cursors.Hand
        };
        var calendar = new System.Windows.Controls.Calendar
        {
            DisplayDate = _bookingQueueDate,
            SelectedDate = _bookingQueueDate,
            SelectionMode = CalendarSelectionMode.SingleDate,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
            Style = CreateBookingQueueCalendarStyle()
        };
        var datePopup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = dateButton,
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
        dateButton.Click += (_, _) =>
        {
            calendar.DisplayDate = _bookingQueueDate;
            calendar.SelectedDate = _bookingQueueDate;
            datePopup.IsOpen = true;
        };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not DateTime selectedDate) return;
            _bookingQueueDate = selectedDate.Date;
            dateText.Text = _bookingQueueDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
            _bookingQueueSlot = NormalizeSelectedSlot(_bookingQueueSlot, _bookingQueueDate);
            datePopup.IsOpen = false;
            _ = LoadBookingQueueAsync();
        };
        var dateField = new StackPanel { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        dateField.Children.Add(dateLabel);
        dateField.Children.Add(dateButton);
        toolbar.Children.Add(dateField);

        var createButton = PurchasingButton("New Booking", primary: true);
        createButton.IsEnabled = !_bookingQueueLoading && _bookingQueueSlotRules.Count > 0;
        AlignActionButton(createButton);
        createButton.Click += (_, _) => _ = OpenBookingQueueEditorAsync(category, null);
        toolbar.Children.Add(createButton);
        if (isSummary)
        {
            var manageButton = PurchasingButton("Manage Queue");
            AlignActionButton(manageButton);
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
            AlignActionButton(_bookingQueueEditButton);
            _bookingQueueEditButton.IsEnabled = false;
            _bookingQueueEditButton.Click += (_, _) =>
            {
                if (_bookingQueueSelectedRecord is not null)
                    _ = OpenBookingQueueEditorAsync(category, _bookingQueueSelectedRecord);
            };
            toolbar.Children.Add(_bookingQueueEditButton);

            _bookingQueueCancelButton = PurchasingButton("Cancel booking");
            if (_bookingQueueCancelButton.Content is FluentIcons.Wpf.FluentIcon cancelIcon)
            {
                cancelIcon.Foreground = CreateBrush("#B42318");
            }
            _bookingQueueCancelButton.BorderBrush = CreateBrush("#F4C7C3");
            _bookingQueueCancelButton.ToolTip = "Cancel the selected booking and keep its history";
            AlignActionButton(_bookingQueueCancelButton);
            _bookingQueueCancelButton.IsEnabled = false;
            _bookingQueueCancelButton.Click += async (_, _) => await CancelSelectedBookingQueueAsync();
            toolbar.Children.Add(_bookingQueueCancelButton);
        }
        var refreshButton = PurchasingButton("Refresh");
        AlignActionButton(refreshButton);
        refreshButton.Click += async (_, _) => await LoadBookingQueueAsync();
        toolbar.Children.Add(refreshButton);

        var viewButton = PurchasingButton(string.Empty);
        var viewButtonContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        viewButtonContent.Children.Add(new FluentIcon
        {
            Icon = _bookingQueueTicketView
                ? FluentIcons.Common.Icon.Table
                : FluentIcons.Common.Icon.CardUi,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            FontSize = 20,
            Width = 20,
            Height = 20,
            Foreground = CreateBrush("#0F766E"),
            VerticalAlignment = VerticalAlignment.Center
        });
        viewButtonContent.Children.Add(new TextBlock
        {
            Text = _bookingQueueTicketView ? "Table view" : "Ticket view",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        });
        viewButton.Content = viewButtonContent;
        viewButton.Padding = new Thickness(10, 0, 10, 0);
        viewButton.Margin = new Thickness(0, 0, 8, 0);
        viewButton.VerticalAlignment = VerticalAlignment.Bottom;
        viewButton.ToolTip = _bookingQueueTicketView ? "Switch to table view" : "Switch to ticket view";
        viewButton.Click += (_, _) =>
        {
            _bookingQueueTicketView = !_bookingQueueTicketView;
            RenderCurrentPage();
        };
        AlignActionButton(viewButton);
        toolbar.Children.Add(viewButton);

        var toolbarRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(toolbar, 0);
        toolbarRow.Children.Add(toolbar);
        var heading = SectionHeading(isSummary ? $"{category} Daily Summary" : $"{category} Booking Management");
        heading.Margin = new Thickness(12, 0, 0, 0);
        heading.VerticalAlignment = VerticalAlignment.Bottom;
        heading.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(heading, 1);
        toolbarRow.Children.Add(heading);
        page.Children.Add(toolbarRow);

        if (_bookingQueueLoading)
            page.Children.Add(Banner("Loading booking queue", "Retrieving bookings for the selected date…", "#0F766E"));
        else if (_bookingQueueMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            page.Children.Add(Banner("Booking queue unavailable", _bookingQueueMessage, "#B42318"));
        if (!_bookingQueueLoading && _bookingQueueSlotRuleError.Length > 0)
            page.Children.Add(Banner("Time slots unavailable", _bookingQueueSlotRuleError, "#B42318"));

        if (isSummary)
        {
            page.Children.Add(BuildBookingQueueSlotPanel(category, true));
        }
        else
        {
            page.Children.Add(BuildBookingQueueSlotPanel(category, false));
        }

        _bookingQueueGrid = null;
        _bookingQueueTicketCards = null;
        _bookingQueueTicketScrollViewer = null;
        if (_bookingQueueTicketView)
        {
            var cards = new StackPanel { Orientation = Orientation.Horizontal };
            var isDraggingCards = false;
            var isLeftButtonDown = false;
            var dragStartPoint = new Point();
            var dragStartOffset = 0d;
            var scrollViewer = new ScrollViewer
            {
                Content = cards,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CanContentScroll = false,
                Cursor = Cursors.Hand
            };
            scrollViewer.PreviewMouseLeftButtonDown += (_, e) =>
            {
                isLeftButtonDown = true;
                isDraggingCards = false;
                dragStartPoint = e.GetPosition(scrollViewer);
                dragStartOffset = scrollViewer.HorizontalOffset;
            };
            scrollViewer.PreviewMouseMove += (_, e) =>
            {
                if (!isLeftButtonDown || e.LeftButton != MouseButtonState.Pressed) return;
                var currentPoint = e.GetPosition(scrollViewer);
                var horizontalDelta = currentPoint.X - dragStartPoint.X;
                var verticalDelta = currentPoint.Y - dragStartPoint.Y;
                if (!isDraggingCards)
                {
                    if (Math.Abs(horizontalDelta) < SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(horizontalDelta) < Math.Abs(verticalDelta)) return;
                    isDraggingCards = true;
                    scrollViewer.CaptureMouse();
                    scrollViewer.Cursor = Cursors.SizeWE;
                }

                scrollViewer.ScrollToHorizontalOffset(dragStartOffset - horizontalDelta);
                e.Handled = true;
            };
            scrollViewer.PreviewMouseLeftButtonUp += (_, e) =>
            {
                isLeftButtonDown = false;
                if (isDraggingCards)
                {
                    isDraggingCards = false;
                    scrollViewer.ReleaseMouseCapture();
                    e.Handled = true;
                }
                scrollViewer.Cursor = Cursors.Hand;
            };
            scrollViewer.LostMouseCapture += (_, _) =>
            {
                isLeftButtonDown = false;
                isDraggingCards = false;
                scrollViewer.Cursor = Cursors.Hand;
            };
            scrollViewer.PreviewMouseWheel += (_, e) =>
            {
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            };
            _bookingQueueTicketCards = cards;
            _bookingQueueTicketScrollViewer = scrollViewer;

            var carousel = new Grid { Height = 540 };
            carousel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            carousel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            carousel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            var previousButton = new Button
            {
                Content = new FluentIcon
                {
                    Icon = FluentIcons.Common.Icon.ChevronLeft,
                    IconSize = IconSize.Size20,
                    IconVariant = IconVariant.Regular,
                    FontSize = 20,
                    Width = 20,
                    Height = 20,
                    Foreground = CreateBrush("#0F766E"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Width = 34,
                Height = 42,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource(typeof(Button)),
                ToolTip = "Previous tickets"
            };
            previousButton.Click += (_, _) => scrollViewer.ScrollToHorizontalOffset(
                Math.Max(0, scrollViewer.HorizontalOffset - scrollViewer.ViewportWidth * .8));
            carousel.Children.Add(previousButton);
            Grid.SetColumn(scrollViewer, 1);
            carousel.Children.Add(scrollViewer);
            var nextButton = new Button
            {
                Content = new FluentIcon
                {
                    Icon = FluentIcons.Common.Icon.ChevronRight,
                    IconSize = IconSize.Size20,
                    IconVariant = IconVariant.Regular,
                    FontSize = 20,
                    Width = 20,
                    Height = 20,
                    Foreground = CreateBrush("#0F766E"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Width = 34,
                Height = 42,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource(typeof(Button)),
                ToolTip = "Next tickets"
            };
            nextButton.Click += (_, _) => scrollViewer.ScrollToHorizontalOffset(
                Math.Min(scrollViewer.ScrollableWidth, scrollViewer.HorizontalOffset + scrollViewer.ViewportWidth * .8));
            Grid.SetColumn(nextButton, 2);
            carousel.Children.Add(nextButton);

            var ticketView = new Border
            {
                Background = Brushes.White,
                BorderBrush = (Brush)FindResource("BorderBrushSoft"),
                BorderThickness = new Thickness(1),
                CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
                Padding = new Thickness(5),
                Child = carousel
            };
            page.Children.Add(ticketView);
        }
        else
        {
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
                VerticalGridLinesBrush = Brushes.Transparent,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowHeaderWidth = 0
            };
            var cellStyle = new Style(typeof(DataGridCell), (Style)FindResource(typeof(DataGridCell)));
            cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            cellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
            grid.CellStyle = cellStyle;
            var rowStyle = new Style(typeof(DataGridRow), (Style)FindResource(typeof(DataGridRow)));
            rowStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            grid.RowStyle = rowStyle;
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
                CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
                Padding = new Thickness(8),
                Child = grid
            };
            page.Children.Add(card);
        }
        ApplyBookingQueueFilters(category, isSummary);
        return page;
    }

    private UIElement BuildBookingQueueSlotPanel(string category, bool summary)
    {
        var panel = new UniformGrid
        {
            Columns = Math.Max(1, _bookingQueueSlotRules.Count + (summary ? 0 : 1)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        if (!summary)
            panel.Children.Add(BuildSlotButton("ALL", "All slots", category, false, summary));
        foreach (var config in _bookingQueueSlotRules)
            panel.Children.Add(BuildSlotButton(config.Slot, config.Slot, category,
                !config.IsOpen, summary));
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
        var slotTextBrush = closed ? Brushes.Gray
            : isFull ? Brushes.IndianRed
            : (Brush)FindResource("TextBrush");
        body.Children.Add(Label(label, 14, slotTextBrush, FontWeights.Bold, true));
        var capacityText = closed ? "Closed" : limit is int max ? $"{count} / {max}" : $"{count} / Unlimited";
        var capacityBrush = closed ? Brushes.Gray
            : isFull ? Brushes.IndianRed
            : (Brush)FindResource("MutedBrush");
        body.Children.Add(Label(capacityText, 13, capacityBrush, FontWeights.Bold, true));
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
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            return style;
        }

        DataGridTextColumn Text(string header, string path, double width, bool center = false)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            if (center)
                textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                ElementStyle = textStyle
            };
            if (center)
            {
                column.HeaderStyle = CenteredHeaderStyle();
            }
            return column;
        }

        grid.Columns.Add(Text("Queue", nameof(BookingQueueRecord.QueueDisplay), .55, true));
        grid.Columns.Add(Text("Booking code", nameof(BookingQueueRecord.BookingCode), .9));
        grid.Columns.Add(Text("Time slot", nameof(BookingQueueRecord.Slot), .8));
        grid.Columns.Add(Text("Code", nameof(BookingQueueRecord.SupplierCode), .6, true));
        grid.Columns.Add(Text("Name", nameof(BookingQueueRecord.SupplierName), 1.3));
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
                        BorderBrush="{Binding StatusBorder}" BorderThickness="1" CornerRadius="__SMALL_RADIUS__"
                        HorizontalAlignment="Center" VerticalAlignment="Center">
                    <TextBlock Text="{Binding StatusLabel}" FontSize="11" FontWeight="SemiBold"
                               Foreground="{Binding StatusForeground}" />
                </Border>
            </DataTemplate>
            """.Replace("__SMALL_RADIUS__", UiTokens.CornerRadiusSmallValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
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
        if (_bookingQueueGrid is null && _bookingQueueTicketCards is null) return;
        var records = _bookingQueueRecords.Where(item => item.Category == category);
        if (!summary && _bookingQueueSlot != "ALL")
            records = records.Where(item => item.Slot == _bookingQueueSlot);
        var visibleRecords = records.OrderBy(item => item.Slot, StringComparer.Ordinal)
            .ThenBy(item => item.QueueNo).ToList();
        if (_bookingQueueGrid is not null)
            _bookingQueueGrid.ItemsSource = visibleRecords;
        if (_bookingQueueTicketCards is not null)
        {
            if (_bookingQueueSelectedRecord is not null && visibleRecords.All(item => item.Id != _bookingQueueSelectedRecord.Id))
                _bookingQueueSelectedRecord = null;
            _bookingQueueTicketCards.Children.Clear();
            foreach (var record in visibleRecords)
                _bookingQueueTicketCards.Children.Add(BuildBookingQueueTicketCard(record));
        }
        UpdateBookingQueueActionButtons();
    }

    private Border BuildBookingQueueTicketCard(BookingQueueRecord record)
    {
        var palette = GetBookingTicketPalette(record.Date);
        var ticket = new Border
        {
            Width = 286,
            Height = 520,
            Background = CreateBrush(palette.Background),
            BorderBrush = CreateBrush(palette.Border),
            BorderThickness = new Thickness(2),
            CornerRadius = UiTokens.CornerRadiusLarge,
            Padding = new Thickness(13, 12, 13, 14)
        };
        var body = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new Image
        {
            Source = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/YTRC-Logo.png", UriKind.Absolute)),
            Width = 88,
            Height = 28,
            Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });
        var title = new TextBlock
        {
            Text = $"Queue {record.Category}",
            FontSize = 13,
            FontWeight = FontWeights.ExtraBold,
            Foreground = CreateBrush("#1A1A1A"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        body.Children.Add(header);

        void AddInfo(string label, string value, bool bold = true, string? color = null)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CreateBrush(palette.Label),
                VerticalAlignment = VerticalAlignment.Top
            });
            var valueText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                FontSize = 11.5,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = CreateBrush(color ?? (bold ? "#1A1A1A" : palette.Muted)),
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);
            body.Children.Add(row);
        }

        AddInfo("Code:", record.SupplierCode);
        AddInfo("Name:", record.SupplierName);
        AddInfo("Date:", FormatBookingTicketDate(record.Date), false, "#1A1A1A");
        AddInfo("Time:", record.Slot.Replace('-', '–'), false, "#1A1A1A");
        AddInfo("Truck Type:", FormatBookingTruckType(record.TruckType));
        AddInfo("Truck Register:", record.TruckRegisterDisplay);

        var rubberTypeRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        rubberTypeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
        rubberTypeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rubberTypeRow.Children.Add(new TextBlock
        {
            Text = "Rubber Type:",
            FontSize = 11.5,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush(palette.Label),
            VerticalAlignment = VerticalAlignment.Center
        });
        var typeBadge = new Border
        {
            Padding = new Thickness(6, 1, 6, 1),
            Background = record.RubberTypeBadge.Background,
            BorderBrush = record.RubberTypeBadge.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = record.RubberTypeBadge.Label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = record.RubberTypeBadge.Foreground
            }
        };
        Grid.SetColumn(typeBadge, 1);
        rubberTypeRow.Children.Add(typeBadge);
        body.Children.Add(rubberTypeRow);

        var queueRow = new Grid { Margin = new Thickness(0, 5, 0, 1) };
        queueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        queueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        queueRow.Children.Add(new TextBlock
        {
            Text = "Queue:",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#1A1A1A"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var queueBadge = new Border
        {
            Width = 58,
            Height = 48,
            CornerRadius = UiTokens.CornerRadiusLarge,
            Background = CreateBrush(palette.QueueBadge),
            Child = new TextBlock
            {
                Text = record.QueueDisplay,
                FontSize = 22,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        Grid.SetColumn(queueBadge, 1);
        queueRow.Children.Add(queueBadge);
        body.Children.Add(queueRow);
        body.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Height = 1,
            Margin = new Thickness(0, 7, 0, 6),
            Stroke = CreateBrush(palette.Divider),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        });
        body.Children.Add(new TextBlock
        {
            Text = "สามารถจอดรถค้างคืนได้\nบริเวณด้านหน้าโรงงาน",
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            LineHeight = 16,
            Foreground = CreateBrush(palette.Muted),
            Margin = new Thickness(0, 0, 0, 3)
        });
        body.Children.Add(new TextBlock
        {
            Text = "*ห้ามจอดรถบนถนนทางเข้าโรงงาน*",
            TextAlignment = TextAlignment.Center,
            FontSize = 11,
            FontWeight = FontWeights.ExtraBold,
            Foreground = CreateBrush("#CC1111"),
            Margin = new Thickness(0, 0, 0, 5)
        });
        var qrImage = new Image
        {
            Source = CreateBookingQrCode(record.BookingCode, palette.Background),
            Width = 96,
            Height = 96,
            HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.HighQuality);
        body.Children.Add(qrImage);

        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var copyButton = BookingTicketActionButton(FluentIcons.Common.Icon.Copy, "Copy ticket image");
        copyButton.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetImage(RenderBookingTicketImage(record, palette));
                copyButton.ToolTip = "Ticket image copied";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not copy the ticket image: {ex.Message}",
                    "Copy ticket", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        actionButtons.Children.Add(copyButton);

        var downloadButton = BookingTicketActionButton(FluentIcons.Common.Icon.ArrowDownload, "Download ticket image");
        downloadButton.Click += (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Download booking ticket",
                Filter = "PNG image (*.png)|*.png",
                DefaultExt = ".png",
                AddExtension = true,
                FileName = $"Ticket_{record.BookingCode}_{record.Date:yyyyMMdd}.png",
                OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(RenderBookingTicketImage(record, palette)));
                using var stream = File.Create(dialog.FileName);
                encoder.Save(stream);
                downloadButton.ToolTip = $"Saved to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not download the ticket image: {ex.Message}",
                    "Download ticket", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        actionButtons.Children.Add(downloadButton);

        var cardLayout = new Grid();
        cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        cardLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(body, 0);
        cardLayout.Children.Add(body);
        Grid.SetRow(actionButtons, 1);
        cardLayout.Children.Add(actionButtons);
        ticket.Child = cardLayout;

        var selected = _bookingQueueSelectedRecord?.Id == record.Id;
        var frame = new Border
        {
            Width = 300,
            Margin = new Thickness(5, 0, 5, 0),
            Padding = new Thickness(1),
            BorderThickness = new Thickness(2),
            BorderBrush = selected ? CreateBrush("#0F766E") : Brushes.Transparent,
            CornerRadius = UiTokens.CornerRadiusLarge,
            Tag = record.Id,
            Child = ticket,
            ToolTip = "Select ticket to edit"
        };
        frame.MouseLeftButtonUp += (_, _) =>
        {
            _bookingQueueSelectedRecord = record;
            foreach (var card in _bookingQueueTicketCards?.Children.OfType<Border>() ?? [])
                card.BorderBrush = Equals(card.Tag?.ToString(), record.Id)
                    ? CreateBrush("#0F766E") : Brushes.Transparent;
            UpdateBookingQueueActionButtons();
        };
        return frame;
    }

    private Button BookingTicketActionButton(FluentIcons.Common.Icon icon, string label)
    {
        var button = new Button
        {
            Content = new FluentIcon
            {
                Icon = icon,
                IconSize = IconSize.Size16,
                IconVariant = IconVariant.Regular,
                FontSize = 16,
                Width = 16,
                Height = 16,
                Foreground = CreateBrush("#0F766E"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 26,
            Height = 24,
            Padding = new Thickness(0),
            Margin = new Thickness(4, 0, 0, 0),
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = label,
            Cursor = Cursors.Hand
        };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    private void UpdateBookingQueueActionButtons()
    {
        var selected = _bookingQueueSelectedRecord;
        if (_bookingQueueEditButton is not null)
            _bookingQueueEditButton.IsEnabled = selected is not null && selected.Status is not ("COMPLETED" or "CANCELLED");
        if (_bookingQueueCancelButton is not null)
            _bookingQueueCancelButton.IsEnabled = selected is not null && selected.Status != "CANCELLED" && !_session.IsPreview;
    }

    private async Task CancelSelectedBookingQueueAsync()
    {
        var record = _bookingQueueSelectedRecord;
        if (record is null || record.Status == "CANCELLED") return;
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Sign in to cancel bookings.", "Sign in required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmation =
            $"Cancel booking {record.BookingCode} ({record.QueueDisplay})?\n\n" +
            $"Supplier: {record.SupplierCode} · {record.SupplierName}\n" +
            $"Date: {record.Date:dd MMM yyyy}   Time: {record.Slot}\n\n" +
            "This will mark the booking as Cancelled and keep its history. " +
            "Bookings with received lots cannot be cancelled.";
        if (MessageBox.Show(this, confirmation, "Confirm booking cancellation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var result = await _apiClient.DeleteAsync(
            $"/api/bookings/{Uri.EscapeDataString(record.Id)}", _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Booking cancellation failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var isApprovalRequest = result.Data is JsonElement response &&
                                ReadProperty(response, "actionType").Equals("DELETE", StringComparison.OrdinalIgnoreCase) &&
                                ReadProperty(response, "status").Equals("PENDING", StringComparison.OrdinalIgnoreCase);
        MessageBox.Show(this,
            isApprovalRequest
                ? "The booking cancellation request was submitted for approval."
                : "The booking was cancelled. Its history is retained.",
            isApprovalRequest ? "Cancellation request submitted" : "Booking cancelled",
            MessageBoxButton.OK, MessageBoxImage.Information);
        _bookingQueueSelectedRecord = null;
        await LoadBookingQueueAsync();
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
        var supplierChoices = _purchasingSuppliers.Select(item =>
        {
            var code = ReadProperty(item, "code", "supplierCode", "supplier_code");
            var name = ReadProperty(item, "name", "supplierName", "supplier_name");
            var label = code.Length > 0 && name.Length > 0 ? $"{code} · {name}" : FirstNonEmpty(code, name);
            return new PurchasingChoice(ReadProperty(item, "id"), label);
        })
            .Where(item => item.Value.Length > 0 && item.Label.Length > 0).ToList();
        var typeChoices = _purchasingRubberTypes
            .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt", "deleted_at")) &&
                           GetCategoryForRubberType(item) == category)
            .Select(item => new PurchasingChoice(
                FirstNonEmpty(ReadProperty(item, "code"), ReadProperty(item, "name")),
                ReadProperty(item, "name")))
            .Where(item => item.Value.Length > 0 && item.Label.Length > 0).ToList();
        var requestedSlot = record?.Slot ?? _bookingQueueSlot;
        if (!await LoadBookingQueueSlotRulesAsync(date))
        {
            MessageBox.Show(this, "Could not load booking time slot rules from the server.",
                "Slot rules unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var initialSlot = requestedSlot == "ALL"
            ? _bookingQueueSlotRules.FirstOrDefault(item => item.IsOpen)?.Slot ?? ""
            : requestedSlot;
        var slotChoices = _bookingQueueSlotRules.Select(item =>
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
            if (IsBookingSlotClosed(item.Slot, date)) text = "Closed for selected date";
            else if (limit is int displayedCapacity && used >= displayedCapacity) text = "Full";
            if (unavailable && !current) return null;
            return new PurchasingChoice(item.Slot, $"{item.Slot}  ·  {text}");
        }).Where(item => item is not null).Select(item => item!).ToList();

        var fields = new List<PurchasingField>();
        if (record is null)
            fields.Add(new("date", "Booking date", PurchasingFieldKind.Date,
                date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Required: true));
        fields.Add(new("slot", "Time slot", PurchasingFieldKind.Choice, initialSlot, Required: true, Choices: slotChoices));
        fields.Add(new("supplierId", "Name", PurchasingFieldKind.Choice,
            record?.SupplierId ?? "", Choices: supplierChoices));
        fields.Add(new("truckRegister", "Truck plate / register", InitialValue: record?.TruckRegister ?? ""));
        fields.Add(new("truckType", "Truck type", PurchasingFieldKind.Choice, record?.TruckType ?? "", Choices:
        [
            new("6-wheel", "รถ 6 ล้อ"), new("10-wheel", "รถ 10 ล้อ"),
            new("18-wheel", "รถ 10 ล้อ (พ่วง)"), new("trailer", "รถเทรลเลอร์"), new("pickup", "รถกระบะ")
        ]));
        var requiresRubberTypeCorrection = record is not null &&
            GetBookingQueueCategory(record.RubberType, _purchasingRubberTypes) != category;
        fields.Add(new("rubberType",
            requiresRubberTypeCorrection ? $"Rubber type (select a {category} type)" : "Rubber type",
            PurchasingFieldKind.Choice, record?.RubberType ?? "",
            Required: requiresRubberTypeCorrection, Choices: typeChoices));
        fields.Add(new("trailerRubberType", "Trailer rubber type (optional)", PurchasingFieldKind.Choice,
            record is null ? "" : ReadProperty(record.Raw, "trailerRubberType", "trailer_rubber_type"),
            Choices: typeChoices));
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
        if (!await LoadBookingQueueSlotRulesAsync(dateValue))
        {
            MessageBox.Show(this, "Could not load booking time slot rules from the server.",
                "Slot rules unavailable", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (IsBookingSlotClosed(slot, dateValue))
        {
            MessageBox.Show(this, "This time slot is closed for the selected date. Choose an available slot.",
                "Slot unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rubberType = payload.GetValueOrDefault("rubberType")?.ToString() ?? "";
        var trailerRubberType = payload.GetValueOrDefault("trailerRubberType")?.ToString() ?? "";
        if (rubberType.Length > 0 && GetBookingQueueCategory(rubberType, _purchasingRubberTypes) != category)
        {
            MessageBox.Show(this, $"Select a {category} rubber type for this queue.", "Category mismatch",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (trailerRubberType.Length > 0 &&
            GetBookingQueueCategory(trailerRubberType, _purchasingRubberTypes) != category)
        {
            MessageBox.Show(this, $"Select a {category} rubber type for the trailer lot.", "Category mismatch",
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
            ["trailerRubberType"] = trailerRubberType.Length == 0 ? null : trailerRubberType,
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
        var checkOutAt = ReadProperty(raw, "weightOutAt", "weight_out_at", "weightoutAt");
        var rubberCondition = NormalizeRubberCondition(ReadProperty(raw, "rubberCondition", "rubber_condition"));
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
        if (rubberCondition.Length == 0)
            rubberCondition = NormalizeRubberCondition(note);
        if (NormalizeRubberCondition(note).Length > 0)
            note = "";
        var category = GetBookingQueueCategory(rubberType, [], code);
        return new BookingQueueRecord(raw.Clone(), id, queueNo, code, date, start, end, slot,
            supplierId, supplierCode, supplierName, ReadProperty(raw, "truckType", "truck_type"),
            ReadProperty(raw, "truckRegister", "truck_register"), rubberType, typeName, category,
            estimatedWeight, recorder, status, checkInAt, checkOutAt, rubberCondition, note);
    }

    private static string NormalizeRubberCondition(string value)
    {
        var normalized = value.Trim();
        if (normalized.Equals("DRY", StringComparison.OrdinalIgnoreCase) || normalized == "แห้ง") return "DRY";
        if (normalized.Equals("DAMP", StringComparison.OrdinalIgnoreCase) || normalized == "หมาด") return "DAMP";
        if (normalized.Equals("WET", StringComparison.OrdinalIgnoreCase) || normalized == "เปียก") return "WET";
        return "";
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
        var normalizedBookingCode = bookingCode.Trim().ToUpperInvariant();
        if (normalizedBookingCode.StartsWith("CANCELLED-", StringComparison.Ordinal))
            normalizedBookingCode = normalizedBookingCode["CANCELLED-".Length..];
        if (normalizedBookingCode.StartsWith('U')) return "USS";
        if (normalizedBookingCode.StartsWith('C')) return "Cuplump";

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

    private int? GetBookingSlotLimit(string slot, DateTime date, string category)
    {
        _ = date;
        _ = category;
        return _bookingQueueSlotRules.FirstOrDefault(item => item.Slot == slot)?.Limit;
    }

    private bool IsBookingSlotClosed(string slot, DateTime date)
    {
        _ = date;
        if (slot == "ALL") return false;
        var rule = _bookingQueueSlotRules.FirstOrDefault(item => item.Slot == slot);
        return rule is null || !rule.IsOpen;
    }

    private string NormalizeSelectedSlot(string slot, DateTime date)
        => IsBookingSlotClosed(slot, date)
            ? _bookingQueueSlotRules.FirstOrDefault(rule => rule.IsOpen)?.Slot ?? ""
            : slot;

    internal static Style CreateBookingQueueCalendarStyle()
    {
        var dayButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.CalendarDayButton));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 37d));
        dayButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 35d));
        dayButtonStyle.Setters.Add(new Setter(Control.FontSizeProperty, 15d));
        dayButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty, CreateBrush("#1E2D3E")));
        dayButtonStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        dayButtonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        dayButtonStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        dayButtonStyle.Setters.Add(new Setter(Control.TemplateProperty,
            System.Windows.Markup.XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                 xmlns:primitives="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework"
                                 TargetType="{x:Type primitives:CalendarDayButton}">
                    <Border x:Name="Chrome" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="__MEDIUM_RADIUS__" SnapsToDevicePixels="True">
                        <ContentPresenter Content="{TemplateBinding Content}"
                                          ContentTemplate="{TemplateBinding ContentTemplate}"
                                          ContentStringFormat="{TemplateBinding ContentStringFormat}"
                                          HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property="IsMouseOver" Value="True">
                            <Setter TargetName="Chrome" Property="Background" Value="#EAF4F2" />
                        </Trigger>
                        <Trigger Property="IsInactive" Value="True">
                            <Setter Property="Foreground" Value="#98A2B3" />
                        </Trigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter Property="Foreground" Value="#B7C0CA" />
                        </Trigger>
                        <Trigger Property="IsToday" Value="True">
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#0F766E" />
                            <Setter TargetName="Chrome" Property="BorderThickness" Value="1" />
                        </Trigger>
                        <Trigger Property="IsSelected" Value="True">
                            <Setter TargetName="Chrome" Property="Background" Value="#B7D5CF" />
                            <Setter TargetName="Chrome" Property="BorderBrush" Value="#0F766E" />
                            <Setter TargetName="Chrome" Property="BorderThickness" Value="1" />
                            <Setter Property="Foreground" Value="#0F625B" />
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
                """.Replace("__MEDIUM_RADIUS__", UiTokens.CornerRadiusMediumValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))));

        var monthButtonStyle = new Style(typeof(System.Windows.Controls.Primitives.CalendarButton));
        monthButtonStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 40d));
        monthButtonStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 35d));
        monthButtonStyle.Setters.Add(new Setter(Control.FontSizeProperty, 15d));

        var calendarStyle = new Style(typeof(System.Windows.Controls.Calendar));
        calendarStyle.Setters.Add(new Setter(Control.FontSizeProperty, 15d));
        calendarStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 280d));
        calendarStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
        calendarStyle.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#D8E0EA")));
        calendarStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        calendarStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        calendarStyle.Setters.Add(new Setter(Control.TemplateProperty,
            System.Windows.Markup.XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                 xmlns:primitives="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework"
                                 TargetType="{x:Type Calendar}">
                    <Border x:Name="PART_Root" Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            Padding="{TemplateBinding Padding}" CornerRadius="__SMALL_RADIUS__"
                            SnapsToDevicePixels="True">
                        <primitives:CalendarItem x:Name="PART_CalendarItem"
                            Background="Transparent" BorderBrush="Transparent" BorderThickness="0"
                            HorizontalAlignment="Center" HorizontalContentAlignment="Center"
                            Style="{TemplateBinding CalendarItemStyle}" />
                    </Border>
                </ControlTemplate>
                """.Replace("__SMALL_RADIUS__", UiTokens.CornerRadiusSmallValue.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))));
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

using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Data;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private DateTime _cuplumpDashboardDateFrom = DateTime.Today;
    private DateTime _cuplumpDashboardDateTo = DateTime.Today;
    private bool _cuplumpDashboardRangeInitialized;
    private string _cuplumpDashboardSupplierId = "";
    private string _cuplumpDashboardRubberTypeId = "";
    private DateTime _ussDashboardDateFrom = DateTime.Today;
    private DateTime _ussDashboardDateTo = DateTime.Today;
    private bool _ussDashboardRangeInitialized;
    private string _ussDashboardSupplierId = "";
    private string _ussDashboardRubberTypeId = "";

    private DateTime GetBookingDashboardDateFrom(string category)
        => category == "Cuplump" ? _cuplumpDashboardDateFrom : _ussDashboardDateFrom;

    private DateTime GetBookingDashboardDateTo(string category)
        => category == "Cuplump" ? _cuplumpDashboardDateTo : _ussDashboardDateTo;

    private string GetBookingDashboardSupplierId(string category)
        => category == "Cuplump" ? _cuplumpDashboardSupplierId : _ussDashboardSupplierId;

    private string GetBookingDashboardRubberTypeId(string category)
        => category == "Cuplump" ? _cuplumpDashboardRubberTypeId : _ussDashboardRubberTypeId;

    private void SetBookingDashboardDateFrom(string category, DateTime value)
    {
        if (category == "Cuplump") _cuplumpDashboardDateFrom = value.Date;
        else _ussDashboardDateFrom = value.Date;
    }

    private void SetBookingDashboardDateTo(string category, DateTime value)
    {
        if (category == "Cuplump") _cuplumpDashboardDateTo = value.Date;
        else _ussDashboardDateTo = value.Date;
    }

    private void SetBookingDashboardSupplierId(string category, string value)
    {
        if (category == "Cuplump") _cuplumpDashboardSupplierId = value;
        else _ussDashboardSupplierId = value;
    }

    private void SetBookingDashboardRubberTypeId(string category, string value)
    {
        if (category == "Cuplump") _cuplumpDashboardRubberTypeId = value;
        else _ussDashboardRubberTypeId = value;
    }

    private sealed record BookingDashboardSupplierChoice(string Id, string Label);
    private sealed record BookingDashboardRubberTypeChoice(string Id, string Code, string Name, string Label);

    private sealed record BookingDashboardRow(BookingQueueRecord Booking, string WeightDisplay)
    {
        public string QueueDisplay => Booking.QueueDisplay;
        public string DateDisplay => Booking.Date.ToString("dd MMM yy", CultureInfo.GetCultureInfo("en-GB"));
        public string BookingCode => Booking.BookingCode;
        public string Slot => Booking.Slot;
        public string SupplierCode => Booking.SupplierCode;
        public string SupplierName => Booking.SupplierName;
        public string Truck => Booking.TruckRegisterDisplay;
        public string TruckType => FormatBookingTruckType(Booking.TruckType);
        public string Status => Booking.StatusLabel;
        public string Note => string.IsNullOrWhiteSpace(Booking.Note) ? "—" : Booking.Note;
    }

    private UIElement BuildBookingDashboardPage(string category)
    {
        var isCuplump = category == "Cuplump";
        if (isCuplump && !_cuplumpDashboardRangeInitialized)
        {
            _cuplumpDashboardDateFrom = _bookingQueueDate.Date;
            _cuplumpDashboardDateTo = _bookingQueueDate.Date;
            _cuplumpDashboardRangeInitialized = true;
        }
        else if (!isCuplump && !_ussDashboardRangeInitialized)
        {
            _ussDashboardDateFrom = _bookingQueueDate.Date;
            _ussDashboardDateTo = _bookingQueueDate.Date;
            _ussDashboardRangeInitialized = true;
        }
        var dateFrom = GetBookingDashboardDateFrom(category);
        var dateTo = GetBookingDashboardDateTo(category);
        var supplierId = GetBookingDashboardSupplierId(category);
        var rubberTypeId = GetBookingDashboardRubberTypeId(category);

        var records = _bookingQueueRecords
            .Where(item => item.Category == category)
            .OrderBy(item => item.Date.Date)
            .ThenBy(item => item.Slot, StringComparer.Ordinal)
            .ThenBy(item => item.QueueNo)
            .ToList();
        var page = new StackPanel();

        var toolbar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        toolbar.Children.Add(CreateBookingDashboardDateField("From date", dateFrom,
            selected => SetBookingDashboardDateFrom(category, selected)));
        toolbar.Children.Add(CreateBookingDashboardDateField("To date", dateTo,
            selected => SetBookingDashboardDateTo(category, selected)));

        var supplierField = new StackPanel
        {
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        supplierField.Children.Add(Label("Supplier", 12, CreateBrush("#68788D"), FontWeights.SemiBold));
        var supplierChoices = GetBookingDashboardSupplierChoices(records, supplierId);
        var supplierFilter = new ComboBox
        {
            Width = 250,
            Height = 36,
            FontSize = 13,
            Margin = new Thickness(0, 1, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            SelectedValuePath = nameof(ComboBoxItem.Tag),
            ItemsSource = supplierChoices.Select(CreateDashboardChoiceItem).ToList()
        };
        supplierFilter.SelectedValue = supplierId;
        supplierField.Children.Add(supplierFilter);
        toolbar.Children.Add(supplierField);

        var rubberTypeField = new StackPanel
        {
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        rubberTypeField.Children.Add(Label("Rubber Type", 12, CreateBrush("#68788D"), FontWeights.SemiBold));
        var rubberTypeChoices = GetBookingDashboardRubberTypeChoices(records, category, rubberTypeId);
        var rubberTypeFilter = new ComboBox
        {
            Width = 230,
            Height = 36,
            FontSize = 13,
            Margin = new Thickness(0, 1, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            SelectedValuePath = nameof(ComboBoxItem.Tag),
            ItemsSource = rubberTypeChoices.Select(CreateDashboardChoiceItem).ToList()
        };
        rubberTypeFilter.SelectedValue = rubberTypeId;
        rubberTypeField.Children.Add(rubberTypeFilter);
        toolbar.Children.Add(rubberTypeField);

        var viewHistoryButton = PurchasingButton("View history", primary: true);
        viewHistoryButton.Height = 36;
        viewHistoryButton.MinWidth = 124;
        viewHistoryButton.FontSize = 13;
        viewHistoryButton.VerticalAlignment = VerticalAlignment.Bottom;
        viewHistoryButton.IsEnabled = !_bookingQueueLoading;
        viewHistoryButton.Click += async (_, _) =>
        {
            var selectedDateFrom = GetBookingDashboardDateFrom(category).Date;
            var selectedDateTo = GetBookingDashboardDateTo(category).Date;
            if (selectedDateFrom > selectedDateTo)
            {
                MessageBox.Show(this, "The start date must be on or before the end date.",
                    "Invalid date range", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBookingDashboardSupplierId(category, supplierFilter.SelectedValue?.ToString() ?? supplierId);
            SetBookingDashboardRubberTypeId(category, rubberTypeFilter.SelectedValue?.ToString() ?? "");
            _bookingQueueDate = selectedDateTo;
            _bookingQueueSlot = NormalizeSelectedSlot(_bookingQueueSlot, _bookingQueueDate);
            await LoadBookingQueueAsync();
        };
        toolbar.Children.Add(viewHistoryButton);

        var toolbarRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarRow.Children.Add(toolbar);
        var heading = SectionHeading($"{category} Booking Dashboard");
        heading.Margin = new Thickness(12, 0, 0, 0);
        heading.VerticalAlignment = VerticalAlignment.Bottom;
        heading.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(heading, 1);
        toolbarRow.Children.Add(heading);
        page.Children.Add(toolbarRow);

        if (_bookingQueueLoading)
            page.Children.Add(Banner("Loading dashboard", "Retrieving booking and lot summaries…", "#0F766E"));
        else if (_bookingQueueMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
            page.Children.Add(Banner("Dashboard unavailable", _bookingQueueMessage, "#B42318"));
        else if (_bookingQueueMessage.StartsWith("Compatibility:", StringComparison.OrdinalIgnoreCase))
            page.Children.Add(Banner("Dashboard data loaded", _bookingQueueMessage["Compatibility:".Length..].Trim(), "#A76B13"));
        if (!_bookingQueueLoading && _bookingQueueSlotRuleError.Length > 0)
            page.Children.Add(Banner("Time slots unavailable", _bookingQueueSlotRuleError, "#B42318"));

        var allLots = records.SelectMany(GetDashboardLots).ToList();
        var activeRecords = records.Where(item => item.Status != "CANCELLED").ToList();
        var receivedLots = allLots.Count(lot => IsLotReceived(lot));
        var expectedWeight = allLots.Sum(lot => ReadDashboardWeight(lot, "expectedWeight", "expected_weight"));
        var receivedWeight = allLots.Sum(lot => ReadDashboardWeight(lot, "receivedWeight", "received_weight"));
        if (allLots.Count == 0)
            expectedWeight = records.Sum(item => item.EstimatedWeightKg ?? 0m);

        var metrics = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 8) };
        metrics.Children.Add(DashboardMetric("Bookings", records.Count.ToString("N0"),
            $"{activeRecords.Count:N0} active · {records.Count(item => item.Status == "CANCELLED"):N0} cancelled", "#0F766E"));
        metrics.Children.Add(DashboardMetric("Lots", allLots.Count.ToString("N0"),
            "Rubber lots attached to bookings", "#2563EB"));
        metrics.Children.Add(DashboardMetric("Received lots", $"{receivedLots:N0} / {allLots.Count:N0}",
            allLots.Count == 0 ? "No lot records" : $"{allLots.Count - receivedLots:N0} pending full receipt", "#16804A"));
        metrics.Children.Add(DashboardMetric("Expected weight", FormatDashboardWeight(expectedWeight),
            "Sum of planned lot weights", "#7042A5"));
        metrics.Children.Add(DashboardMetric("Received weight", FormatDashboardWeight(receivedWeight),
            "Recorded full-lot receipts", "#C26A13"));
        var labSamples = records.Sum(item => ReadDashboardLabSampleCount(item.Raw));
        metrics.Children.Add(DashboardMetric("Lab samples", labSamples.ToString("N0"),
            "MAIN and TRAILER samples", "#0E7490"));
        page.Children.Add(metrics);

        var charts = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        charts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        charts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        charts.Children.Add(DashboardChartCard("Booking status", BuildStatusChart(records)));
        var isSingleDate = dateFrom.Date == dateTo.Date;
        var slotChartCard = DashboardChartCard("Bookings by time slot", BuildSlotChart(activeRecords, isSingleDate));
        Grid.SetColumn(slotChartCard, 1);
        charts.Children.Add(slotChartCard);
        page.Children.Add(charts);

        var rubberTypeCard = DashboardChartCard("Rubber type and Lot receipt", BuildRubberTypeChart(allLots));
        rubberTypeCard.Margin = new Thickness(0, 0, 0, 8);
        page.Children.Add(rubberTypeCard);

        page.Children.Add(SectionHeading("Booking and Lot details"));
        page.Children.Add(Label("Each booking is shown once. Select a date range, Supplier, and Rubber Type to review its history.",
            13, (Brush)FindResource("MutedBrush")));

        var filterRow = new WrapPanel { Margin = new Thickness(0, 6, 0, 7), VerticalAlignment = VerticalAlignment.Center };
        var searchBox = new TextBox
        {
            Width = 330,
            Height = 34,
            FontSize = 13,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(9, 4, 9, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search queue, supplier, truck, type, weight, or note"
        };
        filterRow.Children.Add(searchBox);
        var statusFilter = new ComboBox
        {
            Width = 170,
            Height = 34,
            FontSize = 13,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Filter by booking status"
        };
        statusFilter.Items.Add(new ComboBoxItem { Content = "All statuses", Tag = "" });
        foreach (var recordStatus in records.Select(item => item.Status).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
            statusFilter.Items.Add(new ComboBoxItem
            {
                Content = records.First(item => item.Status == recordStatus).StatusLabel,
                Tag = recordStatus
            });
        statusFilter.SelectedIndex = 0;
        filterRow.Children.Add(statusFilter);
        var resultCount = Label($"{records.Count:N0} booking(s)", 13, (Brush)FindResource("MutedBrush"));
        resultCount.VerticalAlignment = VerticalAlignment.Center;
        filterRow.Children.Add(resultCount);
        page.Children.Add(filterRow);

        var rows = records.Select(item => new BookingDashboardRow(item, FormatDashboardBookingWeight(item))).ToList();
        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 220,
            Height = 380,
            RowHeight = 50,
            FontSize = 13,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(230, 235, 241)),
            VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(238, 242, 246)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        var cellStyle = new Style(typeof(DataGridCell), (Style)FindResource(typeof(DataGridCell)));
        cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        grid.CellStyle = cellStyle;
        AddDashboardColumns(grid);
        grid.AddHandler(Button.ClickEvent, new RoutedEventHandler(BookingDashboardTicket_Click));
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is BookingDashboardRow selected) OpenBookingTicket(selected.Booking);
        };
        _bookingQueueGrid = grid;
        page.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(6),
            Child = grid
        });

        void ApplyFilters()
        {
            var query = searchBox.Text.Trim();
            var selectedStatus = (statusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            var visible = rows.Where(row =>
                (selectedStatus.Length == 0 || row.Booking.Status.Equals(selectedStatus, StringComparison.OrdinalIgnoreCase)) &&
                (query.Length == 0 ||
                 row.DateDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.BookingCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.SupplierCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.SupplierName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.Truck.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.TruckType.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.WeightDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.Note.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            grid.ItemsSource = visible;
            resultCount.Text = $"{visible.Count:N0} booking(s)";
        }

        searchBox.TextChanged += (_, _) => ApplyFilters();
        statusFilter.SelectionChanged += (_, _) => ApplyFilters();
        ApplyFilters();
        grid.Loaded += (_, _) => grid.Height = Math.Max(220, BookingQueueGridHeight());
        return page;
    }

    private StackPanel CreateBookingDashboardDateField(string label, DateTime selectedDate, Action<DateTime> onSelected)
    {
        var dateText = new TextBlock
        {
            Text = selectedDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB")),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Calendar,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            FontSize = 20,
            Width = 20,
            Height = 20,
            Foreground = CreateBrush("#0F766E"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        Grid.SetColumn(dateText, 1);
        content.Children.Add(dateText);
        var chevron = new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.ChevronDown,
            IconSize = IconSize.Size12,
            IconVariant = IconVariant.Regular,
            FontSize = 12,
            Width = 12,
            Height = 12,
            Foreground = CreateBrush("#68788D"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };
        Grid.SetColumn(chevron, 2);
        content.Children.Add(chevron);

        var dateButton = new Button
        {
            Content = content,
            Width = UiTokens.DatePickerWidth,
            Height = UiTokens.DatePickerHeight,
            Padding = new Thickness(9, 0, 8, 0),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = $"Choose {label.ToLowerInvariant()}",
            Cursor = Cursors.Hand
        };
        var calendar = new System.Windows.Controls.Calendar
        {
            DisplayDate = selectedDate,
            SelectedDate = selectedDate,
            SelectionMode = CalendarSelectionMode.SingleDate,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
            Style = CreateBookingQueueCalendarStyle()
        };
        var popup = new Popup
        {
            PlacementTarget = dateButton,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
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
            calendar.DisplayDate = selectedDate;
            calendar.SelectedDate = selectedDate;
            popup.IsOpen = true;
        };
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (calendar.SelectedDate is not DateTime nextDate) return;
            selectedDate = nextDate.Date;
            dateText.Text = selectedDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
            onSelected(selectedDate);
            popup.IsOpen = false;
        };

        var field = new StackPanel { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        field.Children.Add(Label(label, 12, CreateBrush("#68788D"), FontWeights.SemiBold));
        field.Children.Add(dateButton);
        return field;
    }

    private List<BookingDashboardSupplierChoice> GetBookingDashboardSupplierChoices(
        IEnumerable<BookingQueueRecord> records, string selectedSupplierId)
    {
        var choices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var supplier in _purchasingSuppliers)
        {
            var id = ReadProperty(supplier, "id", "supplierId", "supplier_id");
            if (id.Length == 0) continue;
            var code = ReadProperty(supplier, "code", "supplierCode", "supplier_code");
            var name = ReadProperty(supplier, "name", "supplierName", "supplier_name");
            choices[id] = code.Length > 0 && name.Length > 0 ? $"{code} · {name}" : FirstNonEmpty(name, code, id);
        }
        foreach (var booking in records)
        {
            if (booking.SupplierId.Length == 0 || choices.ContainsKey(booking.SupplierId)) continue;
            choices[booking.SupplierId] = booking.SupplierCode.Length > 0 && booking.SupplierName.Length > 0
                ? $"{booking.SupplierCode} · {booking.SupplierName}"
                : FirstNonEmpty(booking.SupplierName, booking.SupplierCode, booking.SupplierId);
        }
        if (selectedSupplierId.Length > 0 && !choices.ContainsKey(selectedSupplierId))
            choices[selectedSupplierId] = selectedSupplierId;

        return new[] { new BookingDashboardSupplierChoice("", "All suppliers") }
            .Concat(choices.OrderBy(item => item.Value, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new BookingDashboardSupplierChoice(item.Key, item.Value)))
            .ToList();
    }

    private List<BookingDashboardRubberTypeChoice> GetBookingDashboardRubberTypeChoices(
        IEnumerable<BookingQueueRecord> records, string category, string selectedRubberTypeId)
    {
        var choices = new Dictionary<string, BookingDashboardRubberTypeChoice>(StringComparer.OrdinalIgnoreCase);
        foreach (var rubberType in _purchasingRubberTypes)
        {
            if (GetCategoryForRubberType(rubberType) != category) continue;
            var id = ReadProperty(rubberType, "id", "rubberTypeId", "rubber_type_id");
            var code = ReadProperty(rubberType, "code", "rubberTypeCode", "rubber_type_code");
            var name = ReadProperty(rubberType, "name", "rubberTypeName", "rubber_type_name");
            var key = FirstNonEmpty(id, code, name);
            if (key.Length == 0) continue;
            choices[key] = new BookingDashboardRubberTypeChoice(key, code, name,
                FirstNonEmpty(name, code, key));
        }

        foreach (var booking in records)
        foreach (var rawType in GetDashboardRubberTypeValues(booking))
        {
            var match = _purchasingRubberTypes.FirstOrDefault(item =>
                string.Equals(ReadProperty(item, "id", "rubberTypeId", "rubber_type_id"), rawType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "code", "rubberTypeCode", "rubber_type_code"), rawType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "name", "rubberTypeName", "rubber_type_name"), rawType, StringComparison.OrdinalIgnoreCase));
            if (match.ValueKind == JsonValueKind.Object && GetCategoryForRubberType(match) != category) continue;
            var id = ReadProperty(match, "id", "rubberTypeId", "rubber_type_id");
            var code = ReadProperty(match, "code", "rubberTypeCode", "rubber_type_code");
            var name = FirstNonEmpty(ReadProperty(match, "name", "rubberTypeName", "rubber_type_name"),
                ResolveBookingRubberTypeName(rawType, ""));
            var key = FirstNonEmpty(id, code, rawType);
            if (key.Length == 0 || choices.ContainsKey(key)) continue;
            choices[key] = new BookingDashboardRubberTypeChoice(key, code, name,
                FirstNonEmpty(name, code, key));
        }

        if (selectedRubberTypeId.Length > 0 && !choices.ContainsKey(selectedRubberTypeId))
            choices[selectedRubberTypeId] = new BookingDashboardRubberTypeChoice(
                selectedRubberTypeId, "", selectedRubberTypeId, selectedRubberTypeId);

        return new[] { new BookingDashboardRubberTypeChoice("", "", "", "All rubber types") }
            .Concat(choices.Values.OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase))
            .ToList();
    }

    private static ComboBoxItem CreateDashboardChoiceItem(BookingDashboardSupplierChoice choice) => new()
    {
        Content = choice.Label,
        Tag = choice.Id
    };

    private static ComboBoxItem CreateDashboardChoiceItem(BookingDashboardRubberTypeChoice choice) => new()
    {
        Content = choice.Label,
        Tag = choice.Id
    };

    private static IEnumerable<string> GetDashboardRubberTypeValues(BookingQueueRecord booking)
    {
        if (!string.IsNullOrWhiteSpace(booking.RubberType)) yield return booking.RubberType;
        if (!string.IsNullOrWhiteSpace(booking.RubberTypeName)) yield return booking.RubberTypeName;
        foreach (var lot in GetDashboardLots(booking))
        {
            var value = ReadProperty(lot, "rubberType", "rubber_type");
            if (value.Length > 0) yield return value;
        }
    }

    private bool BookingMatchesDashboardRubberType(BookingQueueRecord booking, string selectedId)
    {
        if (selectedId.Length == 0) return true;
        var selected = _purchasingRubberTypes.FirstOrDefault(item =>
            string.Equals(ReadProperty(item, "id", "rubberTypeId", "rubber_type_id"), selectedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReadProperty(item, "code", "rubberTypeCode", "rubber_type_code"), selectedId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ReadProperty(item, "name", "rubberTypeName", "rubber_type_name"), selectedId, StringComparison.OrdinalIgnoreCase));
        var selectedCode = ReadProperty(selected, "code", "rubberTypeCode", "rubber_type_code");
        var selectedName = ReadProperty(selected, "name", "rubberTypeName", "rubber_type_name");
        foreach (var rawType in GetDashboardRubberTypeValues(booking))
        {
            if (string.Equals(rawType, selectedId, StringComparison.OrdinalIgnoreCase) ||
                (selectedCode.Length > 0 && string.Equals(rawType, selectedCode, StringComparison.OrdinalIgnoreCase)) ||
                (selectedName.Length > 0 && string.Equals(rawType, selectedName, StringComparison.OrdinalIgnoreCase)))
                return true;
            var rawTypeRecord = _purchasingRubberTypes.FirstOrDefault(item =>
                string.Equals(ReadProperty(item, "id", "rubberTypeId", "rubber_type_id"), rawType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "code", "rubberTypeCode", "rubber_type_code"), rawType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ReadProperty(item, "name", "rubberTypeName", "rubber_type_name"), rawType, StringComparison.OrdinalIgnoreCase));
            if (rawTypeRecord.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadProperty(rawTypeRecord, "id", "rubberTypeId", "rubber_type_id"), selectedId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return selectedName.Length > 0 && GetDashboardRubberTypeValues(booking)
            .Any(value => string.Equals(ResolveBookingRubberTypeName(value, ""), selectedName, StringComparison.OrdinalIgnoreCase));
    }

    private Border DashboardMetric(string title, string value, string detail, string accent)
    {
        var content = new StackPanel();
        content.Children.Add(Label(title, 13, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold));
        content.Children.Add(Label(value, 24, (Brush)FindResource("TextBrush"), FontWeights.Bold));
        content.Children.Add(Label(detail, 12, (Brush)FindResource("MutedBrush")));
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(11, 8, 9, 8),
            Margin = new Thickness(0, 0, 7, 0),
            Child = new Border
            {
                BorderBrush = CreateBrush(accent),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Padding = new Thickness(8, 0, 0, 0),
                Child = content
            }
        };
    }

    private Border DashboardChartCard(string title, UIElement content)
    {
        var panel = new StackPanel();
        panel.Children.Add(Label(title, 15, (Brush)FindResource("TextBrush"), FontWeights.Bold));
        panel.Children.Add(content);
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(11, 8, 11, 9),
            Margin = new Thickness(0, 0, 7, 0),
            Child = panel
        };
    }

    private UIElement BuildStatusChart(IReadOnlyList<BookingQueueRecord> records)
    {
        var chart = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var groups = records.GroupBy(item => item.Status)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .ToList();
        if (groups.Count == 0)
        {
            chart.Children.Add(Label("No booking data for this period.", 13, (Brush)FindResource("MutedBrush")));
            return chart;
        }

        var maxCount = Math.Max(1, groups.Max(group => group.Count()));
        foreach (var group in groups)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.Children.Add(Label(group.First().StatusLabel, 12, (Brush)FindResource("TextBrush")));
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = maxCount,
                Value = group.Count(),
                Height = 9,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = StatusBrush(group.Key),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = $"{group.Count()} booking(s)"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var count = Label(group.Count().ToString("N0"), 12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(count, 2);
            row.Children.Add(count);
            chart.Children.Add(row);
        }
        return chart;
    }

    private UIElement BuildSlotChart(IReadOnlyList<BookingQueueRecord> activeRecords, bool showDailyCapacity)
    {
        var chart = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var maxPeriodCount = Math.Max(1, _bookingQueueSlotRules
            .Select(rule => activeRecords.Count(item => item.Slot == rule.Slot))
            .DefaultIfEmpty(0)
            .Max());
        foreach (var rule in _bookingQueueSlotRules)
        {
            var count = activeRecords.Count(item => item.Slot == rule.Slot);
            var isClosed = showDailyCapacity && !rule.IsOpen;
            var capacity = showDailyCapacity ? rule.Limit : null;
            var max = capacity is int dailyCapacity && dailyCapacity > 0 ? dailyCapacity : maxPeriodCount;
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            row.Children.Add(Label(rule.Slot, 12, (Brush)FindResource("TextBrush")));
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = max,
                Value = count,
                Height = 9,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isClosed ? CreateBrush("#94A3B8")
                    : capacity is not null && count >= max ? CreateBrush("#D9534F") : CreateBrush("#0F766E"),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = isClosed ? "This slot is closed"
                    : capacity is int limit ? $"{count} of {limit} active bookings"
                    : $"{count} booking(s) in the selected period"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var countLabel = Label(isClosed ? "Closed" : capacity is int limitText ? $"{count} / {limitText}" : count.ToString("N0"), 12,
                (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
            countLabel.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(countLabel, 2);
            row.Children.Add(countLabel);
            chart.Children.Add(row);
        }
        if (_bookingQueueSlotRules.Count == 0)
            chart.Children.Add(Label("Slot configuration is not available.", 12, (Brush)FindResource("MutedBrush")));
        return chart;
    }

    private UIElement BuildRubberTypeChart(IReadOnlyList<JsonElement> lots)
    {
        var chart = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var groups = lots.GroupBy(lot =>
                $"{ResolveBookingRubberTypeName(ReadProperty(lot, "rubberType", "rubber_type"), "")} · {ProvinceDisplayName(FirstNonEmpty(ReadProperty(lot, "rubberSource", "rubber_source"), "Source not set"))}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Name = group.Key,
                Count = group.Count(),
                Received = group.Count(IsLotReceived),
                ExpectedWeight = group.Sum(lot => ReadDashboardWeight(lot, "expectedWeight", "expected_weight")),
                ReceivedWeight = group.Sum(lot => ReadDashboardWeight(lot, "receivedWeight", "received_weight"))
            })
            .OrderByDescending(item => item.ExpectedWeight)
            .ThenBy(item => item.Name)
            .Take(8)
            .ToList();
        if (groups.Count == 0)
        {
            chart.Children.Add(Label("No Lot records for this period.", 13, (Brush)FindResource("MutedBrush")));
            return chart;
        }

        var maxWeight = Math.Max(1m, groups.Max(item => item.ExpectedWeight));
        foreach (var group in groups)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            var name = Label(group.Name, 12, (Brush)FindResource("TextBrush"));
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = group.Name;
            row.Children.Add(name);
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = (double)maxWeight,
                Value = (double)group.ExpectedWeight,
                Height = 9,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = CreateBrush("#2563EB"),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = $"Expected {FormatDashboardWeight(group.ExpectedWeight)} · received {FormatDashboardWeight(group.ReceivedWeight)}"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var detail = Label($"{group.Received}/{group.Count} lots · {FormatDashboardWeight(group.ExpectedWeight)}", 11,
                (Brush)FindResource("MutedBrush"));
            detail.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(detail, 2);
            row.Children.Add(detail);
            chart.Children.Add(row);
        }
        return chart;
    }

    private static void AddDashboardColumns(DataGrid grid)
    {
        var dashboardHeaderStyle = new Style(typeof(DataGridColumnHeader),
            (Style)grid.FindResource(typeof(DataGridColumnHeader)));
        dashboardHeaderStyle.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        var centeredHeaderStyle = new Style(typeof(DataGridColumnHeader), dashboardHeaderStyle);
        centeredHeaderStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        centeredHeaderStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));

        DataGridTextColumn Text(string header, string path, double width, bool wrap = false, bool center = false)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star),
                HeaderStyle = center ? centeredHeaderStyle : dashboardHeaderStyle
            };
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(3, 2, 3, 2)));
            if (wrap)
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
            if (center)
            {
                style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            }
            column.ElementStyle = style;
            return column;
        }

        grid.Columns.Add(Text("Queue", nameof(BookingDashboardRow.QueueDisplay), .55, center: true));
        grid.Columns.Add(Text("Date", nameof(BookingDashboardRow.DateDisplay), .75));
        grid.Columns.Add(Text("Time slot", nameof(BookingDashboardRow.Slot), .72));
        grid.Columns.Add(Text("Code", nameof(BookingDashboardRow.SupplierCode), .5, center: true));
        grid.Columns.Add(Text("Name", nameof(BookingDashboardRow.SupplierName), .9));
        grid.Columns.Add(Text("Truck", nameof(BookingDashboardRow.Truck), .75));
        grid.Columns.Add(Text("Truck type", nameof(BookingDashboardRow.TruckType), .7));
        grid.Columns.Add(Text("Weight", nameof(BookingDashboardRow.WeightDisplay), .68));
        grid.Columns.Add(Text("Status", nameof(BookingDashboardRow.Status), .7));
        grid.Columns.Add(Text("Note", nameof(BookingDashboardRow.Note), 1.0, true));
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Ticket",
            HeaderStyle = centeredHeaderStyle,
            CellTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                              xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                    <Button Content="Ticket" Tag="OpenTicket" Padding="8,3"
                            HorizontalAlignment="Center" VerticalAlignment="Center"
                            ToolTip="Open booking ticket" />
                </DataTemplate>
                """),
            Width = new DataGridLength(.65, DataGridLengthUnitType.Star)
        });
    }

    private void BookingDashboardTicket_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button { Tag: "OpenTicket", DataContext: BookingDashboardRow row })
            OpenBookingTicket(row.Booking);
    }

    private static IEnumerable<JsonElement> GetDashboardLots(BookingQueueRecord record)
    {
        if (record.Raw.ValueKind != JsonValueKind.Object) return [];
        foreach (var property in record.Raw.EnumerateObject())
        {
            if (!property.Name.Equals("lots", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.Array)
                continue;
            return property.Value.EnumerateArray().ToArray();
        }
        return [];
    }

    private string FormatDashboardBookingWeight(BookingQueueRecord record)
    {
        var lots = GetDashboardLots(record).ToList();
        var weight = lots.Count > 0
            ? lots.Sum(lot => ReadDashboardWeight(lot, "expectedWeight", "expected_weight"))
            : record.EstimatedWeightKg ?? 0m;
        return FormatDashboardWeight(weight);
    }

    private string ProvinceDisplayName(string value)
    {
        var source = value.Trim();
        if (source.Length == 0) return source;
        const string provinceSuffix = " Province";
        if (source.EndsWith(provinceSuffix, StringComparison.OrdinalIgnoreCase))
            source = source[..^provinceSuffix.Length].Trim();
        source = source.Replace(", Thailand", "", StringComparison.OrdinalIgnoreCase).Trim();
        return _bookingProvinceNames.TryGetValue(ProvinceLookupKey(source), out var thaiName) ? thaiName : source;
    }

    private static string ProvinceLookupKey(string value)
        => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static string FormatDashboardLab(JsonElement booking)
    {
        var lines = new List<string>();
        foreach (var kind in new[] { "main", "trailer" })
        {
            var sampleCountText = ReadDashboardNestedProperty(booking, "labSummary", kind, "sampleCount");
            if (!int.TryParse(sampleCountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sampleCount) || sampleCount == 0)
                continue;
            var metrics = new[]
            {
                FormatDashboardLabMetric("DRC", ReadDashboardNestedProperty(booking, "labSummary", kind, "averageDrc")),
                FormatDashboardLabMetric("Moisture", ReadDashboardNestedProperty(booking, "labSummary", kind, "averageMoisture")),
                FormatDashboardLabMetric("CP", ReadDashboardNestedProperty(booking, "labSummary", kind, "averageCp")),
                FormatDashboardLabMetric("PRI", ReadDashboardNestedProperty(booking, "labSummary", kind, "averagePri"))
            }.Where(value => value.Length > 0);
            lines.Add($"{kind.ToUpperInvariant()}: {sampleCount} sample(s)" +
                (metrics.Any() ? $" · {string.Join(" · ", metrics)}" : ""));
        }
        if (lines.Count > 0) return string.Join(Environment.NewLine, lines);

        var samples = GetDashboardSamples(booking);
        foreach (var group in samples.GroupBy(sample =>
                     ReadProperty(sample, "isTrailer", "is_trailer").Equals("true", StringComparison.OrdinalIgnoreCase)))
        {
            var metrics = new[]
            {
                AverageDashboardSampleMetric("DRC", group, "drc"),
                AverageDashboardSampleMetric("Moisture", group, "moistureFactor", "moisture_factor"),
                AverageDashboardSampleMetric("CP", group, "percentCp", "percent_cp"),
                AverageDashboardSampleMetric("PRI", group, "pri")
            }.Where(value => value.Length > 0);
            var kind = group.Key ? "TRAILER" : "MAIN";
            lines.Add($"{kind}: {group.Count()} sample(s)" +
                (metrics.Any() ? $" · {string.Join(" · ", metrics)}" : ""));
        }
        return lines.Count > 0 ? string.Join(Environment.NewLine, lines) : "No lab samples";
    }

    private static int ReadDashboardLabSampleCount(JsonElement booking)
    {
        var count = 0;
        foreach (var kind in new[] { "main", "trailer" })
            if (int.TryParse(ReadDashboardNestedProperty(booking, "labSummary", kind, "sampleCount"),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                count += value;
        return count > 0 ? count : GetDashboardSamples(booking).Count;
    }

    private static List<JsonElement> GetDashboardSamples(JsonElement booking)
    {
        if (booking.ValueKind != JsonValueKind.Object) return [];
        foreach (var property in booking.EnumerateObject())
            if (property.Name.Equals("labSamples", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.Array)
                return property.Value.EnumerateArray().Select(item => item.Clone()).ToList();
        return [];
    }

    private static string AverageDashboardSampleMetric(string label, IEnumerable<JsonElement> samples, params string[] propertyNames)
    {
        var values = samples
            .Select(sample => ReadProperty(sample, propertyNames))
            .Where(value => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(value => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToList();
        return values.Count == 0 ? "" : $"{label} {values.Average().ToString("N1", CultureInfo.CurrentCulture)}";
    }

    private static string ReadDashboardNestedProperty(JsonElement element, string parent, string child, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return "";
        foreach (var parentProperty in element.EnumerateObject())
        {
            if (!parentProperty.Name.Equals(parent, StringComparison.OrdinalIgnoreCase) || parentProperty.Value.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var childProperty in parentProperty.Value.EnumerateObject())
            {
                if (!childProperty.Name.Equals(child, StringComparison.OrdinalIgnoreCase) || childProperty.Value.ValueKind != JsonValueKind.Object)
                    continue;
                return ReadProperty(childProperty.Value, propertyName);
            }
        }
        return "";
    }

    private static string FormatDashboardLabMetric(string label, string raw)
        => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? $"{label} {value.ToString("N1", CultureInfo.CurrentCulture)}" : "";

    private static bool IsLotReceived(JsonElement lot)
    {
        var receiptStatus = ReadProperty(lot, "receiptStatus", "receipt_status");
        if (receiptStatus.Length > 0)
            return receiptStatus.Equals("RECEIVED", StringComparison.OrdinalIgnoreCase);
        return ReadProperty(lot, "receivedAt", "received_at").Length > 0 ||
               ReadDashboardWeight(lot, "receivedWeight", "received_weight") > 0;
    }

    private static decimal ReadDashboardWeight(JsonElement lot, params string[] names)
        => decimal.TryParse(ReadProperty(lot, names), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value : 0m;

    private static string FormatDashboardWeight(decimal kilograms)
        => $"{(kilograms / 1000m).ToString("N1", CultureInfo.CurrentCulture)} t";

    private static Brush StatusBrush(string status) => CreateBrush(status switch
    {
        "PENDING" => "#D28A18",
        "APPROVED" => "#2563EB",
        "CHECKED_IN" => "#16804A",
        "DRAINING" => "#7042A5",
        "COMPLETED" => "#64748B",
        "CANCELLED" => "#D9534F",
        _ => "#0F766E"
    });
}

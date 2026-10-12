using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const string CuplumpReceivingPageId = "RRC:Cuplump:Receiving";
    private DateTime _receivingDate = DateTime.Today;
    private DateTime _receivingLoadedDate = DateTime.MinValue;
    private List<ReceivingLotRow> _receivingLots = [];
    private string _receivingMessage = "";
    private bool _receivingLoading;
    private int _receivingLoadVersion;

    private string _receivingSearch = "";

    private sealed record ReceivingLotRow(
        string BookingId,
        string BookingCode,
        string QueueNo,
        string SupplierCode,
        string SupplierName,
        string LotId,
        string Kind,
        string RubberType,
        string LotNo,
        string ReceivedWeight,
        string ReceiptStatus,
        bool IsTruckScaleComplete,
        string TruckScaleStatus,
        bool IsTrailer,
        string Moisture);

    private UIElement BuildReceivingPage()
    {
        var page = PageStack();

        var datePicker = new DatePicker
        {
            SelectedDate = _receivingDate,
            Width = UiTokens.DatePickerWidth,
            Height = UiTokens.DatePickerHeight,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        var datePickerChrome = new Grid
        {
            Width = UiTokens.DatePickerWidth,
            Height = UiTokens.DatePickerHeight,
            Margin = new Thickness(0)
        };
        datePickerChrome.Children.Add(datePicker);
        datePickerChrome.Children.Add(new Border
        {
            CornerRadius = (CornerRadius)FindResource("AppDatePickerCornerRadius"),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false
        });
        datePicker.SelectedDateChanged += (_, _) =>
        {
            if (datePicker.SelectedDate is not DateTime selectedDate) return;
            _receivingDate = selectedDate.Date;
            _receivingLoadedDate = DateTime.MinValue;
            _receivingMessage = "";
            _ = LoadReceivingAsync();
        };

        var refresh = PurchasingButton("Refresh");
        refresh.Height = 34;
        refresh.Margin = new Thickness(8, 0, 0, 0);
        refresh.VerticalAlignment = VerticalAlignment.Center;
        refresh.Click += async (_, _) => await LoadReceivingAsync(force: true);

        var searchInput = new TextBox
        {
            Text = _receivingSearch,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 8, 2),
            ToolTip = "Search supplier, booking, queue, or lot number"
        };
        var searchPlaceholder = new TextBlock
        {
            Text = "Search...",
            FontSize = 13,
            Foreground = CreateBrush("#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(_receivingSearch) ? Visibility.Visible : Visibility.Collapsed
        };
        searchInput.TextChanged += (_, _) =>
        {
            _receivingSearch = searchInput.Text;
            searchPlaceholder.Visibility = string.IsNullOrEmpty(searchInput.Text) ? Visibility.Visible : Visibility.Collapsed;
            RenderCurrentPage();
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
        inputHost.Children.Add(searchInput);
        Grid.SetColumn(inputHost, 1);
        searchGrid.Children.Add(inputHost);

        var searchBoxContainer = new Border
        {
            Width = 200,
            Height = 34,
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = searchGrid
        };

        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headerActions.Children.Add(searchBoxContainer);
        headerActions.Children.Add(datePickerChrome);
        headerActions.Children.Add(refresh);

        var heroHeader = CreatePageHeroHeader(
            "RAW RUBBER INTAKE & RECEIVING",
            "Cuplump Receiving",
            "Receive lots after TruckScale and record lab samples against the booking.",
            FluentIcons.Common.Icon.ArrowDownload,
            headerActions);
        page.Children.Add(heroHeader);
        page.Children.Add(Banner(
            "Continue from TruckScale",
            "Only completed Cuplump weigh-outs can be received. Main and trailer rubber are separate lots; each lot is recorded once, and lab samples are stored against its booking.",
            "#2F6FAE"));

        if (_receivingLoading)
            page.Children.Add(Banner("Loading bookings", "Retrieving booking lots from the server…", "#0F766E"));

        if (!string.IsNullOrWhiteSpace(_receivingMessage))
            page.Children.Add(Banner("Receiving records", _receivingMessage, "#C2413B"));

        var displayLots = string.IsNullOrWhiteSpace(_receivingSearch)
            ? _receivingLots
            : _receivingLots.Where(item =>
                item.BookingCode.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase) ||
                item.QueueNo.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase) ||
                item.SupplierCode.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase) ||
                item.SupplierName.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase) ||
                item.LotNo.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase) ||
                item.RubberType.Contains(_receivingSearch, StringComparison.OrdinalIgnoreCase)).ToList();

        if (_receivingLoading && _receivingLots.Count == 0)
        {
            page.Children.Add(EmptyState("Loading bookings", "Getting booking lot records from the ERP API."));
        }
        else if (displayLots.Count == 0)
        {
            page.Children.Add(EmptyState(
                _receivingLots.Count == 0 ? "No booking lots for this date" : "No matching booking lots",
                _receivingLots.Count == 0 ? "Choose another date or refresh the list." : "Try adjusting your search criteria."));
        }
        else
        {
            page.Children.Add(SectionHeading($"Booking lots · {displayLots.Count} item(s)"));
            page.Children.Add(BuildReceivingLotTable(displayLots));
        }

        return page;
    }

    private UIElement BuildReceivingLotTable(IReadOnlyList<ReceivingLotRow>? lots = null)
    {
        var activeLots = lots ?? _receivingLots;
        var table = new Grid { MinWidth = 1370, Background = Brushes.White };
        var widths = new[] { 155d, 85d, 170d, 105d, 155d, 150d, 125d, 130d, 135d, 230d };
        foreach (var width in widths)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headers = new[] { "Booking", "Code", "Name", "Lot item", "Rubber type", "Lot number", "Received kg", "TruckScale", "Status", "Action" };
        for (var column = 0; column < headers.Length; column++)
        {
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
                BorderBrush = (Brush)FindResource("BorderBrushSoft"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 11, 8, 11),
                Child = Label(headers[column], 12, (Brush)FindResource("TextBrush"), FontWeights.SemiBold)
            };
            Grid.SetColumn(header, column);
            Grid.SetRow(header, 0);
            table.Children.Add(header);
        }

        for (var index = 0; index < activeLots.Count; index++)
        {
            var item = activeLots[index];
            var row = index + 1;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var values = new[]
            {
                $"{item.BookingCode}\nQueue {item.QueueNo}",
                string.IsNullOrWhiteSpace(item.SupplierCode) ? "—" : item.SupplierCode,
                string.IsNullOrWhiteSpace(item.SupplierName) ? "—" : item.SupplierName,
                item.Kind,
                item.RubberType,
                string.IsNullOrWhiteSpace(item.LotNo) ? "—" : item.LotNo,
                FormatReceivingWeight(item.ReceivedWeight),
                item.TruckScaleStatus,
                item.ReceiptStatus == "RECEIVED" ? "Received in full" : "Pending"
            };
            for (var column = 0; column < values.Length; column++)
            {
                var foreground = column == 8 && item.ReceiptStatus == "RECEIVED"
                    ? new SolidColorBrush(Color.FromRgb(22, 128, 74))
                    : (Brush)FindResource("TextBrush");
                var cell = new Border
                {
                    Background = index % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(250, 252, 254)),
                    BorderBrush = (Brush)FindResource("BorderBrushSoft"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 9, 8, 9),
                    Child = Label(values[column], 12, foreground,
                        column == 0 || column is 7 or 8 ? FontWeights.SemiBold : FontWeights.Normal)
                };
                Grid.SetColumn(cell, column);
                Grid.SetRow(cell, row);
                table.Children.Add(cell);
            }

            var actionCell = new Border
            {
                Background = index % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(250, 252, 254)),
                BorderBrush = (Brush)FindResource("BorderBrushSoft"),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(8, 6, 8, 6),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var receive = new Button
            {
                Content = item.ReceiptStatus == "RECEIVED" ? "Received" : "Receive lot",
                MinWidth = 102,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 0),
                IsEnabled = item.ReceiptStatus != "RECEIVED" && item.IsTruckScaleComplete && !_session.IsPreview,
                Style = (Style)FindResource(typeof(Button))
            };
            receive.Click += async (_, _) => await OpenLotReceiptAsync(item);
            actions.Children.Add(receive);
            var samples = new Button
            {
                Content = "Lab samples",
                MinWidth = 100,
                Height = 32,
                IsEnabled = item.IsTruckScaleComplete && !_session.IsPreview,
                Style = (Style)FindResource(typeof(Button))
            };
            samples.Click += async (_, _) => await OpenLotSamplesAsync(item);
            actions.Children.Add(samples);
            actionCell.Child = actions;
            Grid.SetColumn(actionCell, 9);
            Grid.SetRow(actionCell, row);
            table.Children.Add(actionCell);
        }

        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(1),
            Child = table
        };
        return new ScrollViewer
        {
            Content = border,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
    }

    private async Task OpenLotReceiptAsync(ReceivingLotRow item)
    {
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Sign in with a receiving account to record a lot receipt.", "Sign in required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var fields = new List<PurchasingField>
        {
            new("lotNo", "Lot number", InitialValue: item.LotNo == "—" ? "" : item.LotNo, Required: true),
            new("receivedWeight", "Full lot weight (kg)", PurchasingFieldKind.Number, Required: true)
        };
        var values = ShowPurchasingEditor($"Receive {item.Kind} · {item.BookingCode}", fields);
        if (values is null) return;
        if (values.GetValueOrDefault("receivedWeight") is not decimal weight || weight <= 0)
        {
            MessageBox.Show(this, "Enter a full lot weight greater than zero.", "Invalid weight",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = await _apiClient.PostJsonAsync(
            $"/api/bookings/{Uri.EscapeDataString(item.BookingId)}/lots/{Uri.EscapeDataString(item.LotId)}/receive",
            new { lotNo = values.GetValueOrDefault("lotNo")?.ToString()?.Trim(), receivedWeight = weight },
            _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Lot receipt failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var lotNo = values.GetValueOrDefault("lotNo")?.ToString()?.Trim() ?? "";
        _receivingLoadedDate = DateTime.MinValue;
        await LoadReceivingAsync(force: true);
        _receivingMessage = $"Lot {lotNo} received in full.";
        if (_currentPage == DepartmentId.Receiving.ToString() || _currentPage == CuplumpReceivingPageId) RenderCurrentPage();
    }

    private async Task LoadReceivingAsync(bool force = false)
    {
        if (_session.IsPreview)
        {
            _receivingLots = [];
            _receivingMessage = "Sign in to load and record live booking receipts.";
            _receivingLoadedDate = _receivingDate.Date;
            _receivingLoading = false;
            RenderCurrentPage();
            return;
        }

        if (!force && _receivingLoadedDate.Date == _receivingDate.Date) return;
        var version = ++_receivingLoadVersion;
        var date = _receivingDate.Date;
        _receivingLoading = true;
        _receivingMessage = "";
        RenderCurrentPage();

        var apiDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var result = await _apiClient.GetAsync($"/api/bookings?date={apiDate}", _session.Token);
        if (version != _receivingLoadVersion) return;

        _receivingLoading = false;
        _receivingLots = [];
        if (!result.Success || result.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            _receivingMessage = result.Error.Length > 0 ? result.Error : "Could not load booking lots.";
        }
        else
        {
            foreach (var booking in ExtractArray(result.Data))
            {
                var bookingCode = ReadProperty(booking, "bookingCode", "booking_code");
                var bookingStatus = ReadProperty(booking, "status");
                if (!bookingCode.StartsWith("C", StringComparison.OrdinalIgnoreCase) ||
                    bookingStatus.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(ReadProperty(booking, "deletedAt", "deleted_at")))
                    continue;
                var mainWeightOut = ParseReceivingNumber(ReadProperty(booking, "weightOut", "weight_out"));
                var trailerWeightOut = ParseReceivingNumber(ReadProperty(booking, "trailerWeightOut", "trailer_weight_out"));
                var isTruckScaleComplete = mainWeightOut + trailerWeightOut > 0 ||
                                           bookingStatus.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase);
                var moisture = ReadProperty(booking, "moisture");
                if (!TryGetProperty(booking, "lots", out var lots) || lots.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var lot in lots.EnumerateArray())
                {
                    var lotId = ReadProperty(lot, "id");
                    if (lotId.Length == 0) continue;
                    var kind = ReadProperty(lot, "kind");
                    var receivedAt = ReadProperty(lot, "receivedAt", "received_at");
                    var receiptStatus = ReadProperty(lot, "receiptStatus", "receipt_status");
                    var isReceived = string.Equals(receiptStatus, "RECEIVED", StringComparison.OrdinalIgnoreCase) ||
                                     !string.IsNullOrWhiteSpace(receivedAt);
                    var isTrailer = kind.Equals("TRAILER", StringComparison.OrdinalIgnoreCase);
                    var queueNo = ReadProperty(booking, "queueNo", "queue_no");
                    _receivingLots.Add(new ReceivingLotRow(
                        ReadProperty(booking, "id"),
                        bookingCode,
                        $"C{queueNo}",
                        ReadProperty(booking, "supplierCode", "supplier_code"),
                        ReadProperty(booking, "supplierName", "supplier_name"),
                        lotId,
                        isTrailer ? "Trailer lot" : "Main lot",
                        ReadProperty(lot, "rubberType", "rubber_type"),
                        ReadProperty(lot, "lotNo", "lot_no"),
                        ReadProperty(lot, "receivedWeight", "received_weight"),
                        isReceived ? "RECEIVED" : "PENDING",
                        isTruckScaleComplete,
                        isTruckScaleComplete ? "Complete" : "Pending",
                        isTrailer,
                        isTrailer ? ReadProperty(booking, "trailerMoisture", "trailer_moisture") : moisture));
                }
            }
            if (_receivingLots.Count == 0 && result.Success)
                _receivingMessage = "No Cuplump lots were returned for this date. Check the booking lots migration and TruckScale records.";
        }

        _receivingLoadedDate = date;
        if (_currentPage == DepartmentId.Receiving.ToString() || _currentPage == CuplumpReceivingPageId) RenderCurrentPage();
    }

    private static double ParseReceivingNumber(string value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static string FormatReceivingWeight(string value) =>
        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("N2", CultureInfo.CurrentCulture)
            : "—";

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }
}

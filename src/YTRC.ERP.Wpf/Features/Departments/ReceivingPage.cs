using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private DateTime _receivingDate = DateTime.Today;
    private DateTime _receivingLoadedDate = DateTime.MinValue;
    private List<ReceivingLotRow> _receivingLots = [];
    private string _receivingMessage = "";
    private bool _receivingLoading;
    private int _receivingLoadVersion;

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
        string ReceiptStatus);

    private UIElement BuildReceivingPage()
    {
        var page = PageStack();
        page.Children.Add(TitleBlock("Receiving", "Receive each booking lot as one complete quantity."));
        page.Children.Add(Banner(
            "One booking can contain two lots",
            "The main and trailer rubber lots appear as separate rows. Record the full weight once for each lot; a received lot is locked against a second receipt.",
            "#2F6FAE"));

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        controls.Children.Add(FieldCompact("Booking date", new DatePicker
        {
            SelectedDate = _receivingDate,
            Width = 175,
            Height = 34,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        }));
        var datePicker = (DatePicker)((StackPanel)controls.Children[0]).Children[1];
        datePicker.SelectedDateChanged += (_, _) =>
        {
            if (datePicker.SelectedDate is not DateTime selectedDate) return;
            _receivingDate = selectedDate.Date;
            _receivingLoadedDate = DateTime.MinValue;
            _receivingMessage = "";
            _ = LoadReceivingAsync();
        };

        var refresh = new Button
        {
            Content = "Refresh",
            MinWidth = 94,
            Height = 34,
            Margin = new Thickness(4, 18, 0, 0),
            Style = (Style)FindResource(typeof(Button))
        };
        refresh.Click += async (_, _) => await LoadReceivingAsync(force: true);
        controls.Children.Add(refresh);
        if (_receivingLoading)
            controls.Children.Add(Label("Loading booking lots…", 12, (Brush)FindResource("MutedBrush")));
        page.Children.Add(controls);

        if (!string.IsNullOrWhiteSpace(_receivingMessage))
            page.Children.Add(Banner("Receiving records", _receivingMessage, "#C2413B"));

        if (_receivingLoading && _receivingLots.Count == 0)
        {
            page.Children.Add(EmptyState("Loading bookings", "Getting booking lot records from the ERP API."));
        }
        else if (_receivingLots.Count == 0)
        {
            page.Children.Add(EmptyState("No booking lots for this date", "Choose another date or refresh the list."));
        }
        else
        {
            page.Children.Add(SectionHeading($"Booking lots · {_receivingLots.Count} item(s)"));
            page.Children.Add(BuildReceivingLotTable());
        }

        return page;
    }

    private UIElement BuildReceivingLotTable()
    {
        var table = new Grid { MinWidth = 1370, Background = Brushes.White };
        var widths = new[] { 160d, 90d, 190d, 105d, 175d, 165d, 145d, 135d, 205d };
        foreach (var width in widths)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headers = new[] { "Booking", "Code", "Name", "Lot item", "Rubber type", "Lot number", "Received kg", "Status", "Action" };
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

        for (var index = 0; index < _receivingLots.Count; index++)
        {
            var item = _receivingLots[index];
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
                string.IsNullOrWhiteSpace(item.ReceivedWeight) ? "—" : item.ReceivedWeight,
                item.ReceiptStatus == "RECEIVED" ? "Received in full" : "Pending"
            };
            for (var column = 0; column < values.Length; column++)
            {
                var foreground = column == 7 && item.ReceiptStatus == "RECEIVED"
                    ? new SolidColorBrush(Color.FromRgb(22, 128, 74))
                    : (Brush)FindResource("TextBrush");
                var cell = new Border
                {
                    Background = index % 2 == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(250, 252, 254)),
                    BorderBrush = (Brush)FindResource("BorderBrushSoft"),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 9, 8, 9),
                    Child = Label(values[column], 12, foreground,
                        column == 0 || column == 7 ? FontWeights.SemiBold : FontWeights.Normal)
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
            if (item.ReceiptStatus == "RECEIVED")
            {
                actionCell.Child = Label("Completed", 12, new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontWeights.SemiBold);
            }
            else
            {
                var receive = new Button
                {
                    Content = "Receive full lot",
                    MinWidth = 130,
                    Height = 34,
                    IsEnabled = !_session.IsPreview,
                    Style = (Style)FindResource("PrimaryButton")
                };
                receive.Click += async (_, _) => await OpenLotReceiptAsync(item);
                actionCell.Child = receive;
            }
            Grid.SetColumn(actionCell, 8);
            Grid.SetRow(actionCell, row);
            table.Children.Add(actionCell);
        }

        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
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
        if (_currentPage == DepartmentId.Receiving.ToString()) RenderCurrentPage();
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
                    _receivingLots.Add(new ReceivingLotRow(
                        ReadProperty(booking, "id"),
                        ReadProperty(booking, "bookingCode", "booking_code"),
                        ReadProperty(booking, "queueNo", "queue_no"),
                        ReadProperty(booking, "supplierCode", "supplier_code"),
                        ReadProperty(booking, "supplierName", "supplier_name"),
                        lotId,
                        kind.Equals("TRAILER", StringComparison.OrdinalIgnoreCase) ? "Trailer lot" : "Main lot",
                        ReadProperty(lot, "rubberType", "rubber_type"),
                        ReadProperty(lot, "lotNo", "lot_no"),
                        ReadProperty(lot, "receivedWeight", "received_weight"),
                        isReceived ? "RECEIVED" : "PENDING"));
                }
            }
            if (_receivingLots.Count == 0)
                _receivingMessage = "The API returned bookings without lot lines. Apply the booking lots migration to enable receiving.";
        }

        _receivingLoadedDate = date;
        if (_currentPage == DepartmentId.Receiving.ToString()) RenderCurrentPage();
    }

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

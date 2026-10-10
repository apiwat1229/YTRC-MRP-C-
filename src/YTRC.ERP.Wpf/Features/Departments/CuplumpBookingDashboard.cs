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
    private sealed record CuplumpDashboardRow(BookingQueueRecord Booking, string LotsDisplay, string LabDisplay)
    {
        public int QueueNo => Booking.QueueNo;
        public string BookingCode => Booking.BookingCode;
        public string Slot => Booking.Slot;
        public string SupplierCode => Booking.SupplierCode;
        public string SupplierName => Booking.SupplierName;
        public string Truck => Booking.TruckRegisterDisplay;
        public string Status => Booking.StatusLabel;
        public string CheckIn => string.IsNullOrWhiteSpace(Booking.CheckInAt) ? "—" : Booking.CheckInAt;
        public string Note => string.IsNullOrWhiteSpace(Booking.Note) ? "—" : Booking.Note;
    }

    private UIElement BuildCuplumpBookingDashboardPage()
    {
        var records = _bookingQueueRecords
            .Where(item => item.Category == "Cuplump")
            .OrderBy(item => item.Slot, StringComparer.Ordinal)
            .ThenBy(item => item.QueueNo)
            .ToList();
        var page = new StackPanel();

        var toolbar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
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
        dateButtonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
            Margin = new Thickness(0, 0, 9, 0)
        });
        Grid.SetColumn(dateText, 1);
        dateButtonContent.Children.Add(dateText);
        var dateChevron = new FluentIcon
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
        Grid.SetColumn(dateChevron, 2);
        dateButtonContent.Children.Add(dateChevron);

        var dateButton = new Button
        {
            Content = dateButtonContent,
            Width = 178,
            Height = 34,
            Padding = new Thickness(10, 0, 9, 0),
            Margin = new Thickness(0, 0, 8, 0),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = "Choose dashboard date",
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
                CornerRadius = new CornerRadius(8),
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
            if (calendar.SelectedDate is not DateTime selectedDate || selectedDate.Date == _bookingQueueDate.Date) return;
            _bookingQueueDate = selectedDate.Date;
            dateText.Text = _bookingQueueDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
            _bookingQueueSlot = NormalizeSelectedSlot(_bookingQueueSlot, _bookingQueueDate);
            datePopup.IsOpen = false;
            _ = LoadBookingQueueAsync();
        };
        var dateField = new StackPanel { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        dateField.Children.Add(Label("Date", 11, CreateBrush("#68788D"), FontWeights.SemiBold));
        dateField.Children.Add(dateButton);
        toolbar.Children.Add(dateField);

        var refreshButton = PurchasingButton("Refresh");
        refreshButton.IsEnabled = !_bookingQueueLoading;
        refreshButton.Click += async (_, _) => await LoadBookingQueueAsync();
        toolbar.Children.Add(refreshButton);

        var toolbarRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbarRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbarRow.Children.Add(toolbar);
        var heading = SectionHeading("Cuplump Booking Dashboard");
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

        var metrics = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 12) };
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

        var charts = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        charts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        charts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        charts.Children.Add(DashboardChartCard("Booking status", BuildStatusChart(records)));
        var slotChart = BuildSlotChart(activeRecords);
        Grid.SetColumn(slotChart, 1);
        charts.Children.Add(slotChart);
        page.Children.Add(charts);

        page.Children.Add(DashboardChartCard("Rubber type and Lot receipt", BuildRubberTypeChart(allLots)));

        page.Children.Add(SectionHeading("Booking and Lot details"));
        page.Children.Add(Label("Each booking appears once. Lot details show MAIN and TRAILER lots separately, including their own Lot number and full-receipt status.",
            12, (Brush)FindResource("MutedBrush")));

        var filterRow = new WrapPanel { Margin = new Thickness(0, 8, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        var searchBox = new TextBox
        {
            Width = 330,
            Height = 32,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(9, 4, 9, 4),
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Search booking, supplier, truck, rubber type, or Lot number"
        };
        filterRow.Children.Add(searchBox);
        var statusFilter = new ComboBox
        {
            Width = 170,
            Height = 32,
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
        var resultCount = Label($"{records.Count:N0} booking(s)", 12, (Brush)FindResource("MutedBrush"));
        resultCount.VerticalAlignment = VerticalAlignment.Center;
        filterRow.Children.Add(resultCount);
        page.Children.Add(filterRow);

        var rows = records.Select(item => new CuplumpDashboardRow(
            item, FormatDashboardLots(item), FormatDashboardLab(item.Raw))).ToList();
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
            RowHeight = 54,
            FontSize = 12,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(230, 235, 241)),
            VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(238, 242, 246)),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0
        };
        AddDashboardColumns(grid);
        grid.AddHandler(Button.ClickEvent, new RoutedEventHandler(CuplumpDashboardTicket_Click));
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is CuplumpDashboardRow selected) OpenBookingTicket(selected.Booking);
        };
        _bookingQueueGrid = grid;
        page.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
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
                 row.BookingCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.SupplierCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.SupplierName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.Truck.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 row.LotsDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
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

    private Border DashboardMetric(string title, string value, string detail, string accent)
    {
        var content = new StackPanel();
        content.Children.Add(Label(title, 12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold));
        content.Children.Add(Label(value, 23, (Brush)FindResource("TextBrush"), FontWeights.Bold));
        content.Children.Add(Label(detail, 11, (Brush)FindResource("MutedBrush")));
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
            Padding = new Thickness(13, 10, 10, 10),
            Margin = new Thickness(0, 0, 8, 0),
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
        panel.Children.Add(Label(title, 14, (Brush)FindResource("TextBrush"), FontWeights.Bold));
        panel.Children.Add(content);
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadius"),
            Padding = new Thickness(14, 11, 14, 12),
            Margin = new Thickness(0, 0, 8, 0),
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
            chart.Children.Add(Label("No booking data for this date.", 12, (Brush)FindResource("MutedBrush")));
            return chart;
        }

        var maxCount = Math.Max(1, groups.Max(group => group.Count()));
        foreach (var group in groups)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.Children.Add(Label(group.First().StatusLabel, 11, (Brush)FindResource("TextBrush")));
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = maxCount,
                Value = group.Count(),
                Height = 10,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = StatusBrush(group.Key),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = $"{group.Count()} booking(s)"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var count = Label(group.Count().ToString("N0"), 11, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
            count.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(count, 2);
            row.Children.Add(count);
            chart.Children.Add(row);
        }
        return chart;
    }

    private UIElement BuildSlotChart(IReadOnlyList<BookingQueueRecord> activeRecords)
    {
        var chart = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        foreach (var rule in _bookingQueueSlotRules)
        {
            var count = activeRecords.Count(item => item.Slot == rule.Slot);
            var isClosed = !rule.IsOpen;
            var max = rule.Limit is int capacity && capacity > 0 ? capacity : Math.Max(1, count);
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            row.Children.Add(Label(rule.Slot, 11, (Brush)FindResource("TextBrush")));
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = max,
                Value = count,
                Height = 10,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = isClosed ? CreateBrush("#94A3B8")
                    : count >= max && rule.Limit is not null ? CreateBrush("#D9534F") : CreateBrush("#0F766E"),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = isClosed ? "This slot is closed"
                    : rule.Limit is int limit ? $"{count} of {limit} active bookings" : $"{count} active bookings · unlimited"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var countLabel = Label(isClosed ? "Closed" : rule.Limit is int limitText ? $"{count} / {limitText}" : $"{count} / ∞", 11,
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
                $"{ResolveBookingRubberTypeName(ReadProperty(lot, "rubberType", "rubber_type"), "")} · {FirstNonEmpty(ReadProperty(lot, "rubberSource", "rubber_source"), "Source not set")}",
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
            chart.Children.Add(Label("No Lot records for this date.", 12, (Brush)FindResource("MutedBrush")));
            return chart;
        }

        var maxWeight = Math.Max(1m, groups.Max(item => item.ExpectedWeight));
        foreach (var group in groups)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            var name = Label(group.Name, 11, (Brush)FindResource("TextBrush"));
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = group.Name;
            row.Children.Add(name);
            var progress = new ProgressBar
            {
                Minimum = 0,
                Maximum = (double)maxWeight,
                Value = (double)group.ExpectedWeight,
                Height = 10,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = CreateBrush("#2563EB"),
                Background = CreateBrush("#E9EEF4"),
                BorderThickness = new Thickness(0),
                ToolTip = $"Expected {FormatDashboardWeight(group.ExpectedWeight)} · received {FormatDashboardWeight(group.ReceivedWeight)}"
            };
            Grid.SetColumn(progress, 1);
            row.Children.Add(progress);
            var detail = Label($"{group.Received}/{group.Count} lots · {FormatDashboardWeight(group.ExpectedWeight)}", 10,
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
        DataGridTextColumn Text(string header, string path, double width, bool wrap = false)
        {
            var column = new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(path),
                Width = new DataGridLength(width, DataGridLengthUnitType.Star)
            };
            if (wrap)
            {
                var style = new Style(typeof(TextBlock));
                style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                style.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(3, 2, 3, 2)));
                column.ElementStyle = style;
            }
            return column;
        }

        grid.Columns.Add(Text("Queue", nameof(CuplumpDashboardRow.QueueNo), .42));
        grid.Columns.Add(Text("Booking code", nameof(CuplumpDashboardRow.BookingCode), 1.0));
        grid.Columns.Add(Text("Time slot", nameof(CuplumpDashboardRow.Slot), .72));
        grid.Columns.Add(Text("Supplier", nameof(CuplumpDashboardRow.SupplierName), 1.15));
        grid.Columns.Add(Text("Truck", nameof(CuplumpDashboardRow.Truck), .85));
        grid.Columns.Add(Text("Lots · type · source · number · weight · receipt", nameof(CuplumpDashboardRow.LotsDisplay), 2.15, true));
        grid.Columns.Add(Text("Lab samples · DRC · moisture · CP · PRI", nameof(CuplumpDashboardRow.LabDisplay), 1.65, true));
        grid.Columns.Add(Text("Status", nameof(CuplumpDashboardRow.Status), .75));
        grid.Columns.Add(Text("Check-in", nameof(CuplumpDashboardRow.CheckIn), 1.0));
        grid.Columns.Add(Text("Note", nameof(CuplumpDashboardRow.Note), 1.1, true));
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
            Width = new DataGridLength(.65, DataGridLengthUnitType.Star)
        });
    }

    private void CuplumpDashboardTicket_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button { Tag: "OpenTicket", DataContext: CuplumpDashboardRow row })
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

    private string FormatDashboardLots(BookingQueueRecord record)
    {
        var lots = GetDashboardLots(record).ToList();
        if (lots.Count == 0) return "No Lot details";
        return string.Join(Environment.NewLine, lots.Select(lot =>
        {
            var kind = FirstNonEmpty(ReadProperty(lot, "kind"), "LOT");
            var type = ResolveBookingRubberTypeName(ReadProperty(lot, "rubberType", "rubber_type"), "");
            var source = FirstNonEmpty(ReadProperty(lot, "rubberSource", "rubber_source"), "Source not set");
            var number = FirstNonEmpty(ReadProperty(lot, "lotNo", "lot_no"), "Lot number not set");
            var weight = ReadDashboardWeight(lot, "expectedWeight", "expected_weight");
            var receipt = IsLotReceived(lot) ? "Received" : "Pending";
            return $"{kind} · {type} · {source} · {number} · {FormatDashboardWeight(weight)} · {receipt}";
        }));
    }

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

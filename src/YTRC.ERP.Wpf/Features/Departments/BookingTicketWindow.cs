using System.Globalization;
using System.Windows.Media.Imaging;
using QRCoder;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private sealed record BookingTicketPalette(
        string Background, string Border, string Label, string Muted, string Divider, string QueueBadge);

    private void OpenBookingTicket(BookingQueueRecord record)
    {
        var palette = GetBookingTicketPalette(record.Date);
        var ticket = new Border
        {
            Width = 430,
            Background = CreateBrush(palette.Background),
            BorderBrush = CreateBrush(palette.Border),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(22, 18, 22, 20)
        };

        var body = new StackPanel();
        var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var logo = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/Assets/YTRC-Logo.png", UriKind.Absolute)),
            Width = 140,
            Height = 40,
            Stretch = System.Windows.Media.Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(logo);
        var title = new TextBlock
        {
            Text = $"Queue {record.Category}",
            FontSize = 20,
            FontWeight = FontWeights.ExtraBold,
            Foreground = CreateBrush("#1A1A1A"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(12, 0, 0, 0)
        };
        Grid.SetColumn(title, 1);
        header.Children.Add(title);
        body.Children.Add(header);

        var thaiDate = FormatBookingTicketDate(record.Date);
        body.Children.Add(BookingTicketInfoRow("Code:", record.SupplierCode, palette, true));
        body.Children.Add(BookingTicketInfoRow("Name:", record.SupplierName, palette, true));
        body.Children.Add(BookingTicketInfoRow("Date:", thaiDate, palette, false, "#1A1A1A"));
        body.Children.Add(BookingTicketInfoRow("Time:", record.Slot.Replace('-', '–'), palette, false, "#1A1A1A"));
        body.Children.Add(BookingTicketInfoRow("Truck Type:", FormatBookingTruckType(record.TruckType), palette, true));
        body.Children.Add(BookingTicketInfoRow("Truck Register:", record.TruckRegisterDisplay, palette, true));
        body.Children.Add(BookingTicketRubberTypeInfoRow("Rubber Type:", record.RubberTypeBadge, palette));

        var queueRow = new Grid { Margin = new Thickness(0, 8, 0, 2) };
        queueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        queueRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var queueLabel = new TextBlock
        {
            Text = "Queue:",
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush("#1A1A1A"),
            VerticalAlignment = VerticalAlignment.Center
        };
        queueRow.Children.Add(queueLabel);
        var queueBadge = new Border
        {
            Width = 72,
            Height = 76,
            CornerRadius = new CornerRadius(14),
            Background = CreateBrush(palette.QueueBadge),
            Child = new TextBlock
            {
                Text = record.QueueNo.ToString(CultureInfo.InvariantCulture),
                FontSize = 38,
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
            Margin = new Thickness(0, 12, 0, 10),
            Stroke = CreateBrush(palette.Divider),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        });
        body.Children.Add(new TextBlock
        {
            Text = "สามารถจอดรถค้างคืนได้\nบริเวณด้านหน้าโรงงาน",
            TextAlignment = TextAlignment.Center,
            FontSize = 16,
            LineHeight = 25,
            Foreground = CreateBrush(palette.Muted),
            Margin = new Thickness(0, 0, 0, 6)
        });
        body.Children.Add(new TextBlock
        {
            Text = "*ห้ามจอดรถบนถนนทางเข้าโรงงาน*",
            TextAlignment = TextAlignment.Center,
            FontSize = 16,
            FontWeight = FontWeights.ExtraBold,
            Foreground = CreateBrush("#CC1111"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        var qrImage = new Image
        {
            Source = CreateBookingQrCode(record.BookingCode, palette.Background),
            Width = 160,
            Height = 160,
            HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.HighQuality);
        body.Children.Add(qrImage);
        ticket.Child = body;

        var content = new Grid { Margin = new Thickness(16) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new TextBlock
        {
            Text = $"Ticket · {record.BookingCode}",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            Margin = new Thickness(2, 0, 0, 12)
        };
        content.Children.Add(heading);

        var scroll = new ScrollViewer
        {
            Content = ticket,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(scroll, 1);
        content.Children.Add(scroll);

        var actions = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var copyStatus = new TextBlock
        {
            Foreground = (Brush)FindResource("MutedBrush"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };
        actions.Children.Add(copyStatus);
        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(actionButtons, 1);
        actions.Children.Add(actionButtons);

        var copyButton = PurchasingButton("Copy Ticket");
        copyButton.Margin = new Thickness(0, 0, 8, 0);
        copyButton.Click += (_, _) =>
        {
            try
            {
                ticket.Measure(new Size(ticket.Width, double.PositiveInfinity));
                var ticketSize = new Size(ticket.Width, Math.Max(ticket.DesiredSize.Height, ticket.ActualHeight));
                ticket.Arrange(new Rect(new Point(0, 0), ticketSize));
                ticket.UpdateLayout();
                const double renderScale = 2;
                var pixelWidth = Math.Max(1, (int)Math.Ceiling(ticketSize.Width * renderScale));
                var pixelHeight = Math.Max(1, (int)Math.Ceiling(ticketSize.Height * renderScale));
                var image = new RenderTargetBitmap(pixelWidth, pixelHeight, 96 * renderScale,
                    96 * renderScale, PixelFormats.Pbgra32);
                image.Render(ticket);
                image.Freeze();
                Clipboard.SetImage(image);
                copyStatus.Text = "Ticket image copied.";
                copyStatus.Foreground = CreateBrush("#16804A");
                copyStatus.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                copyStatus.Text = $"Copy failed: {ex.Message}";
                copyStatus.Foreground = CreateBrush("#B42318");
                copyStatus.Visibility = Visibility.Visible;
            }
        };
        actionButtons.Children.Add(copyButton);

        var printButton = PurchasingButton("Print Ticket", primary: true);
        printButton.Margin = new Thickness(0, 0, 8, 0);
        printButton.Click += (_, _) =>
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
                printDialog.PrintVisual(ticket, $"Booking Ticket {record.BookingCode}");
        };
        actionButtons.Children.Add(printButton);
        var closeButton = PurchasingButton("Close");
        closeButton.Click += (_, _) => Window.GetWindow(closeButton)?.Close();
        actionButtons.Children.Add(closeButton);
        Grid.SetRow(actions, 2);
        content.Children.Add(actions);

        var ticketWindow = new Window
        {
            Title = "Booking Ticket",
            Owner = this,
            Width = 500,
            Height = Math.Min(880, SystemParameters.WorkArea.Height - 48),
            MinHeight = Math.Min(620, SystemParameters.WorkArea.Height - 48),
            MaxHeight = SystemParameters.WorkArea.Height,
            MinWidth = 480,
            ResizeMode = ResizeMode.CanResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)FindResource("CanvasBrush"),
            Content = content
        };
        ticketWindow.ShowDialog();
    }

    private static UIElement BookingTicketInfoRow(string label, string value,
        BookingTicketPalette palette, bool boldValue, string? valueColor = null)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush(palette.Label),
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.NoWrap
        });
        var valueText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
            FontSize = 16,
            FontWeight = boldValue ? FontWeights.Bold : FontWeights.Normal,
            Foreground = CreateBrush(valueColor ?? (boldValue ? "#1A1A1A" : palette.Muted)),
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(valueText, 1);
        row.Children.Add(valueText);
        return row;
    }

    private static UIElement BookingTicketRubberTypeInfoRow(string label, PurchasingBadge badge,
        BookingTicketPalette palette)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = CreateBrush(palette.Label),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        });
        var badgeBorder = new Border
        {
            Padding = new Thickness(8, 2, 8, 2),
            Background = badge.Background,
            BorderBrush = badge.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badge.Label,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = badge.Foreground,
                TextAlignment = TextAlignment.Right,
                TextWrapping = TextWrapping.Wrap
            }
        };
        Grid.SetColumn(badgeBorder, 1);
        row.Children.Add(badgeBorder);
        return row;
    }

    private static BitmapImage CreateBookingQrCode(string bookingCode, string ticketBackground)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(bookingCode, QRCodeGenerator.ECCLevel.H);
        using var renderer = new PngByteQRCode(data);
        var background = (Color)ColorConverter.ConvertFromString(ticketBackground)!;
        var darkModules = new byte[] { 0, 0, 0 };
        var lightModules = new[] { background.R, background.G, background.B };
        using var stream = new MemoryStream(renderer.GetGraphic(5, darkModules, lightModules, true));
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string FormatBookingTicketDate(DateTime date)
    {
        var weekdays = new[] { "อาทิตย์", "จันทร์", "อังคาร", "พุธ", "พฤหัสบดี", "ศุกร์", "เสาร์" };
        var month = CultureInfo.GetCultureInfo("th-TH").DateTimeFormat.GetMonthName(date.Month);
        return $"(วัน{weekdays[(int)date.DayOfWeek]}) {date.Day} {month} {date.Year + 543}";
    }

    private static string FormatBookingTruckType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "6-wheel" or "6 wheel" => "6 Wheels",
        "10-wheel" or "10 wheel" => "10 Wheels",
        "18-wheel" or "18 wheel" => "10 Wheels Trailer",
        "trailer" => "Trailer",
        "pickup" => "Pickup",
        "" => "—",
        _ => value
    };

    private static BookingTicketPalette GetBookingTicketPalette(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Sunday => new("#FFE5E5", "#D45A5A", "#8A3030", "#A05A5A", "#D45A5A", "#D45A5A"),
        DayOfWeek.Monday => new("#FFFDE7", "#FBC02D", "#8A8030", "#A09A5A", "#FBC02D", "#FBC02D"),
        DayOfWeek.Tuesday => new("#FCE4EC", "#D45A8A", "#8A3048", "#A05A6A", "#D45A8A", "#D45A8A"),
        DayOfWeek.Wednesday => new("#E8F5E9", "#52C41A", "#2A6E33", "#4A8A5A", "#52C41A", "#52C41A"),
        DayOfWeek.Thursday => new("#FFF3E0", "#D4905A", "#8B6530", "#A08060", "#C4905A", "#F07C1A"),
        DayOfWeek.Friday => new("#E3F2FD", "#1677FF", "#003EB3", "#0958D9", "#1677FF", "#1677FF"),
        DayOfWeek.Saturday => new("#F3E5F5", "#905AD4", "#52308B", "#755AA0", "#905AD4", "#905AD4"),
        _ => new("#FFF3E0", "#D4905A", "#8B6530", "#A08060", "#C4905A", "#F07C1A")
    };
}

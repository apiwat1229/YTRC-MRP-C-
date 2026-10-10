using System.Globalization;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using QRCoder;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const uint PrintDialogReturnDc = 0x00000100;
    private const uint PrintDialogNoSelection = 0x00000004;
    private const uint PrintDialogNoPageNumbers = 0x00000008;
    private const uint PrintDialogHidePrintToFile = 0x00100000;
    private const uint PrintDialogDisablePrintToFile = 0x00080000;
    private const uint DibRgbColors = 0;
    private const uint SourceCopy = 0x00CC0020;
    private const int HorizontalResolution = 8;
    private const int VerticalResolution = 10;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePrintDialog
    {
        public uint StructSize;
        public IntPtr Owner;
        public IntPtr DevMode;
        public IntPtr DevNames;
        public IntPtr DeviceContext;
        public uint Flags;
        public ushort FromPage;
        public ushort ToPage;
        public ushort MinPage;
        public ushort MaxPage;
        public ushort Copies;
        public IntPtr Instance;
        public IntPtr CustomData;
        public IntPtr PrintHook;
        public IntPtr SetupHook;
        public IntPtr PrintTemplateName;
        public IntPtr SetupTemplateName;
        public IntPtr PrintTemplate;
        public IntPtr SetupTemplate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDocInfo
    {
        public int Size;
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Output;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
        public int Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("comdlg32.dll", EntryPoint = "PrintDlgW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowLegacyPrintDialog(ref NativePrintDialog printDialog);

    [DllImport("comdlg32.dll", EntryPoint = "CommDlgExtendedError")]
    private static extern uint GetPrintDialogExtendedError();

    [DllImport("kernel32.dll", EntryPoint = "GlobalFree")]
    private static extern IntPtr FreeGlobalMemory(IntPtr memory);

    [DllImport("gdi32.dll", EntryPoint = "StartDocW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int StartPrinterDocument(IntPtr deviceContext, ref NativeDocInfo documentInfo);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StartPrinterPage(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndPrinterPage(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int EndPrinterDocument(IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int AbortPrinterDocument(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern int GetPrinterDeviceCapability(IntPtr deviceContext, int index);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int StretchDIBits(
        IntPtr deviceContext, int xDestination, int yDestination, int destinationWidth, int destinationHeight,
        int xSource, int ySource, int sourceWidth, int sourceHeight, IntPtr bits,
        ref BitmapInfo bitmapInfo, uint colorUse, uint rasterOperation);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeletePrinterDeviceContext(IntPtr deviceContext);

    private sealed record BookingTicketPalette(
        string Background, string Border, string Label, string Muted, string Divider, string QueueBadge);

    private void OpenBookingTicket(BookingQueueRecord record)
    {
        var palette = GetBookingTicketPalette(record.Date);
        var ticket = BuildBookingTicketCard(record, palette);
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

        var ticketView = new Viewbox
        {
            Child = ticket,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(ticketView, 1);
        content.Children.Add(ticketView);

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
                Clipboard.SetImage(RenderBookingTicketImage(record, palette));
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
        printButton.Click += (_, _) => PrintBookingTicket(ticket, record, palette);
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

    private void PrintBookingTicket(FrameworkElement ticket, BookingQueueRecord record, BookingTicketPalette palette)
    {
        var owner = Window.GetWindow(ticket);
        var request = new NativePrintDialog
        {
            StructSize = (uint)Marshal.SizeOf<NativePrintDialog>(),
            Owner = owner is null ? IntPtr.Zero : new System.Windows.Interop.WindowInteropHelper(owner).Handle,
            Flags = PrintDialogReturnDc | PrintDialogNoSelection | PrintDialogNoPageNumbers |
                    PrintDialogHidePrintToFile | PrintDialogDisablePrintToFile
        };

        if (!ShowLegacyPrintDialog(ref request))
        {
            var error = GetPrintDialogExtendedError();
            if (error != 0)
                MessageBox.Show(owner, $"Could not open the print dialog (error {error}).", "Print ticket",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            FreePrintDialogHandles(request);
            return;
        }

        var documentStarted = false;
        try
        {
            if (request.DeviceContext == IntPtr.Zero)
                throw new InvalidOperationException("Windows did not return a printer device context.");

            var rendered = RenderBookingTicketImage(record, palette, renderScale: 4);
            var bitmap = new FormatConvertedBitmap(rendered, PixelFormats.Bgra32, null, 0);
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var stride = checked(width * 4);
            var pixels = new byte[checked(stride * height)];
            bitmap.CopyPixels(pixels, stride, 0);

            var pageWidth = GetPrinterDeviceCapability(request.DeviceContext, HorizontalResolution);
            var pageHeight = GetPrinterDeviceCapability(request.DeviceContext, VerticalResolution);
            if (pageWidth <= 0 || pageHeight <= 0)
                throw new InvalidOperationException("The selected printer returned an invalid printable area.");

            const double safeAreaScale = .96;
            var scale = Math.Min((double)pageWidth / width, (double)pageHeight / height) * safeAreaScale;
            var destinationWidth = Math.Max(1, (int)Math.Round(width * scale));
            var destinationHeight = Math.Max(1, (int)Math.Round(height * scale));
            var destinationX = Math.Max(0, (pageWidth - destinationWidth) / 2);
            var destinationY = Math.Max(0, (pageHeight - destinationHeight) / 2);

            var info = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = width,
                    Height = -height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    SizeImage = (uint)pixels.Length
                }
            };
            var pinnedPixels = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var documentInfo = new NativeDocInfo
                {
                    Size = Marshal.SizeOf<NativeDocInfo>(),
                    DocumentName = $"Booking Ticket {record.BookingCode}"
                };
                if (StartPrinterDocument(request.DeviceContext, ref documentInfo) <= 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start the print job.");
                documentStarted = true;

                if (StartPrinterPage(request.DeviceContext) <= 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not start the printer page.");

                var copied = StretchDIBits(request.DeviceContext,
                    destinationX, destinationY, destinationWidth, destinationHeight,
                    0, 0, width, height, pinnedPixels.AddrOfPinnedObject(), ref info,
                    DibRgbColors, SourceCopy);
                if (copied == 0 || copied == -1)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not render the ticket on the printer page.");

                if (EndPrinterPage(request.DeviceContext) <= 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not finish the printer page.");
                if (EndPrinterDocument(request.DeviceContext) <= 0)
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not finish the print job.");
                documentStarted = false;
            }
            finally
            {
                pinnedPixels.Free();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(owner, $"Could not print the ticket: {exception.Message}", "Print ticket",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (documentStarted && request.DeviceContext != IntPtr.Zero)
                AbortPrinterDocument(request.DeviceContext);
            if (request.DeviceContext != IntPtr.Zero)
                DeletePrinterDeviceContext(request.DeviceContext);
            FreePrintDialogHandles(request);
        }
    }

    private static void FreePrintDialogHandles(NativePrintDialog request)
    {
        if (request.DevMode != IntPtr.Zero) FreeGlobalMemory(request.DevMode);
        if (request.DevNames != IntPtr.Zero) FreeGlobalMemory(request.DevNames);
    }

    private RenderTargetBitmap RenderBookingTicketImage(BookingQueueRecord record, BookingTicketPalette palette, double renderScale = 2)
    {
        // Render a separate, naturally measured ticket so the visible card or
        // ScrollViewer viewport cannot clip the copied or downloaded image.
        var renderTicket = BuildBookingTicketCard(record, palette);
        renderTicket.Measure(new Size(renderTicket.Width, double.PositiveInfinity));
        var ticketSize = renderTicket.DesiredSize;
        renderTicket.Arrange(new Rect(ticketSize));
        renderTicket.UpdateLayout();

        var pixelWidth = Math.Max(1, (int)Math.Ceiling(ticketSize.Width * renderScale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(ticketSize.Height * renderScale));
        var image = new RenderTargetBitmap(pixelWidth, pixelHeight,
            96 * renderScale, 96 * renderScale, PixelFormats.Pbgra32);
        image.Render(renderTicket);
        image.Freeze();
        return image;
    }

    private Border BuildBookingTicketCard(BookingQueueRecord record, BookingTicketPalette palette)
    {
        var ticket = new Border
        {
            Width = 430,
            Background = CreateBrush(palette.Background),
            BorderBrush = CreateBrush(palette.Border),
            BorderThickness = new Thickness(3),
            CornerRadius = UiTokens.CornerRadiusLarge,
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
            Width = 82,
            Height = 76,
            CornerRadius = UiTokens.CornerRadiusLarge,
            Background = CreateBrush(palette.QueueBadge),
            Child = new TextBlock
            {
                Text = record.QueueDisplay,
                FontSize = 34,
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
            Width = 210,
            Height = 210,
            Margin = new Thickness(0, 28, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true
        };
        RenderOptions.SetBitmapScalingMode(qrImage, BitmapScalingMode.HighQuality);
        body.Children.Add(qrImage);
        ticket.Child = body;

        return ticket;
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
            CornerRadius = UiTokens.CornerRadiusSmall,
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

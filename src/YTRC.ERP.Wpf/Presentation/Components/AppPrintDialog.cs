using System.Diagnostics;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentIcons.Common;
using FluentIcons.Wpf;
using Symbol = FluentIcons.Common.Icon;

namespace YTRC.ERP.Wpf;

/// <summary>
/// Universal, modern Print Dialog with interactive live preview, zoom/pan controls,
/// popular paper sizes, grayscale toggle, and app-branded styling.
/// Replaces legacy/system print dialogs across the entire ERP application.
/// </summary>
public sealed class AppPrintDialog : Window
{
    public sealed record PaperSizeOption(string DisplayName, PageMediaSizeName MediaSizeName, double WidthPt, double HeightPt)
    {
        public override string ToString() => DisplayName;
    }

    public static readonly IReadOnlyList<PaperSizeOption> PopularPaperSizes =
    [
        new("A4 (210 × 297 mm)", PageMediaSizeName.ISOA4, 793.7, 1122.5),
        new("Letter (8.5 × 11 in)", PageMediaSizeName.NorthAmericaLetter, 816.0, 1056.0),
        new("Legal (8.5 × 14 in)", PageMediaSizeName.NorthAmericaLegal, 816.0, 1344.0),
        new("A3 (297 × 420 mm)", PageMediaSizeName.ISOA3, 1122.5, 1587.4),
        new("A5 (148 × 210 mm)", PageMediaSizeName.ISOA5, 559.4, 793.7),
        new("B4 (JIS) (257 × 364 mm)", PageMediaSizeName.JISB4, 971.3, 1375.7),
        new("B5 (JIS) (182 × 257 mm)", PageMediaSizeName.JISB5, 687.9, 971.3)
    ];

    private readonly string _documentTitle;
    private readonly Func<FrameworkElement>? _visualFactory;
    private readonly Func<double, double, PageOrientation, FrameworkElement>? _visualFactoryWithOptions;
    private readonly Func<FlowDocument>? _flowDocFactory;

    private readonly ComboBox _printerBox;
    private readonly TextBox _copiesText;
    private readonly CheckBox _cbCollate;
    private readonly CheckBox _cbGrayscale;
    private readonly CheckBox _cbDuplex;

    private readonly RadioButton _rbFit;
    private readonly RadioButton _rbActual;
    private readonly RadioButton _rbZoom;
    private readonly TextBox _zoomBox;

    private readonly ComboBox _paperSizeBox;
    private readonly RadioButton _rbPortrait;
    private readonly RadioButton _rbLandscape;

    private readonly ScrollViewer _previewScroller;
    private readonly Border _paperBorder;
    private readonly Border _previewViewport;
    private readonly ScaleTransform _previewScale;
    private readonly TextBlock _zoomPercentLabel;

    private double _currentPreviewZoom = 1.0;
    private bool _isUpdatingPreview;

    public static bool ShowPrint(
        Window? owner,
        string documentTitle,
        Func<FrameworkElement> visualFactory,
        PageOrientation defaultOrientation = PageOrientation.Portrait,
        PageMediaSizeName defaultPaperSize = PageMediaSizeName.ISOA4)
    {
        var dlg = new AppPrintDialog(owner, documentTitle, visualFactory, null, null, defaultOrientation, defaultPaperSize);
        return dlg.ShowDialog() == true;
    }

    public static bool ShowPrint(
        Window? owner,
        string documentTitle,
        Func<double, double, PageOrientation, FrameworkElement> visualFactoryWithOptions,
        PageOrientation defaultOrientation = PageOrientation.Portrait,
        PageMediaSizeName defaultPaperSize = PageMediaSizeName.ISOA4)
    {
        var dlg = new AppPrintDialog(owner, documentTitle, null, visualFactoryWithOptions, null, defaultOrientation, defaultPaperSize);
        return dlg.ShowDialog() == true;
    }

    public static bool ShowPrint(
        Window? owner,
        string documentTitle,
        Func<FlowDocument> flowDocFactory,
        PageOrientation defaultOrientation = PageOrientation.Portrait,
        PageMediaSizeName defaultPaperSize = PageMediaSizeName.ISOA4)
    {
        var dlg = new AppPrintDialog(owner, documentTitle, null, null, flowDocFactory, defaultOrientation, defaultPaperSize);
        return dlg.ShowDialog() == true;
    }

    private AppPrintDialog(
        Window? owner,
        string documentTitle,
        Func<FrameworkElement>? visualFactory,
        Func<double, double, PageOrientation, FrameworkElement>? visualFactoryWithOptions,
        Func<FlowDocument>? flowDocFactory,
        PageOrientation defaultOrientation,
        PageMediaSizeName defaultPaperSize)
    {
        _documentTitle = documentTitle;
        _visualFactory = visualFactory;
        _visualFactoryWithOptions = visualFactoryWithOptions;
        _flowDocFactory = flowDocFactory;

        Title = $"Print · {documentTitle}";
        if (owner is not null) Owner = owner;
        Width = 1080;
        Height = Math.Min(820, SystemParameters.WorkArea.Height - 40);
        MinWidth = 920;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;

        // Printer enumeration
        var printers = new List<string>();
        string? defaultPrinter = null;
        try
        {
            var server = new LocalPrintServer();
            var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
            foreach (var q in queues)
            {
                if (!string.IsNullOrWhiteSpace(q.FullName) && !printers.Contains(q.FullName, StringComparer.OrdinalIgnoreCase))
                {
                    printers.Add(q.FullName);
                }
            }
            defaultPrinter = LocalPrintServer.GetDefaultPrintQueue()?.FullName;
        }
        catch { }

        if (printers.Count == 0) printers.Add("Microsoft Print to PDF");
        if (string.IsNullOrWhiteSpace(defaultPrinter) || !printers.Contains(defaultPrinter, StringComparer.OrdinalIgnoreCase))
        {
            defaultPrinter = printers[0];
        }

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        var mainLayout = new Grid { Margin = new Thickness(16, 14, 16, 10) };
        mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(450) });
        mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) }); // Gap
        mainLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // ========================================================
        // LEFT COLUMN: SETTINGS
        // ========================================================
        var leftScroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0, 0, 8, 0)
        };
        var leftStack = new StackPanel();

        // 1. Printer Row
        var printerRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        printerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        printerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        printerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });

        var printerLabel = new TextBlock
        {
            Text = "Printer",
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Foreground = CreateBrush("#334155"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(printerLabel, 0);
        printerRow.Children.Add(printerLabel);

        _printerBox = new ComboBox
        {
            ItemsSource = printers,
            SelectedItem = defaultPrinter,
            Height = 32,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        Grid.SetColumn(_printerBox, 1);
        printerRow.Children.Add(_printerBox);

        var prefButton = new Button
        {
            Content = "Preference...",
            FontSize = 12,
            Height = 32,
            Padding = new Thickness(8, 0, 8, 0),
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        UiControlProperties.SetButtonCornerRadius(prefButton, new CornerRadius(4));
        prefButton.Click += (_, _) => OpenSelectedPrinterProperties();
        Grid.SetColumn(prefButton, 2);
        printerRow.Children.Add(prefButton);
        leftStack.Children.Add(printerRow);

        // 2. Copies, Collate & Grayscale
        var copiesRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10),
            VerticalAlignment = VerticalAlignment.Center
        };
        copiesRow.Children.Add(new TextBlock
        {
            Text = "Copies",
            FontSize = 13,
            FontWeight = FontWeights.Medium,
            Foreground = CreateBrush("#334155"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        _copiesText = new TextBox
        {
            Text = "1",
            Width = 46,
            Height = 28,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 12.5,
            Margin = new Thickness(0, 0, 16, 0)
        };
        copiesRow.Children.Add(_copiesText);

        _cbCollate = new CheckBox
        {
            Content = "Collate",
            IsChecked = true,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0)
        };
        copiesRow.Children.Add(_cbCollate);

        _cbGrayscale = new CheckBox
        {
            Content = "Grayscale",
            IsChecked = false,
            FontSize = 12.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        _cbGrayscale.Checked += (_, _) => UpdatePreviewVisual();
        _cbGrayscale.Unchecked += (_, _) => UpdatePreviewVisual();
        copiesRow.Children.Add(_cbGrayscale);
        leftStack.Children.Add(copiesRow);

        // Helper for Settings Cards
        Border CreateCard(string title, UIElement content)
        {
            var card = new Border
            {
                Background = Brushes.White,
                BorderBrush = CreateBrush("#E2E8F0"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 12),
                Margin = new Thickness(0, 0, 0, 10)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#334155"),
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(content);
            card.Child = stack;
            return card;
        }

        // 3. Page Range Card
        var pageRangeStack = new StackPanel();
        var rbAllPages = new RadioButton { Content = "All pages (1 / 1)", IsChecked = true, Margin = new Thickness(0, 2, 0, 4), FontSize = 12 };
        var rbCurrentPage = new RadioButton { Content = "Current page", Margin = new Thickness(0, 0, 0, 4), FontSize = 12 };
        var rbSelectedPages = new RadioButton { Content = "Selected pages", Margin = new Thickness(0, 0, 0, 4), FontSize = 12 };
        pageRangeStack.Children.Add(rbAllPages);
        pageRangeStack.Children.Add(rbCurrentPage);
        pageRangeStack.Children.Add(rbSelectedPages);
        leftStack.Children.Add(CreateCard("Page Range", pageRangeStack));

        // 4. Page Sizing & Handling Card (WITHOUT Size/Multiple/Poster/Booklet tabs!)
        var sizingStack = new StackPanel();
        _rbFit = new RadioButton { Content = "Fit Page (Auto-fit to printable area)", IsChecked = true, FontSize = 12, Margin = new Thickness(0, 2, 0, 6) };
        _rbActual = new RadioButton { Content = "Actual Size (100%)", FontSize = 12, Margin = new Thickness(0, 0, 0, 6) };

        var zoomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) };
        _rbZoom = new RadioButton { Content = "Custom Zoom", FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        _zoomBox = new TextBox { Text = "100", Width = 48, Height = 26, FontSize = 12, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        zoomRow.Children.Add(_rbZoom);
        zoomRow.Children.Add(_zoomBox);
        zoomRow.Children.Add(new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(4, 0, 0, 0) });

        _cbDuplex = new CheckBox { Content = "Print on both sides (Duplex)", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };

        sizingStack.Children.Add(_rbFit);
        sizingStack.Children.Add(_rbActual);
        sizingStack.Children.Add(zoomRow);
        sizingStack.Children.Add(_cbDuplex);
        leftStack.Children.Add(CreateCard("Page Sizing", sizingStack));

        // 5. Page Settings Card
        var pageSettingsStack = new StackPanel();
        pageSettingsStack.Children.Add(new TextBlock { Text = "Paper Size", FontSize = 11.5, Foreground = CreateBrush("#64748B"), Margin = new Thickness(0, 0, 0, 3) });

        _paperSizeBox = new ComboBox
        {
            ItemsSource = PopularPaperSizes,
            DisplayMemberPath = nameof(PaperSizeOption.DisplayName),
            Height = 30,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 10)
        };
        // Select matching paper size
        var matchIdx = 0;
        for (int i = 0; i < PopularPaperSizes.Count; i++)
        {
            if (PopularPaperSizes[i].MediaSizeName == defaultPaperSize) { matchIdx = i; break; }
        }
        _paperSizeBox.SelectedIndex = matchIdx;
        _paperSizeBox.SelectionChanged += (_, _) => UpdatePreviewScale();
        pageSettingsStack.Children.Add(_paperSizeBox);

        var orientRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        orientRow.Children.Add(new TextBlock { Text = "Orientation", FontSize = 12, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 16, 0) });
        _rbPortrait = new RadioButton { Content = "Portrait", IsChecked = defaultOrientation == PageOrientation.Portrait, FontSize = 12, Margin = new Thickness(0, 0, 16, 0) };
        _rbLandscape = new RadioButton { Content = "Landscape", IsChecked = defaultOrientation == PageOrientation.Landscape, FontSize = 12 };
        _rbPortrait.Checked += (_, _) => UpdatePreviewScale();
        _rbLandscape.Checked += (_, _) => UpdatePreviewScale();
        orientRow.Children.Add(_rbPortrait);
        orientRow.Children.Add(_rbLandscape);
        pageSettingsStack.Children.Add(orientRow);

        leftStack.Children.Add(CreateCard("Page Settings", pageSettingsStack));

        leftScroller.Content = leftStack;
        Grid.SetColumn(leftScroller, 0);
        mainLayout.Children.Add(leftScroller);

        // ========================================================
        // RIGHT COLUMN: LIVE PREVIEW & INTERACTIVE ZOOM
        // ========================================================
        var rightGrid = new Grid();
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Toolbar
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Viewport
        rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer Navigation

        // Preview Header Toolbar
        var prevHeaderDock = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var prevTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        prevTitle.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.DocumentBulletList,
            IconSize = IconSize.Size16,
            Foreground = CreateBrush("#0F766E"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        prevTitle.Children.Add(new TextBlock
        {
            Text = "Live Print Preview",
            FontSize = 13.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = CreateBrush("#1E293B"),
            VerticalAlignment = VerticalAlignment.Center
        });
        prevHeaderDock.Children.Add(prevTitle);

        // Quick Zoom Bar in Header
        var zoomControls = new Border
        {
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(zoomControls, Dock.Right);

        var zoomStack = new StackPanel { Orientation = Orientation.Horizontal };
        Button CreateSmallZoomBtn(string text, string tip, Action onClick, double minWidth = 26)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                MinWidth = minWidth,
                Height = 24,
                Padding = new Thickness(5, 0, 5, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = tip
            };
            UiControlProperties.SetButtonCornerRadius(btn, new CornerRadius(3));
            btn.Click += (_, _) => onClick();
            btn.MouseEnter += (_, _) => btn.Background = CreateBrush("#E2E8F0");
            btn.MouseLeave += (_, _) => btn.Background = Brushes.Transparent;
            return btn;
        }

        _zoomPercentLabel = new TextBlock
        {
            Text = "100%",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = CreateBrush("#334155"),
            Width = 46,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        zoomStack.Children.Add(CreateSmallZoomBtn("−", "Zoom out (Ctrl + Scroll Down)", () => SetZoomManual(_currentPreviewZoom - 0.15)));
        zoomStack.Children.Add(_zoomPercentLabel);
        zoomStack.Children.Add(CreateSmallZoomBtn("+", "Zoom in (Ctrl + Scroll Up)", () => SetZoomManual(_currentPreviewZoom + 0.15)));
        zoomStack.Children.Add(new Border { Width = 1, Background = CreateBrush("#CBD5E1"), Margin = new Thickness(2, 2, 2, 2) });
        zoomStack.Children.Add(CreateSmallZoomBtn("Fit", "Fit preview to window", () => { _rbFit.IsChecked = true; UpdatePreviewScale(); }, 32));
        zoomStack.Children.Add(CreateSmallZoomBtn("100%", "Actual 100% scale", () => { _rbActual.IsChecked = true; UpdatePreviewScale(); }, 38));
        zoomControls.Child = zoomStack;
        prevHeaderDock.Children.Add(zoomControls);

        Grid.SetRow(prevHeaderDock, 0);
        rightGrid.Children.Add(prevHeaderDock);

        // Viewport with ScrollViewer (Allows zooming in & panning!)
        _previewViewport = new Border
        {
            Background = CreateBrush("#F1F5F9"),
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true
        };

        _previewScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16)
        };

        _previewScale = new ScaleTransform(1.0, 1.0);

        _paperBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            LayoutTransform = _previewScale,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(15, 23, 42),
                BlurRadius = 16,
                ShadowDepth = 2,
                Opacity = 0.12
            }
        };

        // Render Crisp Text
        TextOptions.SetTextFormattingMode(_paperBorder, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(_paperBorder, TextRenderingMode.ClearType);
        _paperBorder.SnapsToDevicePixels = true;
        _paperBorder.UseLayoutRounding = true;

        var centerGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        centerGrid.Children.Add(_paperBorder);
        _previewScroller.Content = centerGrid;
        _previewViewport.Child = _previewScroller;

        // Left-click Drag to Pan/Scroll Preview
        Point panStartPoint = default;
        Point panStartOffset = default;
        bool isPanning = false;

        _previewScroller.Cursor = Cursors.Hand;
        _previewScroller.PreviewMouseLeftButtonDown += (s, e) =>
        {
            panStartPoint = e.GetPosition(_previewScroller);
            panStartOffset = new Point(_previewScroller.HorizontalOffset, _previewScroller.VerticalOffset);
            isPanning = true;
            _previewScroller.Cursor = Cursors.ScrollAll;
            _previewScroller.CaptureMouse();
            e.Handled = true;
        };

        _previewScroller.PreviewMouseMove += (s, e) =>
        {
            if (isPanning && _previewScroller.IsMouseCaptured)
            {
                var current = e.GetPosition(_previewScroller);
                var deltaX = current.X - panStartPoint.X;
                var deltaY = current.Y - panStartPoint.Y;
                _previewScroller.ScrollToHorizontalOffset(panStartOffset.X - deltaX);
                _previewScroller.ScrollToVerticalOffset(panStartOffset.Y - deltaY);
            }
        };

        _previewScroller.PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (isPanning)
            {
                isPanning = false;
                _previewScroller.ReleaseMouseCapture();
                _previewScroller.Cursor = Cursors.Hand;
            }
        };

        _previewScroller.LostMouseCapture += (s, e) =>
        {
            isPanning = false;
            _previewScroller.Cursor = Cursors.Hand;
        };

        // Ctrl + Mouse Wheel Zoom
        _previewScroller.PreviewMouseWheel += (s, e) =>
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                var delta = e.Delta > 0 ? 0.12 : -0.12;
                SetZoomManual(_currentPreviewZoom + delta);
            }
        };

        Grid.SetRow(_previewViewport, 1);
        rightGrid.Children.Add(_previewViewport);

        // Bottom Page Nav Bar
        var navBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2)
        };
        Button CreateNavArrow(Symbol symbol, string tooltip)
        {
            var icon = new FluentIcon
            {
                Icon = symbol,
                IconSize = IconSize.Size16,
                IconVariant = IconVariant.Regular,
                Foreground = CreateBrush("#334155"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Button
            {
                Content = icon,
                Width = 28,
                Height = 26,
                Background = CreateBrush("#F8FAFC"),
                BorderBrush = CreateBrush("#CBD5E1"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = tooltip
            };
            UiControlProperties.SetButtonCornerRadius(btn, new CornerRadius(3));
            btn.MouseEnter += (_, _) => btn.Background = CreateBrush("#E2E8F0");
            btn.MouseLeave += (_, _) => btn.Background = CreateBrush("#F8FAFC");
            return btn;
        }
        navBar.Children.Add(CreateNavArrow(Symbol.ChevronLeft, "Previous page"));
        navBar.Children.Add(new Border
        {
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Padding = new Thickness(16, 2, 16, 2),
            Margin = new Thickness(8, 0, 8, 0),
            CornerRadius = new CornerRadius(3),
            Child = new TextBlock { Text = "1 / 1", FontSize = 12, FontWeight = FontWeights.Medium, Foreground = CreateBrush("#334155") }
        });
        navBar.Children.Add(CreateNavArrow(Symbol.ChevronRight, "Next page"));
        Grid.SetRow(navBar, 2);
        rightGrid.Children.Add(navBar);

        Grid.SetColumn(rightGrid, 2);
        mainLayout.Children.Add(rightGrid);

        Grid.SetRow(mainLayout, 0);
        rootGrid.Children.Add(mainLayout);

        // ========================================================
        // FOOTER ACTION BAR (Cleaned up: No Classic Mode!)
        // ========================================================
        var footerBorder = new Border
        {
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#E2E8F0"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var footerDock = new DockPanel();

        // Right Action Buttons
        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(actionButtons, Dock.Right);

        var cancelBtn = new Button
        {
            Content = "Cancel",
            FontSize = 12.5,
            Height = 36,
            MinWidth = 85,
            Margin = new Thickness(0, 0, 10, 0),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            Foreground = CreateBrush("#334155"),
            Cursor = Cursors.Hand
        };
        UiControlProperties.SetButtonCornerRadius(cancelBtn, new CornerRadius(4));
        cancelBtn.Click += (_, _) => DialogResult = false;

        // Print Button styled in App's Primary Color (#0F766E)
        var printBtn = new Button
        {
            Height = 36,
            MinWidth = 105,
            Background = CreateBrush("#0F766E"), // App Primary Teal!
            BorderBrush = CreateBrush("#0F766E"),
            BorderThickness = new Thickness(1),
            Foreground = Brushes.White,
            Cursor = Cursors.Hand
        };
        UiControlProperties.SetButtonCornerRadius(printBtn, new CornerRadius(4));

        var printContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        printContent.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Print,
            IconSize = IconSize.Size16,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        printContent.Children.Add(new TextBlock
        {
            Text = "Print",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        printBtn.Content = printContent;

        // App Primary Hover states
        printBtn.MouseEnter += (_, _) =>
        {
            printBtn.Background = CreateBrush("#0D9488");
            printBtn.BorderBrush = CreateBrush("#0D9488");
        };
        printBtn.MouseLeave += (_, _) =>
        {
            printBtn.Background = CreateBrush("#0F766E");
            printBtn.BorderBrush = CreateBrush("#0F766E");
        };
        printBtn.PreviewMouseDown += (_, _) =>
        {
            printBtn.Background = CreateBrush("#115E59");
            printBtn.BorderBrush = CreateBrush("#115E59");
        };
        printBtn.PreviewMouseUp += (_, _) =>
        {
            printBtn.Background = printBtn.IsMouseOver ? CreateBrush("#0D9488") : CreateBrush("#0F766E");
            printBtn.BorderBrush = printBtn.Background;
        };

        printBtn.Click += (_, _) => ExecutePrintJob();

        actionButtons.Children.Add(cancelBtn);
        actionButtons.Children.Add(printBtn);
        footerDock.Children.Add(actionButtons);
        footerBorder.Child = footerDock;

        Grid.SetRow(footerBorder, 1);
        rootGrid.Children.Add(footerBorder);

        Content = rootGrid;

        // Wire scale update triggers
        _previewViewport.SizeChanged += (_, _) => UpdatePreviewScale();
        _rbFit.Checked += (_, _) => UpdatePreviewScale();
        _rbActual.Checked += (_, _) => UpdatePreviewScale();
        _rbZoom.Checked += (_, _) => UpdatePreviewScale();
        _zoomBox.TextChanged += (_, _) =>
        {
            if (_rbZoom.IsChecked == true) UpdatePreviewScale();
        };

        Loaded += (_, _) =>
        {
            UpdatePreviewVisual();
            UpdatePreviewScale();
        };
    }

    private PaperSizeOption GetSelectedPaperSize()
    {
        if (_paperSizeBox.SelectedItem is PaperSizeOption option) return option;
        return PopularPaperSizes[0];
    }

    private (double Width, double Height) GetCurrentPaperDimensions()
    {
        var opt = GetSelectedPaperSize();
        var isLandscape = _rbLandscape.IsChecked == true;
        return isLandscape ? (opt.HeightPt, opt.WidthPt) : (opt.WidthPt, opt.HeightPt);
    }

    private void UpdatePreviewVisual()
    {
        var (paperW, paperH) = GetCurrentPaperDimensions();
        _paperBorder.Width = paperW;
        _paperBorder.Height = paperH;

        var orientation = _rbLandscape.IsChecked == true ? PageOrientation.Landscape : PageOrientation.Portrait;

        FrameworkElement? visualContent = null;
        if (_visualFactoryWithOptions is not null)
        {
            visualContent = _visualFactoryWithOptions(paperW, paperH, orientation);
        }
        else if (_visualFactory is not null)
        {
            visualContent = _visualFactory();
        }
        else if (_flowDocFactory is not null)
        {
            var doc = _flowDocFactory();
            doc.PageWidth = paperW;
            doc.PageHeight = paperH;
            doc.ColumnWidth = paperW;
            visualContent = new FlowDocumentScrollViewer
            {
                Document = doc,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        if (visualContent is null) return;

        // Auto-fit visual if it exceeds the paper boundaries (e.g. fixed-size legacy visuals)
        var vw = visualContent.Width > 0 ? visualContent.Width : paperW;
        var vh = visualContent.Height > 0 ? visualContent.Height : paperH;
        if (vw > paperW + 0.5 || vh > paperH + 0.5)
        {
            var fitScale = Math.Min(paperW / vw, paperH / vh);
            visualContent.LayoutTransform = new ScaleTransform(fitScale, fitScale);
        }

        visualContent.HorizontalAlignment = HorizontalAlignment.Center;
        visualContent.VerticalAlignment = VerticalAlignment.Center;

        if (_cbGrayscale.IsChecked == true)
        {
            // Live Grayscale preview: render to bitmap and convert to Gray8
            try
            {
                visualContent.Measure(new Size(paperW, paperH));
                visualContent.Arrange(new Rect(0, 0, paperW, paperH));
                visualContent.UpdateLayout();

                var rtb = new RenderTargetBitmap(
                    (int)Math.Max(1, paperW),
                    (int)Math.Max(1, paperH),
                    96, 96, PixelFormats.Pbgra32);
                rtb.Render(visualContent);

                var grayBitmap = new FormatConvertedBitmap(rtb, PixelFormats.Gray8, null, 0);
                _paperBorder.Child = new Image
                {
                    Source = grayBitmap,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return;
            }
            catch { }
        }

        _paperBorder.Child = visualContent;
    }

    private void UpdatePreviewScale()
    {
        if (_isUpdatingPreview) return;
        _isUpdatingPreview = true;
        try
        {
            UpdatePreviewVisual();

            var availW = _previewViewport.ActualWidth - 48;
            var availH = _previewViewport.ActualHeight - 48;
            if (availW <= 40 || availH <= 40) return;

            var (paperW, paperH) = GetCurrentPaperDimensions();

            if (_rbActual.IsChecked == true)
            {
                ApplyPreviewZoom(1.0);
            }
            else if (_rbZoom.IsChecked == true && double.TryParse(_zoomBox.Text?.Trim(), out var zoomPct) && zoomPct >= 10)
            {
                ApplyPreviewZoom(zoomPct / 100.0);
            }
            else
            {
                // Fit Page
                var fitRatio = Math.Min(availW / paperW, availH / paperH);
                ApplyPreviewZoom(Math.Max(0.15, fitRatio));
            }
        }
        finally
        {
            _isUpdatingPreview = false;
        }
    }

    private void SetZoomManual(double targetZoom)
    {
        targetZoom = Math.Clamp(targetZoom, 0.2, 4.0);
        _rbZoom.IsChecked = true;
        _zoomBox.Text = ((int)Math.Round(targetZoom * 100)).ToString();
        ApplyPreviewZoom(targetZoom);
    }

    private void ApplyPreviewZoom(double zoom)
    {
        _currentPreviewZoom = zoom;
        _previewScale.ScaleX = zoom;
        _previewScale.ScaleY = zoom;
        if (_zoomPercentLabel is not null)
        {
            _zoomPercentLabel.Text = $"{(int)Math.Round(zoom * 100)}%";
        }
    }

    private void OpenSelectedPrinterProperties()
    {
        var selectedPrinter = _printerBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedPrinter)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = $"printui.dll,PrintUIEntry /e /n \"{selectedPrinter}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open printer preferences: {ex.Message}", "Printer Properties",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExecutePrintJob()
    {
        try
        {
            var selectedPrinter = _printerBox.SelectedItem as string;
            var printDialog = new PrintDialog();

            if (!string.IsNullOrWhiteSpace(selectedPrinter))
            {
                try
                {
                    var server = new LocalPrintServer();
                    var queue = server.GetPrintQueue(selectedPrinter);
                    if (queue != null) printDialog.PrintQueue = queue;
                }
                catch { }
            }

            var isLandscape = _rbLandscape.IsChecked == true;
            var isGrayscale = _cbGrayscale.IsChecked == true;
            var paperOpt = GetSelectedPaperSize();

            int copies = 1;
            if (int.TryParse(_copiesText.Text?.Trim(), out var parsed) && parsed > 0) copies = parsed;

            try
            {
                printDialog.PrintTicket.CopyCount = copies;
                printDialog.PrintTicket.Collation = _cbCollate.IsChecked == true ? Collation.Collated : Collation.Uncollated;
                printDialog.PrintTicket.PageOrientation = isLandscape ? PageOrientation.Landscape : PageOrientation.Portrait;
                printDialog.PrintTicket.PageMediaSize = new PageMediaSize(paperOpt.MediaSizeName);
                if (isGrayscale)
                {
                    printDialog.PrintTicket.OutputColor = OutputColor.Monochrome;
                }
                if (_cbDuplex.IsChecked == true)
                {
                    printDialog.PrintTicket.Duplexing = Duplexing.TwoSidedLongEdge;
                }
            }
            catch (ArgumentException) { }

            // Determine printable bounds
            var printableW = printDialog.PrintableAreaWidth > 0 ? printDialog.PrintableAreaWidth : (isLandscape ? paperOpt.HeightPt : paperOpt.WidthPt);
            var printableH = printDialog.PrintableAreaHeight > 0 ? printDialog.PrintableAreaHeight : (isLandscape ? paperOpt.WidthPt : paperOpt.HeightPt);

            FrameworkElement? visual = null;
            if (_visualFactoryWithOptions is not null)
            {
                visual = _visualFactoryWithOptions(printableW, printableH, isLandscape ? PageOrientation.Landscape : PageOrientation.Portrait);
            }
            else if (_visualFactory is not null)
            {
                visual = _visualFactory();
            }

            if (visual is not null)
            {
                double scale = 1.0;
                if (_rbFit.IsChecked == true)
                {
                    var origW = visual.Width > 0 ? visual.Width : printableW;
                    var origH = visual.Height > 0 ? visual.Height : printableH;
                    scale = Math.Min(printableW / origW, printableH / origH);
                }
                else if (_rbZoom.IsChecked == true && double.TryParse(_zoomBox.Text, out var zPct) && zPct > 10)
                {
                    scale = zPct / 100.0;
                }

                if (Math.Abs(scale - 1.0) > 0.005)
                {
                    visual.LayoutTransform = new ScaleTransform(scale, scale);
                }

                var printContainer = new Grid
                {
                    Width = printableW,
                    Height = printableH
                };
                printContainer.Children.Add(visual);

                printContainer.Measure(new Size(printableW, printableH));
                printContainer.Arrange(new Rect(new Point(0, 0), new Size(printableW, printableH)));
                printContainer.UpdateLayout();

                printDialog.PrintVisual(printContainer, _documentTitle);
            }
            else if (_flowDocFactory is not null)
            {
                var doc = _flowDocFactory();
                doc.PageWidth = printableW;
                doc.PageHeight = printableH;
                doc.ColumnWidth = printableW;
                printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, _documentTitle);
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not print document: {ex.Message}", "Print Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }
}

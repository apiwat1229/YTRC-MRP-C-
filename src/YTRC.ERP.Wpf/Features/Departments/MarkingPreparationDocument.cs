using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Printing;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private void OpenMarkingPreparationPreview(MarkingPreparationListItem report)
    {
        var previewWindow = new Window
        {
            Title = $"Marking Preparation Report Preview · {report.MonthLabel}",
            Owner = this,
            Width = 920,
            Height = Math.Min(980, SystemParameters.WorkArea.Height - 40),
            MinHeight = 500,
            MinWidth = 700,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = CreateBrush("#F1F5F9")
        };

        var rootGrid = new Grid();
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Toolbar
        var toolbar = new Border
        {
            Background = Brushes.White,
            BorderBrush = CreateBrush("#E2E8F0"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var toolbarDock = new DockPanel();

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.DocumentBulletList,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = CreateBrush("#0F766E"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = $"Preview · Marking Preparation Report ({report.MonthLabel})",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = CreateBrush("#1E293B"),
            VerticalAlignment = VerticalAlignment.Center
        });
        toolbarDock.Children.Add(titleStack);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DockPanel.SetDock(actions, Dock.Right);

        // Document Scroller
        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0)
        };

        var paper = BuildMarkingPreparationDocumentVisual(report.Raw);
        paper.HorizontalAlignment = HorizontalAlignment.Center;
        paper.VerticalAlignment = VerticalAlignment.Top;

        var scaleTransform = new ScaleTransform(1.0, 1.0);

        // Paper elevation shadow
        var shadowBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = CreateBrush("#CBD5E1"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 24),
            LayoutTransform = scaleTransform,
            Child = paper
        };

        var zoomWrapper = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(24)
        };
        zoomWrapper.Children.Add(shadowBorder);

        scroller.Content = zoomWrapper;
        Grid.SetRow(scroller, 1);
        rootGrid.Children.Add(scroller);

        // Zoom state and logic
        double currentZoom = 1.0;
        const double minZoom = 0.35;
        const double maxZoom = 4.0;

        Button? zoomLabel = null;

        void SetZoom(double targetZoom, Point? anchor = null)
        {
            targetZoom = Math.Clamp(targetZoom, minZoom, maxZoom);
            targetZoom = Math.Round(targetZoom, 2);
            if (Math.Abs(targetZoom - currentZoom) < 0.001) return;

            var oldZoom = currentZoom;
            currentZoom = targetZoom;

            Point mousePos;
            if (anchor is { } pt && pt.X >= 0 && pt.X <= scroller.ViewportWidth && pt.Y >= 0 && pt.Y <= scroller.ViewportHeight)
            {
                mousePos = pt;
            }
            else
            {
                mousePos = new Point(Math.Max(0, scroller.ViewportWidth / 2.0), Math.Max(0, scroller.ViewportHeight / 2.0));
            }

            var relX = scroller.HorizontalOffset + mousePos.X;
            var relY = scroller.VerticalOffset + mousePos.Y;

            scaleTransform.ScaleX = currentZoom;
            scaleTransform.ScaleY = currentZoom;

            scroller.UpdateLayout();

            var ratio = currentZoom / oldZoom;
            var newOffsetX = (relX * ratio) - mousePos.X;
            var newOffsetY = (relY * ratio) - mousePos.Y;

            scroller.ScrollToHorizontalOffset(Math.Max(0, newOffsetX));
            scroller.ScrollToVerticalOffset(Math.Max(0, newOffsetY));

            if (zoomLabel != null)
            {
                zoomLabel.Content = $"{(int)Math.Round(currentZoom * 100)}%";
            }
        }

        // Zoom Toolbar Controls
        var zoomControls = new Border
        {
            Background = CreateBrush("#F8FAFC"),
            BorderBrush = CreateBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(2),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var zoomStack = new StackPanel { Orientation = Orientation.Horizontal };

        Button CreateZoomButton(string text, string toolTip, Action onClick, double minWidth = 28)
        {
            var btn = new Button
            {
                Content = text,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                MinWidth = minWidth,
                Height = 28,
                Padding = new Thickness(4, 0, 4, 0),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = CreateBrush("#475569"),
                Cursor = Cursors.Hand,
                ToolTip = toolTip
            };
            UiControlProperties.SetButtonCornerRadius(btn, new CornerRadius(4));
            btn.MouseEnter += (_, _) =>
            {
                btn.Background = CreateBrush("#E2E8F0");
                btn.Foreground = CreateBrush("#0F766E");
            };
            btn.MouseLeave += (_, _) =>
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = CreateBrush("#475569");
            };
            btn.Click += (_, _) => onClick();
            return btn;
        }

        var zoomOutBtn = CreateZoomButton("−", "Zoom out (Ctrl + Mouse wheel down / Ctrl -)", () => SetZoom(currentZoom / 1.15));
        zoomLabel = CreateZoomButton("100%", "Reset to 100% (Ctrl + 0)", () => SetZoom(1.0), minWidth: 48);
        zoomLabel.Foreground = CreateBrush("#0F172A");
        zoomLabel.MouseLeave += (_, _) =>
        {
            zoomLabel.Background = Brushes.Transparent;
            zoomLabel.Foreground = CreateBrush("#0F172A");
        };
        var zoomInBtn = CreateZoomButton("+", "Zoom in (Ctrl + Mouse wheel up / Ctrl +)", () => SetZoom(currentZoom * 1.15));

        var fitWidthBtn = new Button
        {
            Content = "Fit Width",
            FontSize = 11,
            FontWeight = FontWeights.Medium,
            Height = 28,
            Padding = new Thickness(8, 0, 8, 0),
            BorderThickness = new Thickness(1, 0, 0, 0),
            BorderBrush = CreateBrush("#E2E8F0"),
            Background = Brushes.Transparent,
            Foreground = CreateBrush("#475569"),
            Cursor = Cursors.Hand,
            Margin = new Thickness(2, 0, 0, 0),
            ToolTip = "Fit document to window width"
        };
        UiControlProperties.SetButtonCornerRadius(fitWidthBtn, new CornerRadius(4));
        fitWidthBtn.MouseEnter += (_, _) =>
        {
            fitWidthBtn.Background = CreateBrush("#E2E8F0");
            fitWidthBtn.Foreground = CreateBrush("#0F766E");
            fitWidthBtn.FontWeight = FontWeights.SemiBold;
        };
        fitWidthBtn.MouseLeave += (_, _) =>
        {
            fitWidthBtn.Background = Brushes.Transparent;
            fitWidthBtn.Foreground = CreateBrush("#475569");
            fitWidthBtn.FontWeight = FontWeights.Medium;
        };
        fitWidthBtn.Click += (_, _) =>
        {
            var availableWidth = scroller.ActualWidth - 64;
            if (availableWidth > 150)
            {
                SetZoom(availableWidth / 794.0);
            }
        };

        zoomStack.Children.Add(zoomOutBtn);
        zoomStack.Children.Add(zoomLabel);
        zoomStack.Children.Add(zoomInBtn);
        zoomStack.Children.Add(fitWidthBtn);
        zoomControls.Child = zoomStack;
        actions.Children.Add(zoomControls);

        var exportExcelBtn = new Button
        {
            Content = new ExcelIcon { Width = 18, Height = 18 },
            Width = UiTokens.ToolbarButtonSize + 2,
            MinWidth = UiTokens.ToolbarButtonSize + 2,
            Height = UiTokens.ButtonHeight,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Export report to Excel (.xlsx)",
            Style = (Style)FindResource(typeof(Button)),
            Background = Brushes.White,
            BorderBrush = CreateBrush("#E2E8F0"),
            BorderThickness = new Thickness(1)
        };
        UiControlProperties.SetButtonCornerRadius(exportExcelBtn, UiTokens.CornerRadiusControl);
        exportExcelBtn.MouseEnter += (_, _) =>
        {
            exportExcelBtn.Background = CreateBrush("#F0FDF4");
            exportExcelBtn.BorderBrush = CreateBrush("#16A34A");
        };
        exportExcelBtn.MouseLeave += (_, _) =>
        {
            exportExcelBtn.Background = Brushes.White;
            exportExcelBtn.BorderBrush = CreateBrush("#E2E8F0");
        };
        exportExcelBtn.PreviewMouseDown += (_, _) => exportExcelBtn.Background = CreateBrush("#DCFCE7");
        exportExcelBtn.PreviewMouseUp += (_, _) => exportExcelBtn.Background = exportExcelBtn.IsMouseOver ? CreateBrush("#F0FDF4") : Brushes.White;
        exportExcelBtn.Click += (_, _) => ExportMarkingPreparationToExcel(report);
        actions.Children.Add(exportExcelBtn);

        var printBtn = MarkingPreparationButton("Print", FluentIcons.Common.Icon.Print, primary: true);
        printBtn.Margin = new Thickness(0);
        printBtn.Background = Brushes.White;
        printBtn.BorderBrush = CreateBrush("#E2E8F0");
        printBtn.BorderThickness = new Thickness(1);
        printBtn.ToolTip = "Print report (Ctrl + P)";
        UiControlProperties.SetButtonCornerRadius(printBtn, UiTokens.CornerRadiusControl);

        var printIcon = printBtn.Content as FluentIcon;
        printBtn.MouseEnter += (_, _) =>
        {
            printBtn.Background = CreateBrush("#F0FDFA");
            printBtn.BorderBrush = CreateBrush("#0F766E");
            if (printIcon is not null) printIcon.Foreground = CreateBrush("#0F766E");
        };
        printBtn.MouseLeave += (_, _) =>
        {
            printBtn.Background = Brushes.White;
            printBtn.BorderBrush = CreateBrush("#E2E8F0");
            if (printIcon is not null) printIcon.Foreground = UiTokens.Brushes.TextMuted;
        };
        printBtn.PreviewMouseDown += (_, _) =>
        {
            printBtn.Background = CreateBrush("#DDEDE9");
        };
        printBtn.PreviewMouseUp += (_, _) =>
        {
            printBtn.Background = printBtn.IsMouseOver ? CreateBrush("#F0FDFA") : Brushes.White;
        };
        printBtn.Click += (_, _) => PrintMarkingPreparationVisual(report);
        actions.Children.Add(printBtn);

        toolbarDock.Children.Add(actions);
        toolbar.Child = toolbarDock;
        Grid.SetRow(toolbar, 0);
        rootGrid.Children.Add(toolbar);

        // Ctrl + Mouse Wheel & Shortcuts
        previewWindow.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                var factor = Math.Pow(1.0012, e.Delta);
                var mousePos = e.GetPosition(scroller);
                SetZoom(currentZoom * factor, mousePos);
            }
            else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                if (e.Delta > 0)
                    scroller.LineLeft();
                else
                    scroller.LineRight();
            }
        };

        previewWindow.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                previewWindow.Close();
                e.Handled = true;
                return;
            }

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                if (e.Key is Key.OemPlus or Key.Add)
                {
                    SetZoom(currentZoom * 1.15);
                    e.Handled = true;
                }
                else if (e.Key is Key.OemMinus or Key.Subtract)
                {
                    SetZoom(currentZoom / 1.15);
                    e.Handled = true;
                }
                else if (e.Key is Key.D0 or Key.NumPad0)
                {
                    SetZoom(1.0);
                    e.Handled = true;
                }
                else if (e.Key is Key.P)
                {
                    PrintMarkingPreparationVisual(report);
                    e.Handled = true;
                }
            }
        };

        previewWindow.Content = rootGrid;
        previewWindow.ShowDialog();
    }

    private void PrintMarkingPreparationVisual(MarkingPreparationListItem report)
    {
        AppPrintDialog.ShowPrint(
            owner: this,
            documentTitle: $"Marking Preparation Report ({report.MonthLabel})",
            visualFactoryWithOptions: (w, h, orient) => BuildMarkingPreparationDocumentVisual(report.Raw, w, h, orient),
            defaultOrientation: PageOrientation.Portrait,
            defaultPaperSize: PageMediaSizeName.ISOA4);
    }

    private FrameworkElement BuildMarkingPreparationDocumentVisual(
        JsonElement report,
        double pageWidth = 794,
        double pageHeight = 1123,
        PageOrientation orientation = PageOrientation.Portrait)
    {
        var isLandscape = orientation == PageOrientation.Landscape || pageWidth > pageHeight;
        var page = new Border
        {
            Width = pageWidth,
            Height = pageHeight,
            Background = Brushes.White,
            Padding = isLandscape ? new Thickness(20, 14, 20, 14) : new Thickness(24, 20, 24, 20),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true
        };

        var outerFrame = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1.4),
            Padding = isLandscape ? new Thickness(8, 6, 8, 6) : new Thickness(10, 8, 10, 8)
        };

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Data table
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

        // ==========================================
        // 1. TOP HEADER SECTION
        // ==========================================
        var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };

        var titleBlock = new TextBlock
        {
            Text = "Marking Preparation Report",
            FontSize = 14.5,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Arial"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        headerPanel.Children.Add(titleBlock);

        var subHeaderGrid = new Grid();
        subHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        subHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left sub-header
        var leftHeader = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) };
        var creatorFirstName = GetReportCreatorFirstName(report);
        leftHeader.Children.Add(new TextBlock
        {
            Text = $"User : {creatorFirstName}",
            FontSize = 8.2,
            FontFamily = new FontFamily("Arial"),
            Margin = new Thickness(0, 0, 0, 5)
        });

        var monthRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        monthRow.Children.Add(new TextBlock
        {
            Text = "Report Month : ",
            FontWeight = FontWeights.Bold,
            FontSize = 9.0,
            FontFamily = new FontFamily("Arial"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var monthDate = ReadDate(report, "reportMonth");
        var monthFormatted = monthDate?.ToString("MMM-yy", CultureInfo.InvariantCulture) ?? "";
        var monthBorder = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.8),
            Padding = new Thickness(14, 1, 14, 1),
            Margin = new Thickness(4, 0, 0, 0),
            Child = new TextBlock
            {
                Text = monthFormatted,
                FontWeight = FontWeights.Bold,
                FontSize = 9.0,
                FontFamily = new FontFamily("Arial"),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
        monthRow.Children.Add(monthBorder);
        leftHeader.Children.Add(monthRow);

        Grid.SetColumn(leftHeader, 0);
        subHeaderGrid.Children.Add(leftHeader);

        // Right sub-header: Revisions Table
        var revTable = new Grid { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 2) };
        revTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) }); // Revision.
        revTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) }); // Date Report
        revTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(186) }); // Remark.

        // Revisions Header
        revTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        AddDocCell(revTable, 0, 0, "Revision.", bold: true, center: true, fontSize: 8.0);
        AddDocCell(revTable, 0, 1, "Date Report", bold: true, center: true, fontSize: 8.0);
        AddDocCell(revTable, 0, 2, "Remark.", bold: true, center: true, fontSize: 8.0);

        var revisions = ReadJsonArray(report, "revisions");
        var revCount = 0;
        foreach (var rev in revisions.EnumerateArray())
        {
            revCount++;
            revTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            AddDocCell(revTable, revCount, 0, ReadString(rev, "revision"), bold: true, center: true, fontSize: 8.0);
            AddDocCell(revTable, revCount, 1, FormatDate(rev, "reportDate"), center: true, fontSize: 8.0);
            AddDocCell(revTable, revCount, 2, ReadString(rev, "remark"), fontSize: 8.0);
        }
        while (revCount < 2)
        {
            revCount++;
            revTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
            AddDocCell(revTable, revCount, 0, "", center: true, fontSize: 8.0);
            AddDocCell(revTable, revCount, 1, "", center: true, fontSize: 8.0);
            AddDocCell(revTable, revCount, 2, "", fontSize: 8.0);
        }

        Grid.SetColumn(revTable, 1);
        subHeaderGrid.Children.Add(revTable);

        headerPanel.Children.Add(subHeaderGrid);
        Grid.SetRow(headerPanel, 0);
        contentGrid.Children.Add(headerPanel);

        // ==========================================
        // 2. MAIN DATA TABLE
        // ==========================================
        var mainTable = new Grid { VerticalAlignment = VerticalAlignment.Top };
        if (!isLandscape)
        {
            // Total usable width ~726px for Portrait
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });  // 0: No
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });  // 1: Month
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(144) }); // 2: Joint Contract Name
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // 3: Code
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });  // 4: Dest.
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });  // 5: Grade
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });  // 6: Unit
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });  // 7: Gross
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });  // 8: Net.
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });  // 9: Package
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(68) });  // 10: Marking Due Date
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // 11: Sign (under Status)
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });  // 12: Date (under Status)
        }
        else
        {
            // Usable width ~1058px for Landscape (wide readable table)
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });  // 0: No
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });  // 1: Month
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) }); // 2: Joint Contract Name
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // 3: Code
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) }); // 4: Dest.
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // 5: Grade
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // 6: Unit
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(66) });  // 7: Gross
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });  // 8: Net.
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });  // 9: Package
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(95) });  // 10: Marking Due Date
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 11: Sign (under Status)
            mainTable.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(75) });  // 12: Date (under Status)
        }

        // Header Rows (2 rows)
        mainTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); // Row 0
        mainTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) }); // Row 1

        // RowSpan 2 headers
        AddDocCell(mainTable, 0, 0, "No", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 1, "Month", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 2, "Joint Contract Name", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 3, "Code", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 4, "Dest.", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 5, "Grade", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 6, "Unit", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 7, "Gross", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 8, "Net.", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 9, "Package", rowSpan: 2, bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 0, 10, "Marking\nDue Date", rowSpan: 2, bold: true, center: true, fontSize: 7.2);

        // Status group header (ColSpan 2)
        AddDocCell(mainTable, 0, 11, "Status", colSpan: 2, bold: true, center: true, fontSize: 8.0);
        // Status sub-headers in Row 1
        AddDocCell(mainTable, 1, 11, "Sign", bold: true, center: true, fontSize: 8.0);
        AddDocCell(mainTable, 1, 12, "Date", bold: true, center: true, fontSize: 8.0);

        // Detail Rows
        var details = ReadJsonArray(report, "details");
        var detailCount = 0;
        foreach (var _ in details.EnumerateArray()) detailCount++;

        double rowHeight;
        double baseFontSize;
        if (isLandscape)
        {
            // In Landscape A4 height is ~794, so allocate row height to fit all records cleanly without clipping
            var reservedHeaderFooterHeight = 175.0;
            var availableTableHeight = pageHeight - reservedHeaderFooterHeight;
            rowHeight = detailCount > 0 ? Math.Min(16.5, Math.Max(12.2, availableTableHeight / (detailCount + 2))) : 16.0;
            baseFontSize = rowHeight < 13.5 ? 7.0 : (rowHeight < 15.0 ? 7.6 : 8.2);
        }
        else
        {
            rowHeight = 17.0;
            baseFontSize = 8.2;
        }

        var rowIndex = 2;
        var rowNo = 0;
        foreach (var detail in details.EnumerateArray())
        {
            rowNo++;
            mainTable.RowDefinitions.Add(new RowDefinition { Height = new GridLength(rowHeight) });

            var rowMonth = ReadString(detail, "monthLabel");
            if (string.IsNullOrWhiteSpace(rowMonth)) rowMonth = monthFormatted;

            AddDocCell(mainTable, rowIndex, 0, rowNo.ToString(CultureInfo.InvariantCulture), center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 1, rowMonth, center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 2, ReadString(detail, "jointContractName"), paddingLeft: 3, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 3, MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "code")), center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 4, MarkingPreparationCatalogHelper.CleanValue(ReadString(detail, "destination")), paddingLeft: 3, fontSize: baseFontSize);
            var gradeText = ReadString(detail, "grade");
            AddDocCell(mainTable, rowIndex, 5, gradeText, center: true, bold: true, fontSize: baseFontSize,
                background: GradeColorHelper.GetBackgroundBrush(gradeText),
                foreground: GradeColorHelper.GetForegroundBrush(gradeText));
            AddDocCell(mainTable, rowIndex, 6, ReadString(detail, "unit"), center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 7, FormatWeight(detail, "grossWeight"), paddingRight: 3, right: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 8, FormatWeight(detail, "netWeight"), paddingRight: 3, right: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 9, ReadString(detail, "packageType"), center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 10, FormatDueDate(detail, "markingDueDate"), center: true, fontSize: Math.Max(6.8, baseFontSize - 0.4));
            AddDocCell(mainTable, rowIndex, 11, ReadString(detail, "markingSign"), center: true, fontSize: baseFontSize);
            AddDocCell(mainTable, rowIndex, 12, FormatDate(detail, "statusDate"), center: true, fontSize: baseFontSize);

            rowIndex++;
        }

        Grid.SetRow(mainTable, 1);
        contentGrid.Children.Add(mainTable);

        // ==========================================
        // 3. FOOTER SIGN-OFF SECTION
        // ==========================================
        var footerGrid = new Grid { Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var footerLeft = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 1)
        };
        var printUser = GetCurrentPrintUser();
        var printDate = DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        footerLeft.Children.Add(new TextBlock
        {
            Text = $"User Print : {printUser}",
            FontSize = 7.8,
            FontWeight = FontWeights.Normal,
            Foreground = CreateBrush("#64748B"),
            FontFamily = new FontFamily("Arial")
        });
        footerLeft.Children.Add(new TextBlock
        {
            Text = $"Date : {printDate}",
            FontSize = 7.8,
            FontWeight = FontWeights.Normal,
            Foreground = CreateBrush("#64748B"),
            FontFamily = new FontFamily("Arial"),
            Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(footerLeft, 0);
        footerGrid.Children.Add(footerLeft);

        // Approval Table
        var approvalGrid = new Grid { HorizontalAlignment = HorizontalAlignment.Right };
        approvalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) }); // Issue By
        approvalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) }); // Department signs
        approvalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // Dates

        approvalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        approvalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
        approvalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });

        // Issue By (RowSpan 3)
        var issueBy = ReadString(report, "issueBy");
        var issueDate = FormatDate(report, "issueDate");
        var issuePanel = new DockPanel { Margin = new Thickness(3, 2, 3, 2) };
        issuePanel.Children.Add(new TextBlock
        {
            Text = $"Issue By : {issueBy}",
            FontSize = 7.8,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Arial")
        });
        var dateText = new TextBlock
        {
            Text = $"Date : {issueDate}",
            FontSize = 7.8,
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Arial")
        };
        DockPanel.SetDock(dateText, Dock.Bottom);
        issuePanel.Children.Add(dateText);

        var issueBorder = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.6),
            Child = issuePanel
        };
        Grid.SetRow(issueBorder, 0);
        Grid.SetRowSpan(issueBorder, 3);
        Grid.SetColumn(issueBorder, 0);
        approvalGrid.Children.Add(issueBorder);

        // Production Admin
        AddDocCell(approvalGrid, 0, 1, "Production Admin: " + ReadString(report, "productionAdminSign"), bold: true, fontSize: 7.8, paddingLeft: 3);
        AddDocCell(approvalGrid, 0, 2, "Date: " + FormatDate(report, "productionAdminDate"), bold: true, fontSize: 7.8, paddingLeft: 3);

        // QAC
        AddDocCell(approvalGrid, 1, 1, "QAC: " + ReadString(report, "qacSign"), bold: true, fontSize: 7.8, paddingLeft: 3);
        AddDocCell(approvalGrid, 1, 2, "Date: " + FormatDate(report, "qacDate"), bold: true, fontSize: 7.8, paddingLeft: 3);

        // WH
        AddDocCell(approvalGrid, 2, 1, "W/H : " + ReadString(report, "warehouseSign"), bold: true, fontSize: 7.8, paddingLeft: 3);
        AddDocCell(approvalGrid, 2, 2, "Date: " + FormatDate(report, "warehouseDate"), bold: true, fontSize: 7.8, paddingLeft: 3);

        Grid.SetColumn(approvalGrid, 1);
        footerGrid.Children.Add(approvalGrid);

        Grid.SetRow(footerGrid, 2);
        contentGrid.Children.Add(footerGrid);

        outerFrame.Child = contentGrid;
        page.Child = outerFrame;
        return page;
    }

    private static void AddDocCell(
        Grid grid, int row, int col, string text,
        int rowSpan = 1, int colSpan = 1,
        bool bold = false, bool center = false, bool right = false,
        double fontSize = 7, double paddingLeft = 0, double paddingRight = 0,
        Brush? background = null, Brush? foreground = null)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = new FontFamily("Arial"),
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            Foreground = foreground ?? Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = center ? TextAlignment.Center : (right ? TextAlignment.Right : TextAlignment.Left),
            Margin = new Thickness(paddingLeft > 0 ? paddingLeft : 1, 0, paddingRight > 0 ? paddingRight : 1, 0)
        };

        var border = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0.55),
            Background = background ?? Brushes.White,
            Child = textBlock
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        if (rowSpan > 1) Grid.SetRowSpan(border, rowSpan);
        if (colSpan > 1) Grid.SetColumnSpan(border, colSpan);
        grid.Children.Add(border);
    }

    private string GetReportCreatorFirstName(JsonElement report)
    {
        string[] candidates = ["createdBy", "created_by", "createdByName", "created_by_name", "creator", "author", "createdUser", "user"];
        foreach (var key in candidates)
        {
            if (report.TryGetProperty(key, out var val))
            {
                if (val.ValueKind == JsonValueKind.String)
                {
                    var s = val.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(s)) return ExtractFirstName(s);
                }
                else if (val.ValueKind == JsonValueKind.Object)
                {
                    if (val.TryGetProperty("firstName", out var fn) && fn.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(fn.GetString()))
                        return fn.GetString()!.Trim();
                    if (val.TryGetProperty("first_name", out var fn2) && fn2.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(fn2.GetString()))
                        return fn2.GetString()!.Trim();
                    if (val.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(dn.GetString()))
                        return ExtractFirstName(dn.GetString());
                    if (val.TryGetProperty("fullName", out var full) && full.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(full.GetString()))
                        return ExtractFirstName(full.GetString());
                    if (val.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(un.GetString()))
                        return un.GetString()!.Trim();
                }
            }
        }

        var printedBy = ReadString(report, "printedBy");
        if (!string.IsNullOrWhiteSpace(printedBy)) return ExtractFirstName(printedBy);

        var issueBy = ReadString(report, "issueBy");
        if (!string.IsNullOrWhiteSpace(issueBy)) return ExtractFirstName(issueBy);

        var userFirst = _session.User?.FirstName;
        if (!string.IsNullOrWhiteSpace(userFirst)) return userFirst;
        var fullName = _session.User?.FullName;
        if (!string.IsNullOrWhiteSpace(fullName)) return ExtractFirstName(fullName);
        return _session.User?.Username ?? "admin";
    }

    private static string ExtractFirstName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var trimmed = name.Trim();
        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : trimmed;
    }

    private string GetCurrentPrintUser()
    {
        var first = _session.User?.FirstName;
        if (!string.IsNullOrWhiteSpace(first)) return first;
        var fullName = _session.User?.FullName;
        if (!string.IsNullOrWhiteSpace(fullName)) return ExtractFirstName(fullName);
        return _session.User?.Username ?? "admin";
    }
}

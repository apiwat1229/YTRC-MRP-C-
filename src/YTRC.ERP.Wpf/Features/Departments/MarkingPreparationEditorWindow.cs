using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Data;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

internal sealed class MarkingPreparationEditorWindow : Window
{
    private const double EditorDatePickerWidth = UiTokens.DatePickerWidth;
    private const double EditorDatePickerHeight = UiTokens.DatePickerHeight;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly EditorState _state;
    private readonly bool _isReadOnly;
    private readonly ObservableCollection<RevisionDraft> _revisions;
    private readonly ObservableCollection<DetailDraft> _details;
    private readonly Dictionary<string, IReadOnlyList<string>> _codeDestinations;
    private readonly IReadOnlyList<string> _codeOptions;
    private readonly IReadOnlyList<string> _gradeOptions;
    private readonly IReadOnlyList<string> _packageOptions;
    private readonly bool _isNewReport;
    private readonly string _originalDetailFingerprint;
    private readonly HashSet<string> _originalRevisionNumbers;
    private readonly BookingQueueDatePicker _reportMonth;
    private readonly DataGrid _revisionGrid;
    private readonly DataGrid _detailGrid;
    private TextBlock _totalLinesText = null!;
    private TextBlock _totalGrossText = null!;
    private TextBlock _totalNetText = null!;
    private TextBlock _detailLineCountBadge = null!;

    public object? Payload { get; private set; }

    public MarkingPreparationEditorWindow(
        JsonElement? source,
        JsonElement catalog,
        string defaultCreatedBy = "",
        string defaultPrintedBy = "",
        bool isReadOnly = false,
        IReadOnlyList<JsonElement>? selectedSiLines = null)
    {
        _isReadOnly = isReadOnly;
        (_codeDestinations, _codeOptions, _gradeOptions, _packageOptions) = ReadCatalog(catalog);
        _isNewReport = source is null;
        _state = source is { ValueKind: JsonValueKind.Object } json
            ? JsonSerializer.Deserialize<EditorState>(json.GetRawText(), JsonOptions) ?? new EditorState()
            : new EditorState
            {
                ReportMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
                CreatedBy = defaultCreatedBy,
                PrintedBy = defaultPrintedBy,
                Revisions = [new RevisionDraft { Revision = "00", ReportDate = DateTime.Today }]
            };
        if (string.IsNullOrWhiteSpace(_state.CreatedBy) && !string.IsNullOrWhiteSpace(defaultCreatedBy))
        {
            _state.CreatedBy = defaultCreatedBy;
        }
        _state.ReportMonth = _state.ReportMonth == default
            ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
            : new DateTime(_state.ReportMonth.Year, _state.ReportMonth.Month, 1);
        _state.Revisions ??= [];
        _state.Details ??= [];
        if (_isNewReport && selectedSiLines is { Count: > 0 } &&
            ReadDate(selectedSiLines[0], "shipmentMonth") is { } shipmentMonth)
        {
            _state.ReportMonth = new DateTime(shipmentMonth.Year, shipmentMonth.Month, 1);
        }
        if (_state.Revisions.Count == 0)
        {
            _state.Revisions.Add(new RevisionDraft
            {
                Revision = "00",
                ReportDate = _isNewReport ? DateTime.Today : _state.CreatedAt?.Date ?? DateTime.Today
            });
        }
        _revisions = new ObservableCollection<RevisionDraft>(_state.Revisions);
        _details = new ObservableCollection<DetailDraft>(_state.Details);
        foreach (var detail in _details)
            detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
        _originalDetailFingerprint = DetailFingerprint(_details);
        if (selectedSiLines is not null)
        {
            foreach (var sourceLine in selectedSiLines)
            {
                var detail = CreateDetailFromSICandidate(sourceLine);
                detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
                _details.Add(detail);
            }
        }
        if (_isNewReport && _details.Count == 0)
        {
            var firstDetail = new DetailDraft();
            firstDetail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
            _details.Add(firstDetail);
        }
        _originalRevisionNumbers = _revisions
            .Where(item => !string.IsNullOrWhiteSpace(item.Revision))
            .Select(item => item.Revision!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Title = _isReadOnly
            ? $"View Marking Preparation Report · {_state.ReportMonth:MMM yyyy}"
            : source is null ? "New Marking Preparation Report" : "Edit Marking Preparation Report";
        Width = 1420;
        Height = 780;
        MinWidth = 1080;
        MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        Background = Brush("#F8FAFC");

        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Row 0: Top Header Card
        var headerCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var headerDock = new DockPanel();

        var rightControls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(rightControls, Dock.Right);

        _reportMonth = CreateDatePicker(_state.ReportMonth);
        _reportMonth.MonthSelectionOnly = true;
        _reportMonth.DateDisplayFormat = "MMM yyyy";
        _reportMonth.ToolTip = "Report month · first day of the month";
        _reportMonth.Margin = new Thickness(0);
        if (_isReadOnly)
        {
            _reportMonth.IsEnabled = false;
            _reportMonth.Focusable = false;
        }
        var monthStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        monthStack.Children.Add(new TextBlock
        {
            Text = "Report Month",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        monthStack.Children.Add(_reportMonth);
        rightControls.Children.Add(monthStack);
        headerDock.Children.Add(rightControls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBorder = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = UiTokens.CornerRadiusSmall,
            Background = UiTokens.Brushes.MintSurface,
            BorderBrush = UiTokens.Brushes.MintBorder,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Child = new FluentIcon
            {
                Icon = FluentIcons.Common.Icon.DocumentBulletList,
                IconSize = IconSize.Size20,
                IconVariant = IconVariant.Regular,
                Foreground = UiTokens.Brushes.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        leftStack.Children.Add(iconBorder);

        var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBlock.Children.Add(new TextBlock
        {
            Text = _isReadOnly
                ? "View Marking Preparation Report"
                : _isNewReport ? "New Marking Preparation Report" : "Edit Marking Preparation Report",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("#0F172A"),
            FontFamily = (FontFamily)FindResource("AppFontFamily")
        });
        leftStack.Children.Add(titleBlock);
        headerDock.Children.Add(leftStack);

        headerCard.Child = headerDock;
        if (!_isReadOnly)
        {
            root.Children.Add(headerCard);
            Grid.SetRow(headerCard, 0);
        }

        // Row 1: Revision History Card
        var revisionCard = BuildRevisionSection(out _revisionGrid);
        root.Children.Add(revisionCard);
        Grid.SetRow(revisionCard, 1);

        // Row 2: Marking Details Card
        var detailCard = BuildDetailSection(out _detailGrid);
        root.Children.Add(detailCard);
        Grid.SetRow(detailCard, 2);

        // Row 3: Footer Bar
        var footerCard = BuildFooterSection();
        root.Children.Add(footerCard);
        Grid.SetRow(footerCard, 3);

        _details.CollectionChanged += (_, _) => UpdateFooterMetrics();
        _detailGrid.CellEditEnding += (_, _) => Dispatcher.InvokeAsync(UpdateFooterMetrics);
        UpdateFooterMetrics();

        Content = root;
    }

    private Border BuildRevisionSection(out DataGrid grid)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var gridLayout = new Grid();
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(controls, Dock.Right);

        var add = CreateButton("Add revision");
        add.Click += (_, _) => _revisions.Add(new RevisionDraft { Revision = NextRevision(_revisions), ReportDate = DateTime.Today });

        DataGrid? localGrid = null;
        var remove = CreateButton("Remove revision");
        remove.Click += (_, _) =>
        {
            if (localGrid?.SelectedItem is not RevisionDraft selected) return;
            if (selected.Revision == "00")
            {
                ShowValidation("The initial revision 00 must remain in the report.");
                return;
            }
            _revisions.Remove(selected);
        };
        controls.Children.Add(add);
        controls.Children.Add(remove);
        if (_isReadOnly)
        {
            controls.Children.Clear();
            var monthBadge = new Border
            {
                Background = Brush("#F1F5F9"),
                BorderBrush = Brush("#CBD5E1"),
                BorderThickness = new Thickness(1),
                CornerRadius = UiTokens.CornerRadiusSmall,
                Padding = new Thickness(10, 3, 10, 3),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new FluentIcon
                        {
                            Icon = FluentIcons.Common.Icon.Calendar,
                            IconSize = IconSize.Size16,
                            IconVariant = IconVariant.Regular,
                            Foreground = Brush("#0F766E"),
                            Margin = new Thickness(0, 0, 6, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = $"Report Month: {_state.ReportMonth:MMM yyyy}",
                            FontSize = 12,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = Brush("#1E293B"),
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            controls.Children.Add(monthBadge);
            controls.Visibility = Visibility.Visible;
        }
        header.Children.Add(controls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftStack.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.History,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = Brush("#0F766E"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Revision History",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Report publication dates and modification logs",
            FontSize = 11,
            Foreground = Brush("#64748B"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(leftStack);

        gridLayout.Children.Add(header);
        Grid.SetRow(header, 0);

        grid = new DataGrid
        {
            ItemsSource = _revisions,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            IsReadOnly = _isReadOnly,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = Brush("#F8FAFC"),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = Brush("#F1F5F9"),
            VerticalGridLinesBrush = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            FontSize = 12,
            MinRowHeight = 30,
            MaxHeight = 168,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        localGrid = grid;
        AddRevisionColumns(grid);
        if (!_isReadOnly) EnableSingleClickEditing(grid);

        gridLayout.Children.Add(grid);
        Grid.SetRow(grid, 1);

        card.Child = gridLayout;
        return card;
    }

    private Border BuildDetailSection(out DataGrid grid)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var gridLayout = new Grid();
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        gridLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

        var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(controls, Dock.Right);

        _detailLineCountBadge = new TextBlock
        {
            Text = _details.Count == 1 ? "1 line" : $"{_details.Count} lines",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#475569"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var badgeBorder = new Border
        {
            Background = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _detailLineCountBadge
        };
        controls.Children.Add(badgeBorder);

        var add = CreateButton("Add line");
        add.Click += (_, _) =>
        {
            var detail = new DetailDraft();
            detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
            _details.Add(detail);
        };

        DataGrid? localGrid = null;
        var remove = CreateButton("Remove line");
        remove.Click += (_, _) =>
        {
            if (localGrid?.SelectedItem is DetailDraft selected)
                _details.Remove(selected);
        };
        add.Visibility = Visibility.Collapsed;
        if (_isReadOnly) remove.Visibility = Visibility.Collapsed;
        controls.Children.Add(add);
        controls.Children.Add(remove);
        header.Children.Add(controls);

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftStack.Children.Add(new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Table,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = Brush("#0F766E"),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        leftStack.Children.Add(new TextBlock
        {
            Text = "Marking Details & Specifications",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(leftStack);

        gridLayout.Children.Add(header);
        Grid.SetRow(header, 0);

        grid = new DataGrid
        {
            ItemsSource = _details,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = !_isReadOnly,
            IsReadOnly = _isReadOnly,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            RowBackground = Brushes.White,
            AlternatingRowBackground = Brush("#F8FAFC"),
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            HorizontalGridLinesBrush = Brush("#F1F5F9"),
            VerticalGridLinesBrush = Brush("#F1F5F9"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            FontSize = 12,
            RowHeight = 34,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        localGrid = grid;
        grid.InitializingNewItem += (_, eventArgs) =>
        {
            if (eventArgs.NewItem is DetailDraft detail)
                detail.ConfigureCatalog(_codeDestinations, _codeOptions, _gradeOptions, _packageOptions);
        };
        grid.LoadingRow += (_, eventArgs) => eventArgs.Row.Header = (eventArgs.Row.GetIndex() + 1).ToString(CultureInfo.InvariantCulture);
        AddDetailColumns(grid);
        if (!_isReadOnly) EnableSingleClickEditing(grid);

        gridLayout.Children.Add(grid);
        Grid.SetRow(grid, 1);

        card.Child = gridLayout;
        return card;
    }

    private Border BuildFooterSection()
    {
        var footerCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(14, 10, 14, 10)
        };

        var dock = new DockPanel();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(buttons, Dock.Right);

        var cancel = CreateButton(_isReadOnly ? "Close" : "Cancel");
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(cancel);

        if (!_isReadOnly)
        {
            var save = CreateButton("Save report", primary: true);
            save.Click += (_, _) => SaveReport();
            buttons.Children.Add(save);
        }
        dock.Children.Add(buttons);

        var metrics = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        _totalLinesText = new TextBlock { Text = _details.Count.ToString(CultureInfo.InvariantCulture), FontWeight = FontWeights.Bold, Foreground = Brush("#0F172A") };
        _totalGrossText = new TextBlock { Text = "0", FontWeight = FontWeights.Bold, Foreground = Brush("#0F172A") };
        _totalNetText = new TextBlock { Text = "0", FontWeight = FontWeights.Bold, Foreground = Brush("#0F766E") };

        metrics.Children.Add(CreateMetricPill("Lines", _totalLinesText));
        metrics.Children.Add(CreateMetricPill("Total Gross", _totalGrossText, " MT"));
        metrics.Children.Add(CreateMetricPill("Total Net", _totalNetText, " MT"));

        dock.Children.Add(metrics);
        footerCard.Child = dock;
        return footerCard;
    }

    private static Border CreateMetricPill(string label, TextBlock valueBlock, string suffix = "")
    {
        var border = new Border
        {
            Background = Brush("#F8FAFC"),
            BorderBrush = Brush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusSmall,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 11.5,
            Foreground = Brush("#64748B"),
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(valueBlock);
        if (!string.IsNullOrEmpty(suffix))
        {
            stack.Children.Add(new TextBlock
            {
                Text = suffix,
                FontSize = 11,
                Foreground = Brush("#64748B"),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        border.Child = stack;
        return border;
    }

    private void UpdateFooterMetrics()
    {
        var count = _details.Count;
        var totalGross = _details.Sum(d => d.GrossWeight ?? 0);
        var totalNet = _details.Sum(d => d.NetWeight ?? 0);

        if (_totalLinesText is not null)
            _totalLinesText.Text = count.ToString(CultureInfo.InvariantCulture);
        if (_totalGrossText is not null)
            _totalGrossText.Text = totalGross.ToString("N0", CultureInfo.InvariantCulture);
        if (_totalNetText is not null)
            _totalNetText.Text = totalNet.ToString("N0", CultureInfo.InvariantCulture);
        if (_detailLineCountBadge is not null)
            _detailLineCountBadge.Text = count == 1 ? "1 line" : $"{count} lines";
    }

    private static Style CreateHeaderStyle(HorizontalAlignment alignment = HorizontalAlignment.Center)
    {
        var style = new Style(typeof(DataGridColumnHeader));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridColumnHeader)) is Style defaultStyle)
            style.BasedOn = defaultStyle;
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#F8FAFC")));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#475569")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, 11.5d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, alignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 5, 6, 5)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#E2E8F0")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        return style;
    }

    private void AddRevisionColumns(DataGrid grid)
    {
        var binding = new Binding(nameof(RevisionDraft.Revision))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
        };
        var textStyle = new Style(typeof(TextBlock));
        textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#0F766E")));
        textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        textStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var remarkTextStyle = new Style(typeof(TextBlock));
        remarkTextStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
        remarkTextStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        remarkTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 4, 6, 4)));
        remarkTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var remarkEditStyle = new Style(typeof(TextBox));
        remarkEditStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        remarkEditStyle.Setters.Add(new Setter(TextBox.TextWrappingProperty, TextWrapping.Wrap));
        remarkEditStyle.Setters.Add(new Setter(TextBox.AcceptsReturnProperty, true));
        remarkEditStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
        remarkEditStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        remarkEditStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        remarkEditStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var editStyle = new Style(typeof(TextBox));
        editStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
        editStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        var remarkCellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch);

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Revision",
            Binding = binding,
            Width = new DataGridLength(75),
            ElementStyle = textStyle,
            EditingElementStyle = editStyle,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Center),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            IsReadOnly = true
        });
        AddDateColumn(grid, "Date Report", nameof(RevisionDraft.ReportDate), _isReadOnly);
        AddTextColumn(grid, "Remark", nameof(RevisionDraft.Remark), new DataGridLength(1, DataGridLengthUnitType.Star),
            isReadOnly: _isReadOnly,
            elementStyle: remarkTextStyle, editingElementStyle: remarkEditStyle, cellStyle: remarkCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Left));
    }

    private void AddDetailColumns(DataGrid grid)
    {
        var detailTextStyle = new Style(typeof(TextBlock));
        detailTextStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
        detailTextStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 0, 4, 0)));
        detailTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        var centeredDetailTextStyle = new Style(typeof(TextBlock), detailTextStyle);
        centeredDetailTextStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        var detailEditStyle = new Style(typeof(TextBox));
        detailEditStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        detailEditStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
        detailEditStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        detailEditStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        detailEditStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var centeredDetailEditStyle = new Style(typeof(TextBox), detailEditStyle);
        centeredDetailEditStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
        var detailCellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch);

        AddRowNumberColumn(grid);
        AddMonthColumn(grid, _isReadOnly);
        AddTextColumn(grid, "Joint Contract Name", nameof(DetailDraft.JointContractName),
            new DataGridLength(1, DataGridLengthUnitType.Star), minWidth: 160, isReadOnly: _isReadOnly,
            elementStyle: detailTextStyle, editingElementStyle: detailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Left));
        AddChoiceColumn(grid, "Code", nameof(DetailDraft.Code), nameof(DetailDraft.CodeOptions), 90, _isReadOnly, HorizontalAlignment.Center);
        AddChoiceColumn(grid, "Dest.", nameof(DetailDraft.Destination), nameof(DetailDraft.DestinationOptions), 150, _isReadOnly, HorizontalAlignment.Left);
        AddGradeChoiceColumn(grid, 110, _isReadOnly);
        AddTextColumn(grid, "Unit", nameof(DetailDraft.Unit), 65, isReadOnly: _isReadOnly,
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddTextColumn(grid, "Gross (MT)", nameof(DetailDraft.GrossWeight), 95, "N0", isReadOnly: _isReadOnly,
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddTextColumn(grid, "Net (MT)", nameof(DetailDraft.NetWeight), 95, "N0", isReadOnly: _isReadOnly,
            elementStyle: centeredDetailTextStyle, editingElementStyle: centeredDetailEditStyle, cellStyle: detailCellStyle,
            headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
        AddChoiceColumn(grid, "Package", nameof(DetailDraft.PackageType), nameof(DetailDraft.PackageOptions), 105, _isReadOnly, HorizontalAlignment.Center);
        AddDateColumn(grid, "Marking Due", nameof(DetailDraft.MarkingDueDate), _isReadOnly);
    }

    private static void AddGradeChoiceColumn(DataGrid grid, double width, bool isReadOnly = false)
    {
        if (isReadOnly)
        {
#pragma warning disable CS0618
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4));
            borderFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            borderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.SetValue(FrameworkElement.MinWidthProperty, 60d);
            borderFactory.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(DetailDraft.GradeBadgeVisibility)));
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(DetailDraft.GradeBackground)));
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(DetailDraft.GradeBorderBrush)));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            textFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            textFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.MarginProperty, new Thickness(6, 2, 6, 2));
            textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(DetailDraft.Grade)));
            textFactory.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(DetailDraft.GradeForeground)));

            borderFactory.AppendChild(textFactory);

            var template = new DataTemplate { VisualTree = borderFactory };
#pragma warning restore CS0618
            grid.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Grade",
                CellTemplate = template,
                CellStyle = CreateGridCellStyle(HorizontalAlignment.Center),
                HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
                Width = new DataGridLength(width),
                MinWidth = width,
                IsReadOnly = true
            });
            return;
        }

#pragma warning disable CS0618
        var boxFactory = new FrameworkElementFactory(typeof(ClearableComboBox));
        boxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
        boxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        boxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxFactory.SetBinding(Border.BackgroundProperty, new Binding(nameof(DetailDraft.GradeBackground)));
        boxFactory.SetBinding(Border.BorderBrushProperty, new Binding(nameof(DetailDraft.GradeBorderBrush)));
        boxFactory.SetBinding(ClearableComboBox.ItemForegroundProperty, new Binding(nameof(DetailDraft.GradeForeground)));
        boxFactory.SetValue(ClearableComboBox.ItemFontWeightProperty, FontWeights.Bold);
        boxFactory.SetBinding(ClearableComboBox.ItemsSourceProperty, new Binding(nameof(DetailDraft.GradeOptions)));
        boxFactory.SetBinding(ClearableComboBox.SelectedItemProperty, new Binding(nameof(DetailDraft.Grade))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });

        var editTemplate = new DataTemplate { VisualTree = boxFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Grade",
            CellTemplate = editTemplate,
            CellEditingTemplate = editTemplate,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(width),
            MinWidth = width
        });
    }

    private static void AddChoiceColumn(DataGrid grid, string title, string property,
        string itemsSourceProperty, double width, bool isReadOnly = false, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        if (isReadOnly)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
            textStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            if (alignment == HorizontalAlignment.Center)
            {
                textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            }
            else
            {
                textStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
                textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left));
            }

            AddTextColumn(grid, title, property, width, isReadOnly: true,
                elementStyle: textStyle,
                cellStyle: CreateGridCellStyle(alignment),
                headerStyle: CreateHeaderStyle(alignment));
            return;
        }

#pragma warning disable CS0618
        var boxFactory = new FrameworkElementFactory(typeof(ClearableComboBox));
        boxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 2, 4, 2));
        boxFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        boxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        boxFactory.SetValue(Border.BackgroundProperty, Brushes.White);
        boxFactory.SetValue(Border.BorderBrushProperty, Brush("#E2E8F0"));
        boxFactory.SetBinding(ClearableComboBox.ItemsSourceProperty, new Binding(itemsSourceProperty));
        boxFactory.SetBinding(ClearableComboBox.SelectedItemProperty, new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        var template = new DataTemplate { VisualTree = boxFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = title,
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Stretch),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(width),
            MinWidth = width
        });
    }

    private static (Dictionary<string, IReadOnlyList<string>> Destinations, IReadOnlyList<string> Codes,
        IReadOnlyList<string> Grades, IReadOnlyList<string> Packages) ReadCatalog(JsonElement catalog)
    {
        if (catalog.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("The server returned an invalid marking preparation catalog.");

        var destinations = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (catalog.TryGetProperty("codeDestinations", out var pairs) && pairs.ValueKind == JsonValueKind.Array)
        {
            foreach (var pair in pairs.EnumerateArray())
            {
                var code = MarkingPreparationCatalogHelper.CleanValue(ReadCatalogString(pair, "code"));
                var destination = MarkingPreparationCatalogHelper.CleanValue(ReadCatalogString(pair, "destination"));
                if (code.Length == 0 || destination.Length == 0) continue;
                if (!destinations.TryGetValue(code, out var values))
                    destinations[code] = values = [];
                if (!values.Contains(destination, StringComparer.OrdinalIgnoreCase)) values.Add(destination);
            }
        }

        string[] grades = catalog.TryGetProperty("grades", out var gradeRows) && gradeRows.ValueKind == JsonValueKind.Array
            ? gradeRows.EnumerateArray().Select(item => ReadCatalogString(item, "name"))
                .Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        string[] packages = catalog.TryGetProperty("packageTypes", out var packageRows) && packageRows.ValueKind == JsonValueKind.Array
            ? packageRows.EnumerateArray().Select(item => ReadCatalogString(item, "code"))
                .Where(code => code.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        var orderedDestinations = destinations.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<string>)item.Value.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var codes = orderedDestinations.Keys.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        if (codes.Length == 0 || grades.Length == 0 || packages.Length == 0)
            throw new InvalidOperationException("The backend marking preparation catalog is empty. Check the Code, Destination, Grade and Package master data.");
        return (orderedDestinations, codes, grades, packages);
    }

    private static string ReadCatalogString(JsonElement item, string property)
        => item.ValueKind == JsonValueKind.Object && item.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    private static void AddMonthColumn(DataGrid grid, bool isReadOnly = false)
    {
        if (isReadOnly)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
            textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            textStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));

            AddTextColumn(grid, "Month", nameof(DetailDraft.MonthDate), 90, "MMM-yy",
                isReadOnly: true, elementStyle: textStyle,
                cellStyle: CreateGridCellStyle(HorizontalAlignment.Center),
                headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
            return;
        }

#pragma warning disable CS0618
        var pickerFactory = new FrameworkElementFactory(typeof(BookingQueueDatePicker));
        pickerFactory.SetValue(FrameworkElement.WidthProperty, 100d);
        pickerFactory.SetValue(FrameworkElement.HeightProperty, EditorDatePickerHeight);
        pickerFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        pickerFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        pickerFactory.SetValue(BookingQueueDatePicker.MonthSelectionOnlyProperty, true);
        pickerFactory.SetValue(UiControlProperties.ButtonCornerRadiusProperty, UiTokens.CornerRadiusSmall);
        pickerFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        pickerFactory.SetValue(BookingQueueDatePicker.DateDisplayFormatProperty, "MMM-yy");
        pickerFactory.SetBinding(BookingQueueDatePicker.SelectedDateProperty, new Binding(nameof(DetailDraft.MonthDate))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        var template = new DataTemplate { VisualTree = pickerFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Month",
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateDatePickerCellStyle(),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(106)
        });
    }

    private static void AddRowNumberColumn(DataGrid grid)
    {
#pragma warning disable CS0618
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        factory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.SetValue(TextBlock.ForegroundProperty, Brush("#64748B"));
        factory.SetBinding(TextBlock.TextProperty, new Binding(nameof(DataGridRow.Header))
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(DataGridRow), 1)
        });
        var template = new DataTemplate { VisualTree = factory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "No.",
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateGridCellStyle(HorizontalAlignment.Center),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            IsReadOnly = true,
            Width = new DataGridLength(36)
        });
    }

    private static void AddTextColumn(DataGrid grid, string title, string property, double width, string? format = null,
        bool isReadOnly = false, Style? elementStyle = null, Style? editingElementStyle = null, Style? cellStyle = null, Style? headerStyle = null)
        => AddTextColumn(grid, title, property, new DataGridLength(width), format,
            minWidth: 20, isReadOnly: isReadOnly, elementStyle: elementStyle, editingElementStyle: editingElementStyle, cellStyle: cellStyle, headerStyle: headerStyle);

    private static void AddTextColumn(DataGrid grid, string title, string property, DataGridLength width,
        string? format = null, double minWidth = 20, bool isReadOnly = false, Style? elementStyle = null,
        Style? editingElementStyle = null, Style? cellStyle = null, Style? headerStyle = null)
    {
        var binding = new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            StringFormat = format is null ? null : $"{{0:{format}}}",
            ConverterCulture = format is "N0" or "N3" or "MMM-yy" or "dd-MMM-yyyy"
                ? CultureInfo.GetCultureInfo("en-US")
                : CultureInfo.CurrentCulture
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = title,
            Binding = binding,
            Width = width,
            MinWidth = minWidth,
            IsReadOnly = isReadOnly,
            ElementStyle = elementStyle,
            EditingElementStyle = editingElementStyle,
            CellStyle = cellStyle,
            HeaderStyle = headerStyle ?? CreateHeaderStyle()
        });
    }

    private static void AddDateColumn(DataGrid grid, string title, string property, bool isReadOnly = false)
    {
        if (isReadOnly)
        {
            var textStyle = new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.5d));
            textStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            textStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            textStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            textStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));

            AddTextColumn(grid, title, property, 115, "dd-MMM-yyyy",
                isReadOnly: true, elementStyle: textStyle,
                cellStyle: CreateGridCellStyle(HorizontalAlignment.Center),
                headerStyle: CreateHeaderStyle(HorizontalAlignment.Center));
            return;
        }

#pragma warning disable CS0618
        var datePickerFactory = new FrameworkElementFactory(typeof(BookingQueueDatePicker));
        datePickerFactory.SetValue(FrameworkElement.WidthProperty, 115d);
        datePickerFactory.SetValue(FrameworkElement.HeightProperty, EditorDatePickerHeight);
        datePickerFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        datePickerFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        datePickerFactory.SetValue(UiControlProperties.ButtonCornerRadiusProperty, UiTokens.CornerRadiusSmall);
        datePickerFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
        datePickerFactory.SetBinding(BookingQueueDatePicker.SelectedDateProperty, new Binding(property)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        datePickerFactory.SetValue(BookingQueueDatePicker.DateDisplayFormatProperty, "dd-MMM-yyyy");
        if (property == nameof(DetailDraft.EtdDate))
        {
            datePickerFactory.AddHandler(BookingQueueDatePicker.DateSelectedEvent,
                new RoutedPropertyChangedEventHandler<DateTime?>((sender, args) =>
                {
                    if (sender is not BookingQueueDatePicker { DataContext: DetailDraft detail }) return;
                    if (detail.MarkingDueDate is null || detail.MarkingDueDate == args.OldValue?.AddDays(-20))
                        detail.MarkingDueDate = args.NewValue?.AddDays(-20);
                }));
        }

        var template = new DataTemplate { VisualTree = datePickerFactory };
#pragma warning restore CS0618
        grid.Columns.Add(new DataGridTemplateColumn
        {
            Header = title,
            CellTemplate = template,
            CellEditingTemplate = template,
            CellStyle = CreateDatePickerCellStyle(),
            HeaderStyle = CreateHeaderStyle(HorizontalAlignment.Center),
            Width = new DataGridLength(122)
        });
    }

    private static Style CreateDatePickerCellStyle()
    {
        var style = CreateGridCellStyle(HorizontalAlignment.Stretch);
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private static Style CreateGridCellStyle(HorizontalAlignment contentAlignment)
    {
        var style = new Style(typeof(DataGridCell));
        if (System.Windows.Application.Current?.TryFindResource(typeof(DataGridCell)) is Style defaultStyle)
            style.BasedOn = defaultStyle;
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2, 1, 2, 1)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, contentAlignment));
        style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#F1F5F9")));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        return style;
    }

    private static void EnableSingleClickEditing(DataGrid grid)
    {
        grid.PreviewMouseLeftButtonDown += (sender, args) =>
        {
            if (args.OriginalSource is not DependencyObject dep) return;
            if (FindVisualParent<Button>(dep) is not null ||
                FindVisualParent<BookingQueueDatePicker>(dep) is not null ||
                FindVisualParent<System.Windows.Controls.Primitives.ScrollBar>(dep) is not null)
            {
                return;
            }

            var cell = FindVisualParent<DataGridCell>(dep);
            if (cell is null || cell.IsEditing || cell.IsReadOnly) return;

            if (cell.DataContext is not null && !Equals(grid.SelectedItem, cell.DataContext))
            {
                grid.SelectedItem = cell.DataContext;
            }

            var clearable = FindVisualChild<ClearableComboBox>(cell);
            if (clearable is not null)
            {
                clearable.OpenDropDown();
                return;
            }

            if (cell.Column is not DataGridTextColumn) return;

            if (!cell.IsFocused) cell.Focus();
            grid.BeginEdit();

            grid.Dispatcher.BeginInvoke(() =>
            {
                if (cell.Content is TextBox tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        };
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var sub = FindVisualChild<T>(child);
            if (sub is not null) return sub;
        }
        return null;
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            DependencyObject? parent = null;
            if (current is Visual)
            {
                try { parent = VisualTreeHelper.GetParent(current); } catch { }
            }
            parent ??= LogicalTreeHelper.GetParent(current);
            current = parent;
        }
        return null;
    }

    private void SaveReport()
    {
        _revisionGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _revisionGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _detailGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _detailGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_reportMonth.SelectedDate is not { } reportMonth)
        {
            ShowValidation("Choose a report month.");
            return;
        }

        var revisions = _revisions.Where(IsMeaningful).ToList();
        var details = _details.Where(IsMeaningful).ToList();
        var detailsChanged = !_isNewReport &&
                             !string.Equals(DetailFingerprint(details), _originalDetailFingerprint, StringComparison.Ordinal);
        var newRevisions = revisions
            .Where(item => !string.IsNullOrWhiteSpace(item.Revision) &&
                           !_originalRevisionNumbers.Contains(item.Revision.Trim()))
            .ToList();
        if (detailsChanged && newRevisions.Count == 0)
        {
            var revision = new RevisionDraft
            {
                Revision = NextRevision(revisions),
                ReportDate = DateTime.Today
            };
            _revisions.Add(revision);
            _revisionGrid.SelectedItem = revision;
            _revisionGrid.ScrollIntoView(revision);
            ShowValidation($"Marking details changed. Revision {revision.Revision} was added. Enter the reason in its Remark cell, then save again.");
            return;
        }
        if (newRevisions.Any(item => string.IsNullOrWhiteSpace(item.Remark)))
        {
            ShowValidation("Enter a reason in the Remark column for each new revision.");
            return;
        }
        if (revisions.Any(item => string.IsNullOrWhiteSpace(item.Revision) || item.ReportDate is null))
        {
            ShowValidation("Each revision needs a revision number and report date.");
            return;
        }
        if (revisions.Select(item => item.Revision?.Trim() ?? "")
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != revisions.Count)
        {
            ShowValidation("Revision numbers must be unique within a report.");
            return;
        }

        if (details.Any(item => string.IsNullOrWhiteSpace(item.JointContractName)))
        {
            ShowValidation("Each detail row needs a Joint Contract Name.");
            return;
        }
        if (_isNewReport && details.Any(item =>
                string.IsNullOrWhiteSpace(item.SourceSiScheduleId) ||
                string.IsNullOrWhiteSpace(item.SourceSiLineId) ||
                item.SourceSiRevision is null))
        {
            ShowValidation("New Marking reports must be created from selected SI lines.");
            return;
        }

        Payload = new
        {
            reportMonth = reportMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            createdBy = Optional(_state.CreatedBy),
            printedBy = Optional(_state.PrintedBy),
            issueBy = Optional(_state.IssueBy),
            issueDate = ToIso(_state.IssueDate),
            productionAdminSign = Optional(_state.ProductionAdminSign),
            productionAdminDate = ToIso(_state.ProductionAdminDate),
            qacSign = Optional(_state.QacSign),
            qacDate = ToIso(_state.QacDate),
            warehouseSign = Optional(_state.WarehouseSign),
            warehouseDate = ToIso(_state.WarehouseDate),
            revisions = revisions.Select(item => new
            {
                revision = item.Revision!.Trim(),
                reportDate = ToIso(item.ReportDate),
                remark = Optional(item.Remark)
            }).ToList(),
            details = details.Select(item => new
            {
                monthLabel = Optional(item.MonthLabel),
                jointContractName = item.JointContractName!.Trim(),
                code = Optional(MarkingPreparationCatalogHelper.CleanValue(item.Code)),
                destination = Optional(MarkingPreparationCatalogHelper.CleanValue(item.Destination)),
                grade = Optional(item.Grade),
                unit = item.Unit,
                grossWeight = item.GrossWeight,
                netWeight = item.NetWeight,
                packageType = Optional(item.PackageType),
                markingDueDate = ToIso(item.MarkingDueDate),
                markingSign = Optional(item.MarkingSign),
                statusDate = ToIso(item.StatusDate),
                etdDate = ToIso(item.EtdDate),
                sourceSiScheduleId = Optional(item.SourceSiScheduleId),
                sourceSiLineId = Optional(item.SourceSiLineId),
                sourceSiRevision = item.SourceSiRevision,
                sourceSiLinkNote = Optional(item.SourceSiLinkNote)
            }).ToList()
        };
        DialogResult = true;
        Close();
    }

    private static bool IsMeaningful(RevisionDraft item)
        => !string.IsNullOrWhiteSpace(item.Revision) || !string.IsNullOrWhiteSpace(item.Remark) || item.ReportDate is not null;

    private static bool IsMeaningful(DetailDraft item)
        => !string.IsNullOrWhiteSpace(item.MonthLabel) || !string.IsNullOrWhiteSpace(item.JointContractName) ||
           !string.IsNullOrWhiteSpace(item.Code) || !string.IsNullOrWhiteSpace(item.Destination) ||
           !string.IsNullOrWhiteSpace(item.Grade) || item.Unit is not null || item.GrossWeight is not null ||
           item.NetWeight is not null || !string.IsNullOrWhiteSpace(item.PackageType) || item.MarkingDueDate is not null ||
           !string.IsNullOrWhiteSpace(item.MarkingSign) || item.StatusDate is not null || item.EtdDate is not null ||
           !string.IsNullOrWhiteSpace(item.SourceSiScheduleId) || !string.IsNullOrWhiteSpace(item.SourceSiLineId);

    private static string DetailFingerprint(IEnumerable<DetailDraft> details)
        => JsonSerializer.Serialize(details.Where(IsMeaningful).Select(item => new
        {
            MonthLabel = Optional(item.MonthLabel),
            JointContractName = Optional(item.JointContractName),
            Code = Optional(item.Code),
            Destination = Optional(item.Destination),
            Grade = Optional(item.Grade),
            item.Unit,
            item.GrossWeight,
            item.NetWeight,
            PackageType = Optional(item.PackageType),
            MarkingDueDate = item.MarkingDueDate?.Date,
            MarkingSign = Optional(item.MarkingSign),
            StatusDate = item.StatusDate?.Date,
            EtdDate = item.EtdDate?.Date,
            item.SourceSiScheduleId,
            item.SourceSiLineId,
            item.SourceSiRevision
        }).ToArray(), JsonOptions);

    private static DetailDraft CreateDetailFromSICandidate(JsonElement source)
    {
        var etdDate = ReadDate(source, "etdDate");
        return new DetailDraft
        {
            MonthLabel = ReadString(source, "monthLabel"),
            JointContractName = ReadString(source, "jointContractName"),
            Code = ReadString(source, "code"),
            Destination = ReadString(source, "destination"),
            Grade = ReadString(source, "grade"),
            Unit = ReadNullableInt(source, "unit"),
            GrossWeight = ReadNullableDecimal(source, "grossWeight"),
            NetWeight = ReadNullableDecimal(source, "netWeight"),
            PackageType = ReadString(source, "packageType"),
            EtdDate = etdDate,
            MarkingDueDate = etdDate?.AddDays(-20),
            SourceSiScheduleId = ReadString(source, "sourceSiScheduleId"),
            SourceSiLineId = ReadString(source, "sourceSiLineId"),
            SourceSiRevision = ReadNullableInt(source, "sourceSiRevision")
        };
    }

    private static string? ReadString(JsonElement source, string name)
        => source.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ToString()
            : null;

    private static DateTime? ReadDate(JsonElement source, string name)
        => source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
           DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.Date
            : null;

    private static int? ReadNullableInt(JsonElement source, string name)
        => source.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;

    private static decimal? ReadNullableDecimal(JsonElement source, string name)
        => source.TryGetProperty(name, out var value) && value.TryGetDecimal(out var parsed) ? parsed : null;

    private void ShowValidation(string message)
        => MessageBox.Show(this, message, "Check report details", MessageBoxButton.OK, MessageBoxImage.Information);

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? ToIso(DateTime? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string NextRevision(IEnumerable<RevisionDraft> revisions)
    {
        var numbers = revisions.Select(item => int.TryParse(item.Revision, out var number) ? number : -1);
        return numbers.DefaultIfEmpty(-1).Max() is var next && next >= 0 ? (next + 1).ToString("D2", CultureInfo.InvariantCulture) : "00";
    }

    private static BookingQueueDatePicker CreateDatePicker(DateTime? value)
    {
        var datePicker = new BookingQueueDatePicker
        {
            SelectedDate = value?.Date,
            Width = EditorDatePickerWidth,
            Height = EditorDatePickerHeight
        };
        UiControlProperties.SetButtonCornerRadius(datePicker, UiTokens.CornerRadiusSmall);
        return datePicker;
    }

    private Button CreateButton(string text, bool primary = false)
    {
        if (!primary && text.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
        {
            var button = new Button
            {
                Content = new FluentIcon
                {
                    Icon = FluentIcons.Common.Icon.Delete,
                    IconSize = IconSize.Size16,
                    IconVariant = IconVariant.Regular,
                    Width = 16,
                    Height = 16,
                    Foreground = Brush("#EF4444"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Width = 28,
                MinWidth = 28,
                Height = 28,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                Background = Brush("#FEF2F2"),
                BorderBrush = Brush("#FECACA"),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = text
            };
            UiControlProperties.SetButtonCornerRadius(button, UiTokens.CornerRadiusSmall);
            System.Windows.Automation.AutomationProperties.SetName(button, text);
            return button;
        }

        if (text.StartsWith("Add", StringComparison.OrdinalIgnoreCase))
        {
            var icon = new FluentIcon
            {
                Icon = FluentIcons.Common.Icon.Add,
                IconSize = IconSize.Size16,
                IconVariant = IconVariant.Regular,
                Foreground = Brush("#0F766E"),
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(icon);
            stack.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#0F766E"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var button = new Button
            {
                Content = stack,
                Height = 28,
                Padding = new Thickness(10, 2, 10, 2),
                Background = Brush("#F0FDF4"),
                BorderBrush = Brush("#99F6E4"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0)
            };
            UiControlProperties.SetButtonCornerRadius(button, UiTokens.CornerRadiusSmall);
            return button;
        }

        var btn = new Button
        {
            Content = text,
            Height = 34,
            MinWidth = primary ? 120 : 90,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(14, 4, 14, 4),
            FontSize = 13,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
        };
        UiControlProperties.SetButtonCornerRadius(btn, UiTokens.CornerRadiusControl);
        return btn;
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);

    private sealed class EditorState
    {
        public DateTime? CreatedAt { get; set; }
        public DateTime ReportMonth { get; set; }
        public string? CreatedBy { get; set; }
        public string? PrintedBy { get; set; }
        public string? IssueBy { get; set; }
        public DateTime? IssueDate { get; set; }
        public string? ProductionAdminSign { get; set; }
        public DateTime? ProductionAdminDate { get; set; }
        public string? QacSign { get; set; }
        public DateTime? QacDate { get; set; }
        public string? WarehouseSign { get; set; }
        public DateTime? WarehouseDate { get; set; }
        public List<RevisionDraft> Revisions { get; set; } = [];
        public List<DetailDraft> Details { get; set; } = [];
    }

    private sealed class RevisionDraft
    {
        public string? Revision { get; set; }
        public DateTime? ReportDate { get; set; }
        public string? Remark { get; set; }
    }

    private sealed class DetailDraft : INotifyPropertyChanged
    {
        private string? _monthLabel;
        private DateTime? _markingDueDate;
        private string? _code;
        private string? _destination;
        private string? _grade;
        private Dictionary<string, IReadOnlyList<string>> _codeDestinations = new(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<string> _allCodes = [];
        private IReadOnlyList<string> _allDestinations = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        public string? MonthLabel
        {
            get => _monthLabel;
            set
            {
                if (_monthLabel == value) return;
                _monthLabel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthLabel)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MonthDate)));
            }
        }
        public DateTime? MonthDate
        {
            get => ParseMonthLabel(MonthLabel);
            set => MonthLabel = value?.ToString("MMM-yy", CultureInfo.InvariantCulture);
        }
        public string? JointContractName { get; set; }
        public string? Code
        {
            get => _code;
            set
            {
                var clean = string.IsNullOrWhiteSpace(value) ? null : MarkingPreparationCatalogHelper.CleanValue(value);
                if (string.Equals(_code, clean, StringComparison.Ordinal)) return;
                _code = clean;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
                RefreshDestinationsForCode();
            }
        }
        public string? Destination
        {
            get => _destination;
            set
            {
                var clean = string.IsNullOrWhiteSpace(value) ? null : MarkingPreparationCatalogHelper.CleanValue(value);
                if (string.Equals(_destination, clean, StringComparison.Ordinal))
                {
                    if (clean is null && !string.IsNullOrWhiteSpace(Code))
                    {
                        Code = null;
                        SetCodeOptions(_allCodes);
                    }
                    return;
                }
                _destination = clean;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Destination)));
                if (clean is null)
                {
                    Code = null;
                    SetCodeOptions(_allCodes);
                    return;
                }

                var matchingCodes = CodesForDestination(clean);
                if (matchingCodes.Count == 0 && clean.Length >= 2)
                {
                    var prefixMatches = _codeDestinations
                        .Where(item => item.Value.Any(d => d.Trim().StartsWith(clean, StringComparison.OrdinalIgnoreCase)))
                        .Select(item => item.Key).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
                    if (prefixMatches.Length == 1)
                    {
                        matchingCodes = prefixMatches;
                    }
                }

                if (matchingCodes.Count > 0)
                {
                    if (!matchingCodes.Contains(Code ?? "", StringComparer.OrdinalIgnoreCase))
                    {
                        Code = matchingCodes[0];
                    }
                    SetCodeOptions(matchingCodes);
                }
            }
        }
        public string? Grade
        {
            get => _grade;
            set
            {
                if (string.Equals(_grade, value, StringComparison.Ordinal)) return;
                _grade = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Grade)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeBackground)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeForeground)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeBorderBrush)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeBadgeVisibility)));
            }
        }
        public Visibility GradeBadgeVisibility => string.IsNullOrWhiteSpace(Grade) ? Visibility.Collapsed : Visibility.Visible;
        public Brush GradeBackground => GradeColorHelper.GetBackgroundBrush(Grade);
        public Brush GradeForeground => GradeColorHelper.GetForegroundBrush(Grade);
        public Brush GradeBorderBrush => GradeColorHelper.GetBorderBrush(Grade);
        public ObservableCollection<string> DestinationOptions { get; } = [];
        public IReadOnlyList<string> CodeOptions { get; private set; } = [];
        public IReadOnlyList<string> GradeOptions { get; private set; } = [];
        public IReadOnlyList<string> PackageOptions { get; private set; } = [];
        public int? Unit { get; set; }
        [JsonPropertyName("grossWeight")]
        public decimal? GrossWeight { get; set; }
        [JsonPropertyName("gross_weight")]
        public decimal? GrossWeightSnake
        {
            get => GrossWeight;
            set => GrossWeight ??= value;
        }
        [JsonPropertyName("netWeight")]
        public decimal? NetWeight { get; set; }
        [JsonPropertyName("net_weight")]
        public decimal? NetWeightSnake
        {
            get => NetWeight;
            set => NetWeight ??= value;
        }
        private string? _packageType;
        public string? PackageType
        {
            get => _packageType;
            set
            {
                if (string.Equals(_packageType, value, StringComparison.Ordinal)) return;
                _packageType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageType)));
            }
        }
        public DateTime? MarkingDueDate
        {
            get => _markingDueDate;
            set
            {
                if (_markingDueDate == value) return;
                _markingDueDate = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkingDueDate)));
            }
        }
        public string? MarkingSign { get; set; }
        public DateTime? StatusDate { get; set; }
        public DateTime? EtdDate { get; set; }
        public string? SourceSiScheduleId { get; set; }
        public string? SourceSiLineId { get; set; }
        public int? SourceSiRevision { get; set; }
        public string? SourceSiLinkNote { get; set; }

        public void ConfigureCatalog(Dictionary<string, IReadOnlyList<string>> codeDestinations,
            IReadOnlyList<string> codes, IReadOnlyList<string> grades, IReadOnlyList<string> packages)
        {
            _codeDestinations = codeDestinations;
            _allCodes = codes;
            _allDestinations = codeDestinations.Values.SelectMany(value => value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
            GradeOptions = PreserveExistingValue(grades, Grade);

            // Normalize already-saved values as well as future selections. Without this,
            // a row loaded with a Code but no Destination stays incomplete when the user
            // selects that same Code again (the property setter correctly sees no change).
            if (!string.IsNullOrWhiteSpace(Code))
            {
                RefreshDestinationsForCode();
            }
            else if (!string.IsNullOrWhiteSpace(Destination))
            {
                var matchingCodes = CodesForDestination(Destination);
                if (matchingCodes.Count == 1)
                    Code = matchingCodes[0];
                else
                {
                    SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                    SetCodeOptions(matchingCodes.Count > 0
                        ? matchingCodes
                        : PreserveExistingValue(codes, Code));
                }
            }
            else
            {
                SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                SetCodeOptions(PreserveExistingValue(codes, Code));
            }
            PackageOptions = PreserveExistingValue(packages, PackageType);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PackageOptions)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GradeOptions)));
        }

        private static IReadOnlyList<string> PreserveExistingValue(IReadOnlyList<string> options, string? existing)
            => !string.IsNullOrWhiteSpace(existing) && !options.Contains(existing, StringComparer.OrdinalIgnoreCase)
                ? options.Append(existing).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray()
                : options;

        private void RefreshDestinationsForCode()
        {
            if (string.IsNullOrWhiteSpace(Code) || !_codeDestinations.TryGetValue(Code, out var matches))
            {
                SetDestinationOptions(_allDestinations, preserveExistingValue: true);
                SetCodeOptions(PreserveExistingValue(_allCodes, Code));
                return;
            }

            if (string.IsNullOrWhiteSpace(Destination))
            {
                if (matches.Count == 1) Destination = matches[0];
            }
            else if (!matches.Contains(Destination, StringComparer.OrdinalIgnoreCase))
                Destination = matches.Count == 1 ? matches[0] : null;
            SetDestinationOptions(matches, preserveExistingValue: true);
            SetCodeOptions(PreserveExistingValue(_allCodes, Code));
        }

        private IReadOnlyList<string> CodesForDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) return Array.Empty<string>();
            var target = destination.Trim();
            return _codeDestinations
                .Where(item => item.Value.Any(d => string.Equals(d.Trim(), target, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private void SetDestinationOptions(IEnumerable<string> values, bool preserveExistingValue)
        {
            var options = values.ToList();
            if (preserveExistingValue && !string.IsNullOrWhiteSpace(Destination) &&
                !options.Contains(Destination, StringComparer.OrdinalIgnoreCase))
                options.Add(Destination);
            DestinationOptions.Clear();
            foreach (var value in options.Distinct(StringComparer.OrdinalIgnoreCase))
                DestinationOptions.Add(value);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationOptions)));
        }

        private void SetCodeOptions(IEnumerable<string> values)
        {
            CodeOptions = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CodeOptions)));
        }

        private static DateTime? ParseMonthLabel(string? label)
        {
            if (string.IsNullOrWhiteSpace(label)) return null;
            var formats = new[] { "MMM-yy", "MMM-yyyy", "MMM yyyy", "MMMM yyyy", "yyyy-MM", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(label.Trim(), formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces, out var month))
                return null;
            return new DateTime(month.Year, month.Month, 1);
        }
    }
}


internal sealed class ClearableComboBox : Border
{
    private static readonly ControlTemplate EditableComboBoxNoArrowTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type ComboBox}'>
    <Grid>
        <TextBox x:Name='PART_EditableTextBox'
                 FocusVisualStyle='{x:Null}'
                 Background='Transparent'
                 BorderThickness='0'
                 Padding='6,2,22,2'
                 VerticalContentAlignment='Center'
                 FontSize='{TemplateBinding FontSize}'
                 FontFamily='{TemplateBinding FontFamily}'
                 Foreground='{TemplateBinding Foreground}'
                 FontWeight='{TemplateBinding FontWeight}'
                 IsReadOnly='{TemplateBinding IsReadOnly}' />
        <Popup x:Name='PART_Popup'
               AllowsTransparency='True'
               IsOpen='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}'
               Placement='Bottom'
               PopupAnimation='Fade'
               Focusable='False'>
            <Border Background='White'
                    BorderBrush='#CBD5E1'
                    BorderThickness='1'
                    CornerRadius='4'
                    MinHeight='24'
                    MaxHeight='220'
                    MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
                    SnapsToDevicePixels='True'>
                <Border.Effect>
                    <DropShadowEffect Color='#0F172A' BlurRadius='10' ShadowDepth='3' Opacity='0.16' />
                </Border.Effect>
                <ScrollViewer SnapsToDevicePixels='True' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled'>
                    <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Contained' />
                </ScrollViewer>
            </Border>
        </Popup>
    </Grid>
</ControlTemplate>");

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(string), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public static readonly DependencyProperty ItemForegroundProperty = DependencyProperty.Register(
        nameof(ItemForeground), typeof(Brush), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(null, OnItemForegroundChanged));

    public static readonly DependencyProperty ItemFontWeightProperty = DependencyProperty.Register(
        nameof(ItemFontWeight), typeof(FontWeight), typeof(ClearableComboBox),
        new FrameworkPropertyMetadata(FontWeights.Normal, OnItemFontWeightChanged));

    private readonly ComboBox _comboBox;
    private readonly Button _clearButton;
    private bool _isUpdatingInternally;

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public string? SelectedItem
    {
        get => (string?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public Brush? ItemForeground
    {
        get => (Brush?)GetValue(ItemForegroundProperty);
        set => SetValue(ItemForegroundProperty, value);
    }

    public FontWeight ItemFontWeight
    {
        get => (FontWeight)GetValue(ItemFontWeightProperty);
        set => SetValue(ItemFontWeightProperty, value);
    }

    public ClearableComboBox()
    {
        CornerRadius = new CornerRadius(4);
        BorderThickness = new Thickness(1);
        BorderBrush = Brush("#E2E8F0");
        Background = Brushes.White;
        Height = 28;
        MinHeight = 26;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        var grid = new Grid();

        _comboBox = new ComboBox
        {
            IsEditable = true,
            IsTextSearchEnabled = true,
            Template = EditableComboBoxNoArrowTemplate,
            BorderThickness = new Thickness(0),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            FontSize = 12.5,
            FocusVisualStyle = null,
            MinHeight = 26,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            IsSynchronizedWithCurrentItem = false
        };

        var itemStyle = new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 4, 8, 4)));
        itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5d));
        itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
        _comboBox.ItemContainerStyle = itemStyle;

        _comboBox.SelectionChanged += (_, _) =>
        {
            if (_isUpdatingInternally) return;
            var selected = _comboBox.SelectedItem as string ?? _comboBox.Text;
            var currentVal = string.IsNullOrWhiteSpace(selected) ? null : selected.Trim();
            if (!string.Equals(SelectedItem, currentVal, StringComparison.Ordinal))
            {
                _isUpdatingInternally = true;
                try
                {
                    SetCurrentValue(SelectedItemProperty, currentVal);
                }
                finally
                {
                    _isUpdatingInternally = false;
                }
            }
            UpdateClearButtonVisibility();
        };

        _comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent,
            new TextChangedEventHandler((_, _) =>
            {
                if (_isUpdatingInternally) return;
                var text = _comboBox.Text;
                var currentVal = string.IsNullOrWhiteSpace(text) ? null : text;
                if (!string.Equals(SelectedItem, currentVal, StringComparison.Ordinal))
                {
                    _isUpdatingInternally = true;
                    try
                    {
                        SetCurrentValue(SelectedItemProperty, currentVal);
                    }
                    finally
                    {
                        _isUpdatingInternally = false;
                    }
                }
                UpdateClearButtonVisibility();
            }));

        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is DependencyObject dep && FindVisualParent<Button>(dep) == _clearButton)
            {
                return;
            }
            OpenDropDown();
        };

        _comboBox.PreviewMouseLeftButtonDown += (_, e) =>
        {
            OpenDropDown();
        };

        _comboBox.GotFocus += (_, _) =>
        {
            SelectParentDataGridRow();
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = Brush("#0F766E");
            }
            OpenDropDown();
        };
        _comboBox.LostFocus += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = IsMouseOver ? Brush("#CBD5E1") : Brush("#E2E8F0");
            }
            if (!string.IsNullOrWhiteSpace(_comboBox.Text))
            {
                var trimmed = _comboBox.Text.Trim();
                if (!string.Equals(_comboBox.Text, trimmed, StringComparison.Ordinal))
                {
                    _comboBox.Text = trimmed;
                }
            }
        };
        _comboBox.DropDownOpened += (_, _) =>
        {
            SelectParentDataGridRow();
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = Brush("#0F766E");
            }
        };
        _comboBox.DropDownClosed += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null)
            {
                BorderBrush = IsMouseOver ? Brush("#CBD5E1") : Brush("#E2E8F0");
            }
        };

        MouseEnter += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = Brush("#CBD5E1");
            }
        };
        MouseLeave += (_, _) =>
        {
            if (BindingOperations.GetBinding(this, BackgroundProperty) == null && !_comboBox.IsDropDownOpen)
            {
                BorderBrush = Brush("#E2E8F0");
            }
        };

        _clearButton = CreateClearButton();
        _clearButton.PreviewMouseLeftButtonDown += (_, e) =>
        {
            SelectParentDataGridRow();
            e.Handled = true;
            ClearSelection();
        };
        _clearButton.Click += (_, e) =>
        {
            SelectParentDataGridRow();
            e.Handled = true;
            ClearSelection();
        };

        grid.Children.Add(_comboBox);
        grid.Children.Add(_clearButton);
        Child = grid;

        Loaded += (_, _) =>
        {
            SyncInternalState();
        };
    }

    private void SelectParentDataGridRow()
    {
        if (DataContext is not null)
        {
            var grid = FindVisualParent<DataGrid>(this);
            if (grid != null && !Equals(grid.SelectedItem, DataContext))
            {
                grid.SelectedItem = DataContext;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            DependencyObject? parent = null;
            if (current is Visual)
            {
                parent = VisualTreeHelper.GetParent(current);
            }
            current = parent ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ClearSelection()
    {
        _isUpdatingInternally = true;
        try
        {
            _comboBox.SelectedItem = null;
            _comboBox.Text = string.Empty;
            SetCurrentValue(SelectedItemProperty, null);
        }
        finally
        {
            _isUpdatingInternally = false;
        }
        UpdateClearButtonVisibility();
        _comboBox.Focus();
    }

    public void OpenDropDown()
    {
        SelectParentDataGridRow();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_comboBox.IsKeyboardFocusWithin)
            {
                _comboBox.Focus();
            }
            if (!_comboBox.IsDropDownOpen)
            {
                _comboBox.IsDropDownOpen = true;
            }
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void SyncInternalState()
    {
        if (ItemsSource is not null && _comboBox.ItemsSource != ItemsSource)
        {
            _comboBox.ItemsSource = ItemsSource;
        }

        var currentText = SelectedItem ?? string.Empty;
        if (!string.Equals(_comboBox.Text, currentText, StringComparison.Ordinal))
        {
            _isUpdatingInternally = true;
            try
            {
                _comboBox.Text = currentText;
                _comboBox.SelectedItem = SelectedItem;
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }

        if (ItemForeground is not null)
        {
            _comboBox.Foreground = ItemForeground;
        }

        _comboBox.FontWeight = ItemFontWeight;

        UpdateClearButtonVisibility();
    }

    private void UpdateClearButtonVisibility()
    {
        var hasValue = !string.IsNullOrWhiteSpace(SelectedItem) ||
                       !string.IsNullOrWhiteSpace(_comboBox.Text);
        _clearButton.Visibility = hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box)
        {
            box._comboBox.ItemsSource = e.NewValue as IEnumerable;
            box.SyncInternalState();
        }
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && !box._isUpdatingInternally)
        {
            var val = e.NewValue as string;
            box._isUpdatingInternally = true;
            try
            {
                var textVal = val ?? string.Empty;
                if (!string.Equals(box._comboBox.Text, textVal, StringComparison.Ordinal))
                {
                    box._comboBox.Text = textVal;
                }
                if (!string.Equals(box._comboBox.SelectedItem as string, val, StringComparison.Ordinal))
                {
                    box._comboBox.SelectedItem = val;
                }
            }
            finally
            {
                box._isUpdatingInternally = false;
            }
            box.UpdateClearButtonVisibility();
        }
    }

    private static void OnItemForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && e.NewValue is Brush brush)
        {
            box._comboBox.Foreground = brush;
        }
    }

    private static void OnItemFontWeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClearableComboBox box && e.NewValue is FontWeight fw)
        {
            box._comboBox.FontWeight = fw;
        }
    }

    private static Button CreateClearButton()
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M 3 3 L 9 9 M 9 3 L 3 9"),
            Stroke = Brush("#94A3B8"),
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 12,
            Height = 12
        };

        var border = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Child = path
        };

        var button = new Button
        {
            Width = 16,
            Height = 16,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 5, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = "Clear",
            Visibility = Visibility.Collapsed,
            Content = border
        };

        button.MouseEnter += (_, _) =>
        {
            border.Background = Brush("#FEE2E2");
            path.Stroke = Brush("#DC2626");
        };
        button.MouseLeave += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            path.Stroke = Brush("#94A3B8");
        };

        return button;
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}


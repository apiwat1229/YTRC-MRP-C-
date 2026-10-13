using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows.Data;
using YTRC.ERP.Application.Features.SIExport;

namespace YTRC.ERP.Wpf;

internal sealed class SIExportEditorWindow : Window
{
    private readonly ObservableCollection<SIExportLine> _lines;
    private readonly TextBox _title;
    private readonly TextBox _recipient;
    private readonly TextBox _year;
    private readonly ComboBox _month;
    private readonly ComboBox _status;
    private readonly TextBox _remark;
    private readonly TextBox _reason;
    private readonly DataGrid _grid;
    private bool _saving;
    private readonly Dictionary<string, List<string>> _catalog;

    internal static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    internal static string NormalizeSupplier(string? supplier)
    {
        if (string.IsNullOrWhiteSpace(supplier)) return "";
        var trimmed = supplier.Trim();
        var simplified = trimmed.Replace(" ", "").Replace(".", "").Replace(",", "");
        if (simplified.IndexOf("YTRUBBER", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Y.T.RUBBER CO.,LTD";
        }
        return trimmed;
    }

    internal static string NormalizePol(string? pol)
    {
        if (string.IsNullOrWhiteSpace(pol)) return "";
        var trimmed = pol.Trim();
        var simplified = trimmed.Replace(" ", "").Replace(".", "").Replace(",", "").ToUpperInvariant();
        if (simplified.Contains("LAEMCHABANG"))
        {
            return "LAEM CHABANG";
        }
        if (simplified.Contains("PHUKET"))
        {
            return "PHUKET";
        }
        if (trimmed.StartsWith("FOB ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].Trim();
        }
        else if (trimmed.StartsWith("FOB", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 3 && !char.IsLetterOrDigit(trimmed[3]))
        {
            trimmed = trimmed[3..].Trim();
        }
        if (trimmed.EndsWith(", THAILAND", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^10].Trim();
        }
        else if (trimmed.EndsWith(",THAILAND", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^9].Trim();
        }
        else if (trimmed.EndsWith(" THAILAND", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^9].Trim();
        }
        return trimmed.ToUpperInvariant();
    }

    internal static Dictionary<string, List<string>> SanitizeCatalog(Dictionary<string, List<string>> catalog)
    {
        var sanitized = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, list) in catalog)
        {
            if (key.Equals("supplier", StringComparison.OrdinalIgnoreCase))
            {
                sanitized[key] = list
                    .Select(NormalizeSupplier)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else if (key.Equals("pol", StringComparison.OrdinalIgnoreCase))
            {
                sanitized[key] = list
                    .Select(NormalizePol)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                sanitized[key] = list
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        return sanitized;
    }

    public SIExportEditorWindow(SIExportSchedule? source, bool copy, Dictionary<string, List<string>> catalog, Func<SIExportSave, Task> save)
    {
        _catalog = SanitizeCatalog(catalog);
        var document = source is null ? new SIExportDocument() : Clone(source.Document);
        foreach (var line in document.Lines)
        {
            line.Supplier = NormalizeSupplier(line.Supplier);
            line.Pol = NormalizePol(line.Pol);
        }
        if (copy)
        {
            document.Title += " (Copy)";
            document.PlanningStatus = "DRAFT";
            foreach (var line in document.Lines)
            {
                line.Id = Guid.NewGuid().ToString();
                line.SentToCarrierDate = line.CarrierConfirmedDate = line.ActualEtd = line.ActualEta = null;
            }
        }
        Title = source is null || copy ? "New SI Export" : $"Edit {source.ScheduleNo} · {source.RevisionLabel}";
        Width = 1180; Height = 780; MinWidth = 880; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 250));
        FontFamily = new FontFamily("Leelawadee UI");
        var root = new Grid { Margin = new Thickness(20) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto,
                     GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            root.RowDefinitions.Add(new RowDefinition { Height = height });
        var heading = new TextBlock { Text = Title, FontSize = 21, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14) };
        root.Children.Add(heading);
        var header = new WrapPanel(); Grid.SetRow(header, 1); root.Children.Add(header);
        _title = Input(document.Title); _recipient = Input(document.Recipient);
        _year = Input(document.ShipmentYear.ToString(CultureInfo.InvariantCulture));
        _month = Choice(Enumerable.Range(1, 12).Select(x => x.ToString("00")), document.ShipmentMonth.ToString("00"));
        _status = Choice(["DRAFT", "FORECAST"], document.PlanningStatus);
        AddLabeled(header, "SI Title *", _title, 280);
        AddLabeled(header, "Recipient / Business Unit *", _recipient, 230);
        AddLabeled(header, "Shipment Year *", _year, 100);
        AddLabeled(header, "Shipment Month *", _month, 90);
        AddLabeled(header, "Planning Status", _status, 140);
        var notes = new WrapPanel(); Grid.SetRow(notes, 2); root.Children.Add(notes);
        _remark = Input(document.Remark); _reason = Input("");
        AddLabeled(notes, "Remarks", _remark, 530);
        AddLabeled(notes, source is null || copy ? "Reason / Source" : "Revision Change Reason *", _reason, 530);
        _lines = new ObservableCollection<SIExportLine>(document.Lines);
        _grid = SIExportUi.LineGrid(_lines);
        RefreshLineNumbers();
        _grid.MouseDoubleClick += (_, _) => EditLine();
        Grid.SetRow(_grid, 4); root.Children.Add(_grid);
        var tools = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetRow(tools, 3); root.Children.Add(tools);
        tools.Children.Add(SIExportUi.Button("Add Contract", () =>
        {
            var line = new SIExportLine();
            if (ShowLineEditor(line))
            {
                _lines.Add(line);
                RefreshLineNumbers();
            }
        }));
        tools.Children.Add(SIExportUi.Button("Edit Line", EditLine));
        tools.Children.Add(SIExportUi.Button("View Source Excel", () =>
        {
            if (_grid.SelectedItem is SIExportLine selected) SIExportSourceWindow.Show(this, selected);
        }));
        tools.Children.Add(SIExportUi.Button("Copy Line", () =>
        {
            if (_grid.SelectedItem is not SIExportLine selected) return;
            var line = Clone(selected); line.Id = Guid.NewGuid().ToString();
            line.SentToCarrierDate = line.CarrierConfirmedDate = line.ActualEtd = line.ActualEta = null;
            if (ShowLineEditor(line))
            {
                _lines.Add(line);
                RefreshLineNumbers();
            }
        }));
        tools.Children.Add(SIExportUi.Button("Remove Line", () =>
        {
            if (_grid.SelectedItem is SIExportLine selected)
            {
                _lines.Remove(selected);
                RefreshLineNumbers();
            }
        }));
        tools.Children.Add(new TextBlock { Text = "Double-click a line to edit. Forecast schedules can be completed later.",
            VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.SlateGray });
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(footer, 5); root.Children.Add(footer);
        var cancel = SIExportUi.Button("Close", Close); DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        var saveButton = SIExportUi.Button("Save SI / Revision", () => { });
        DockPanel.SetDock(saveButton, Dock.Right); footer.Children.Add(saveButton);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick,
            MaxWidth = 740, VerticalAlignment = VerticalAlignment.Center };
        var saveProgress = new ProgressBar { Height = 4, IsIndeterminate = true,
            Visibility = Visibility.Collapsed, MaxWidth = 740, Margin = new Thickness(0, 0, 0, 4) };
        var saveStatus = new StackPanel(); saveStatus.Children.Add(saveProgress); saveStatus.Children.Add(error);
        footer.Children.Add(saveStatus);
        saveButton.Click += async (_, _) =>
        {
            try
            {
                _saving = true; root.IsEnabled = false;
                saveProgress.Visibility = Visibility.Visible;
                error.Foreground = Brushes.SlateGray; error.Text = "Saving SI schedule…";
                if (!int.TryParse(_year.Text, out var year)) throw new ArgumentException("Shipment year must be a valid AD year.");
                var payload = new SIExportSave
                {
                    Title = _title.Text.Trim(), Recipient = _recipient.Text.Trim(), ShipmentYear = year,
                    ShipmentMonth = int.Parse(_month.SelectedItem?.ToString() ?? "0", CultureInfo.InvariantCulture),
                    PlanningStatus = _status.SelectedItem?.ToString() ?? "DRAFT", Remark = _remark.Text.Trim(),
                    Lines = _lines.ToList(), ExpectedRevision = source is null || copy ? null : source.Revision,
                    ChangeReason = _reason.Text.Trim()
                };
                SIExportValidation.Validate(payload);
                if (payload.ExpectedRevision is not null && payload.ChangeReason.Length == 0)
                    throw new ArgumentException("Enter a reason for this revision.");
                await save(payload);
                _saving = false; DialogResult = true;
            }
            catch (Exception ex) { error.Foreground = Brushes.Firebrick; error.Text = ex.Message; }
            finally { _saving = false; root.IsEnabled = true; saveProgress.Visibility = Visibility.Collapsed; }
        };
        Closing += (_, e) => e.Cancel = _saving;
        Content = root;
    }

    private void RefreshLineNumbers()
    {
        for (var i = 0; i < _lines.Count; i++) _lines[i].No = i + 1;
        _grid?.Items.Refresh();
    }

    private void EditLine()
    {
        if (_grid.SelectedItem is not SIExportLine line) return;
        var edited = Clone(line);
        if (!ShowLineEditor(edited)) return;
        _lines[_lines.IndexOf(line)] = edited;
        _grid.SelectedItem = edited;
        RefreshLineNumbers();
    }

    private bool ShowLineEditor(SIExportLine line)
    {
        var window = new SIExportLineEditorWindow(line, _catalog) { Owner = this };
        return window.ShowDialog() == true;
    }

    internal static TextBox Input(string value) => new() { Text = value, MinHeight = 32, Padding = new Thickness(7, 4, 7, 4) };
    internal static ComboBox Choice(IEnumerable<string> values, string selected)
        => new() { ItemsSource = values.ToList(), SelectedItem = selected, MinHeight = 32 };
    internal static void AddLabeled(Panel panel, string label, FrameworkElement input, double width)
    {
        var box = new StackPanel { Width = width, Margin = new Thickness(0, 0, 12, 10) };
        box.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4), Foreground = Brushes.SlateGray });
        box.Children.Add(input); panel.Children.Add(box);
    }
}

internal sealed class SIExportLineEditorWindow : Window
{
    private readonly Dictionary<PropertyInfo, FrameworkElement> _inputs = [];

    private readonly Dictionary<string, List<string>> _catalog;
    public SIExportLineEditorWindow(SIExportLine line, Dictionary<string, List<string>> catalog)
    {
        _catalog = SIExportEditorWindow.SanitizeCatalog(catalog);
        line.Supplier = SIExportEditorWindow.NormalizeSupplier(line.Supplier);
        line.Pol = SIExportEditorWindow.NormalizePol(line.Pol);
        Title = "Shipment Line · Contract and Shipping Details";
        Width = 840; Height = 720; MinWidth = 740; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Leelawadee UI");
        var root = new DockPanel { Margin = new Thickness(18) };
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var save = SIExportUi.Button("Apply Line", () => { });
        DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
        var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 580 };
        footer.Children.Add(error);
        var tabs = new TabControl(); root.Children.Add(tabs);
        AddTab(tabs, "Contract and Quantity", line,
        [
            ("Contract", "Contract # (or enter Customer PO)"), ("CustomerPo", "Customer PO"),
            ("FullContract", "Full contract A+B"), ("Plant", "Plant"), ("Grade", "Grade"), ("Code", "Code"),
            ("Supplier", "Supplier"), ("Factory", "Factory"), ("ContractType", "Contract type"),
            ("Quantity", "Quantity *"), ("QuantityUnit", "Unit *"), ("Package", "Package"), ("MbUnits", "MB units")
        ]);
        AddTab(tabs, "Routing and Carrier", line,
        [
            ("Pol", "Port of loading (POL)"), ("Pod", "Port of discharge (POD)"),
            ("TransportMode", "Transport Mode"), ("ContainerCount", "Container Count"), ("ContainerType", "Container Type"),
            ("Service", "Service, e.g. CY-CY / CFS-CFS"),
            ("Carrier", "Carrier"), ("CarrierContractId", "Carrier service contract ID"),
            ("SecondNotifyParty", "2nd Notify Party")
        ]);
        AddTab(tabs, "Schedule and Actual Milestones", line,
        [
            ("PlannedEtd", "Planned ETD"), ("PlannedEta", "Planned ETA"),
            ("SentToCarrierDate", "Sent to Carrier Date"), ("CarrierConfirmedDate", "Carrier Confirmation Date"),
            ("ActualEtd", "Actual ETD"), ("ActualEta", "Actual ETA")
        ]);
        save.Click += (_, _) =>
        {
            try
            {
                var candidate = SIExportEditorWindow.Clone(line);
                foreach (var (property, input) in _inputs)
                {
                    object? value;
                    if (input is BookingQueueDatePicker bqDate) value = bqDate.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;
                    else if (input is DatePicker date) value = date.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;
                    else if (input is CheckBox check) value = check.IsChecked == true;
                    else if (input is ComboBox choice) value = choice.IsEditable ? choice.Text.Trim() : choice.SelectedItem?.ToString() ?? "";
                    else
                    {
                        var text = ((TextBox)input).Text.Trim();
                        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        if (type == typeof(decimal))
                        {
                            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number))
                                throw new ArgumentException($"{property.Name}: Enter a valid number");
                            value = number;
                        }
                        else if (type == typeof(int))
                        {
                            if (text.Length == 0) value = null;
                            else if (int.TryParse(text, out var integer)) value = integer;
                            else throw new ArgumentException($"{property.Name}: Enter a whole number");
                        }
                        else value = text;
                    }
                    if (property.Name == nameof(SIExportLine.Supplier) && value is string supplierText)
                    {
                        value = SIExportEditorWindow.NormalizeSupplier(supplierText);
                    }
                    if (property.Name == nameof(SIExportLine.Pol) && value is string polText)
                    {
                        value = SIExportEditorWindow.NormalizePol(polText);
                    }
                    property.SetValue(candidate, value);
                }
                SIExportValidation.ValidateLine(candidate);
                foreach (var property in _inputs.Keys) property.SetValue(line, property.GetValue(candidate));
                DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
        Content = root;
    }

    private void AddTab(TabControl tabs, string title, SIExportLine line, (string Key, string Label)[] fields)
    {
        var panel = new WrapPanel { Margin = new Thickness(12), MaxWidth = 780 };
        foreach (var (key, label) in fields)
        {
            var property = typeof(SIExportLine).GetProperty(key)!;
            var value = property.GetValue(line);
            FrameworkElement input;
            if (property.PropertyType == typeof(DateOnly?))
            {
                var picker = new BookingQueueDatePicker
                {
                    SelectedDate = value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null,
                    DateDisplayFormat = "dd MMM yyyy",
                    Height = 32,
                    MinHeight = 32,
                    Width = double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ToolTip = "Optional. Press Delete to clear the date."
                };
                UiControlProperties.SetButtonCornerRadius(picker, UiTokens.CornerRadiusSmall);
                input = picker;
            }
            else if (key is "Cancelled" or "SourceReviewRequired") input = new CheckBox { IsChecked = (bool)value!, Content = key == "Cancelled" ? "Cancelled" : "Source review required (clear after review)", MinHeight = 32 };
            else if (key == "QuantityUnit") input = SIExportEditorWindow.Choice(["MT", "KG", "TON", "UNKNOWN"], (string)value!);
            else if (key == "TransportMode") input = SIExportEditorWindow.Choice(["CONTAINER", "BREAKBULK", "SHARED_CONTAINER"], (string)value!);
            else if (_catalog.TryGetValue(char.ToLowerInvariant(key[0]) + key[1..], out var options))
            {
                var initialText = key == "Supplier" ? SIExportEditorWindow.NormalizeSupplier((string?)value)
                    : key == "Pol" ? SIExportEditorWindow.NormalizePol((string?)value)
                    : ((string?)value ?? "");
                var choice = SIExportEditorWindow.Choice(options, initialText);
                choice.IsEditable = true; choice.Text = initialText;
                input = choice;
            }
            else
            {
                var box = SIExportEditorWindow.Input(Convert.ToString(value, CultureInfo.CurrentCulture) ?? "");
                if (key == "SourceReference" && line.SourceData.Length > 0) box.IsReadOnly = true;
                if (key == "Remark") { box.AcceptsReturn = true; box.Height = 80; box.TextWrapping = TextWrapping.Wrap; }
                input = box;
            }
            _inputs[property] = input;
            SIExportEditorWindow.AddLabeled(panel, label, input, 350);
        }
        tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
}

internal static class SIExportUi
{
    internal static Button Button(string label, Action action)
    {
        var button = new Button { Content = label, MinHeight = 32, Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(0, 0, 8, 0) };
        button.Click += (_, _) => action(); return button;
    }

    internal static void ApplySoftGridTheme(DataGrid grid)
    {
        grid.Background = Brushes.White;
        grid.BorderBrush = new SolidColorBrush(Color.FromRgb(225, 231, 239));
        grid.BorderThickness = new Thickness(1);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        grid.HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(225, 231, 239));
        grid.VerticalGridLinesBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        grid.RowBackground = Brushes.White;
        grid.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        grid.Resources[SystemColors.HighlightBrushKey] = new SolidColorBrush(Color.FromRgb(230, 244, 241));
        grid.Resources[SystemColors.HighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
        grid.Resources[SystemColors.InactiveSelectionHighlightBrushKey] = new SolidColorBrush(Color.FromRgb(241, 245, 249));
        grid.Resources[SystemColors.InactiveSelectionHighlightTextBrushKey] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
    }

    internal static void Column(DataGrid grid, string title, string path, double width = 120,
        bool center = false, string? stringFormat = null)
    {
        var binding = new Binding(path);
        if (!string.IsNullOrWhiteSpace(stringFormat)) binding.StringFormat = stringFormat;
        var column = new DataGridTextColumn { Header = title, Binding = binding, Width = width };
        if (center)
        {
            column.ElementStyle = new Style(typeof(TextBlock));
            column.ElementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            column.ElementStyle.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            column.ElementStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            var headerStyle = new Style(typeof(DataGridColumnHeader));
            if ((grid.TryFindResource(typeof(DataGridColumnHeader)) ?? System.Windows.Application.Current?.TryFindResource(typeof(DataGridColumnHeader))) is Style baseHeader)
                headerStyle.BasedOn = baseHeader;
            headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            column.HeaderStyle = headerStyle;
        }
        grid.Columns.Add(column);
    }

    internal static void StatusBadgeColumn(DataGrid grid, string title = "Status", double width = 120)
    {
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(7, 1.5, 7, 1.5));
        borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(UiTokens.CornerRadiusSmallValue));
        borderFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        borderFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetValue(TextBlock.FontSizeProperty, 11d);
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        textFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        textFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        textFactory.SetBinding(TextBlock.TextProperty, new Binding("Status"));

        borderFactory.AppendChild(textFactory);

        borderFactory.SetBinding(Border.BackgroundProperty, new Binding("Status") { Converter = SIStatusBrushConverter.Background });
        borderFactory.SetBinding(Border.BorderBrushProperty, new Binding("Status") { Converter = SIStatusBrushConverter.Border });
        textFactory.SetBinding(TextBlock.ForegroundProperty, new Binding("Status") { Converter = SIStatusBrushConverter.Foreground });

        var headerStyle = new Style(typeof(DataGridColumnHeader));
        if ((grid.TryFindResource(typeof(DataGridColumnHeader)) ?? System.Windows.Application.Current?.TryFindResource(typeof(DataGridColumnHeader))) is Style baseHeader)
            headerStyle.BasedOn = baseHeader;
        headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));

        var column = new DataGridTemplateColumn
        {
            Header = title,
            Width = width,
            HeaderStyle = headerStyle,
            SortMemberPath = "Status",
            CellTemplate = new DataTemplate { VisualTree = borderFactory }
        };
        grid.Columns.Add(column);
    }

    internal static DataGrid LineGrid(System.Collections.IEnumerable rows)
    {
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, RowHeight = 32,
            HeadersVisibility = DataGridHeadersVisibility.Column };
        ApplySoftGridTheme(grid);

        var idx = 1;
        foreach (var item in rows)
        {
            if (item is SIExportLine line) line.No = idx++;
        }

        var centeredColumns = new HashSet<string>(StringComparer.Ordinal)
            { "No", "Plant", "Grade", "Code", "Quantity", "QuantityUnit", "Package", "MbUnits", "ContainerCount" };
        var dateColumns = new HashSet<string>(StringComparer.Ordinal)
            { "PlannedEtd", "PlannedEta" };

        var columns = new (string Label, string Key, double Width)[]
        {
            ("No.", "No", 48d),
            ("Contract #", "Contract", 110d),
            ("Customer PO", "CustomerPo", 120d),
            ("Plant", "Plant", 75d),
            ("Grade", "Grade", 95d),
            ("Code", "Code", 75d),
            ("Quantity", "Quantity", 90d),
            ("Unit", "QuantityUnit", 55d),
            ("Package", "Package", 75d),
            ("MB units", "MbUnits", 75d),
            ("POL", "Pol", 130d),
            ("POD", "Pod", 120d),
            ("Containers", "ContainerCount", 85d),
            ("Planned ETD", "PlannedEtd", 105d),
            ("Planned ETA", "PlannedEta", 105d),
            ("Carrier", "Carrier", 100d)
        };

        foreach (var (label, key, width) in columns)
        {
            Column(grid, label, key, width: width, center: centeredColumns.Contains(key),
                stringFormat: dateColumns.Contains(key) ? "dd MMM yyyy" : (key == "Quantity" ? "N3" : null));
        }

        grid.Loaded += (_, _) =>
        {
            if (grid.Items.Count > 0 && grid.Columns.Count > 0)
            {
                grid.ScrollIntoView(grid.Items[0], grid.Columns[0]);
            }
        };

        return grid;
    }
}

internal sealed class SIStatusBrushConverter : IValueConverter
{
    public static readonly SIStatusBrushConverter Background = new("Bg");
    public static readonly SIStatusBrushConverter Border = new("Border");
    public static readonly SIStatusBrushConverter Foreground = new("Fg");

    private static readonly SolidColorBrush DraftFg = CreateFrozenBrush(Color.FromRgb(71, 85, 105));      // #475569
    private static readonly SolidColorBrush DraftBg = CreateFrozenBrush(Color.FromRgb(241, 245, 249));    // #F1F5F9
    private static readonly SolidColorBrush DraftBorder = CreateFrozenBrush(Color.FromRgb(203, 213, 225)); // #CBD5E1

    private static readonly SolidColorBrush ForecastFg = CreateFrozenBrush(Color.FromRgb(2, 132, 199));     // #0284C7
    private static readonly SolidColorBrush ForecastBg = CreateFrozenBrush(Color.FromRgb(224, 242, 254));   // #E0F2FE
    private static readonly SolidColorBrush ForecastBorder = CreateFrozenBrush(Color.FromRgb(186, 230, 253)); // #BAE6FD

    private static readonly SolidColorBrush ConfirmedFg = CreateFrozenBrush(Color.FromRgb(15, 118, 110));   // #0F766E
    private static readonly SolidColorBrush ConfirmedBg = CreateFrozenBrush(Color.FromRgb(234, 247, 239));  // #EAF7EF
    private static readonly SolidColorBrush ConfirmedBorder = CreateFrozenBrush(Color.FromRgb(185, 227, 200)); // #B9E3C8

    private static readonly SolidColorBrush CancelledFg = CreateFrozenBrush(Color.FromRgb(180, 35, 24));     // #B42318
    private static readonly SolidColorBrush CancelledBg = CreateFrozenBrush(Color.FromRgb(254, 240, 239));   // #FEF0EF
    private static readonly SolidColorBrush CancelledBorder = CreateFrozenBrush(Color.FromRgb(244, 199, 195)); // #F4C7C3

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static (Brush Fg, Brush Bg, Brush Border) GetPalette(string? status)
    {
        return status?.Trim().ToUpperInvariant() switch
        {
            "FORECAST" => (ForecastFg, ForecastBg, ForecastBorder),
            "CONFIRMED" or "APPROVED" => (ConfirmedFg, ConfirmedBg, ConfirmedBorder),
            "CANCELLED" or "REJECTED" => (CancelledFg, CancelledBg, CancelledBorder),
            _ => (DraftFg, DraftBg, DraftBorder)
        };
    }

    private readonly string _target;
    public SIStatusBrushConverter(string target) => _target = target;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (fg, bg, border) = GetPalette(value?.ToString());
        return _target switch
        {
            "Fg" => fg,
            "Border" => border,
            _ => bg
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

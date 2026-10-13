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

    public SIExportEditorWindow(SIExportSchedule? source, bool copy, Dictionary<string, List<string>> catalog, Func<SIExportSave, Task> save)
    {
        _catalog = catalog;
        var document = source is null ? new SIExportDocument() : Clone(source.Document);
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
        AddLabeled(header, "ชื่อชุด SI *", _title, 280);
        AddLabeled(header, "ผู้รับ / Business unit *", _recipient, 230);
        AddLabeled(header, "ปีส่งออก *", _year, 100);
        AddLabeled(header, "เดือน *", _month, 90);
        AddLabeled(header, "สถานะแผน", _status, 140);
        var notes = new WrapPanel(); Grid.SetRow(notes, 2); root.Children.Add(notes);
        _remark = Input(document.Remark); _reason = Input("");
        AddLabeled(notes, "หมายเหตุ", _remark, 530);
        AddLabeled(notes, source is null || copy ? "เหตุผล / แหล่งข้อมูล" : "เหตุผลการแก้ไข revision *", _reason, 530);
        _lines = new ObservableCollection<SIExportLine>(document.Lines);
        _grid = SIExportUi.LineGrid(_lines);
        _grid.MouseDoubleClick += (_, _) => EditLine();
        Grid.SetRow(_grid, 4); root.Children.Add(_grid);
        var tools = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
        Grid.SetRow(tools, 3); root.Children.Add(tools);
        tools.Children.Add(SIExportUi.Button("เพิ่มสัญญา", () =>
        {
            var line = new SIExportLine();
            if (ShowLineEditor(line)) _lines.Add(line);
        }));
        tools.Children.Add(SIExportUi.Button("แก้ไขรายการ", EditLine));
        tools.Children.Add(SIExportUi.Button("ดูค่าต้นฉบับ Excel", () =>
        {
            if (_grid.SelectedItem is SIExportLine selected) SIExportSourceWindow.Show(this, selected);
        }));
        tools.Children.Add(SIExportUi.Button("คัดลอกรายการ", () =>
        {
            if (_grid.SelectedItem is not SIExportLine selected) return;
            var line = Clone(selected); line.Id = Guid.NewGuid().ToString();
            line.SentToCarrierDate = line.CarrierConfirmedDate = line.ActualEtd = line.ActualEta = null;
            if (ShowLineEditor(line)) _lines.Add(line);
        }));
        tools.Children.Add(SIExportUi.Button("นำรายการออก", () =>
        {
            if (_grid.SelectedItem is SIExportLine selected) _lines.Remove(selected);
        }));
        tools.Children.Add(new TextBlock { Text = "ดับเบิลคลิกเพื่อแก้ไข · Forecast เติมรายละเอียดขนส่งภายหลังได้",
            VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.SlateGray });
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        Grid.SetRow(footer, 5); root.Children.Add(footer);
        var cancel = SIExportUi.Button("ปิด", Close); DockPanel.SetDock(cancel, Dock.Right); footer.Children.Add(cancel);
        var saveButton = SIExportUi.Button("บันทึก SI / Revision", () => { });
        DockPanel.SetDock(saveButton, Dock.Right); footer.Children.Add(saveButton);
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick,
            MaxWidth = 740, VerticalAlignment = VerticalAlignment.Center };
        footer.Children.Add(error);
        saveButton.Click += async (_, _) =>
        {
            try
            {
                _saving = true; root.IsEnabled = false; error.Text = "";
                if (!int.TryParse(_year.Text, out var year)) throw new ArgumentException("ปีส่งออกต้องเป็นตัวเลข ค.ศ.");
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
                    throw new ArgumentException("กรุณาระบุเหตุผลการแก้ไข revision");
                await save(payload);
                _saving = false; DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
            finally { _saving = false; root.IsEnabled = true; }
        };
        Closing += (_, e) => e.Cancel = _saving;
        Content = root;
    }

    private void EditLine()
    {
        if (_grid.SelectedItem is not SIExportLine line) return;
        var edited = Clone(line);
        if (!ShowLineEditor(edited)) return;
        _lines[_lines.IndexOf(line)] = edited;
        _grid.SelectedItem = edited;
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
        _catalog = catalog;
        Title = "Shipment line · สัญญาและข้อมูลขนส่ง";
        Width = 840; Height = 720; MinWidth = 740; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = new FontFamily("Leelawadee UI");
        var root = new DockPanel { Margin = new Thickness(18) };
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var save = SIExportUi.Button("ใช้ข้อมูลรายการนี้", () => { });
        DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
        var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, MaxWidth = 580 };
        footer.Children.Add(error);
        var tabs = new TabControl(); root.Children.Add(tabs);
        AddTab(tabs, "สัญญาและปริมาณ", line,
        [
            ("Contract", "Contract # (หรือระบุ PO)"), ("CustomerPo", "Customer PO"),
            ("FullContract", "Full contract A+B"), ("Plant", "Plant"), ("Grade", "Grade"), ("Code", "Code"),
            ("Supplier", "Supplier"), ("Factory", "Factory"), ("ContractType", "Contract type"),
            ("Quantity", "Quantity *"), ("QuantityUnit", "หน่วย *"), ("Package", "Package"), ("MbUnits", "MB units")
        ]);
        AddTab(tabs, "เส้นทางและผู้ขนส่ง", line,
        [
            ("Pol", "Port of loading (POL)"), ("Pod", "Port of discharge (POD)"),
            ("TransportMode", "รูปแบบขนส่ง"), ("ContainerCount", "จำนวนตู้"), ("ContainerType", "ชนิดตู้"),
            ("SharedContainerGroup", "รหัสกลุ่มแชร์ตู้"), ("Service", "Service เช่น CY-CY / CFS-CFS"),
            ("Carrier", "Carrier"), ("CarrierContractId", "Carrier service contract ID"),
            ("Vessel", "Vessel / Voyage"), ("SecondNotifyParty", "2nd Notify Party")
        ]);
        AddTab(tabs, "กำหนดการและสถานะจริง", line,
        [
            ("PlannedEtd", "กำหนดออกเดินทาง (ETD)"), ("PlannedEta", "กำหนดถึง (ETA)"),
            ("SentToCarrierDate", "วันที่ส่งให้ผู้ขนส่ง"), ("CarrierConfirmedDate", "วันที่ผู้ขนส่งยืนยัน"),
            ("ActualEtd", "วันออกเดินทางจริง"), ("ActualEta", "วันถึงปลายทางจริง"),
            ("Cancelled", "ยกเลิกรายการ"), ("SourceReviewRequired", "ยังต้องตรวจค่าต้นฉบับ Excel"),
            ("Remark", "หมายเหตุ"), ("SourceReference", "แหล่งข้อมูลประวัติ (ถ้ามี)")
        ]);
        save.Click += (_, _) =>
        {
            try
            {
                var candidate = SIExportEditorWindow.Clone(line);
                foreach (var (property, input) in _inputs)
                {
                    object? value;
                    if (input is DatePicker date) value = date.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null;
                    else if (input is CheckBox check) value = check.IsChecked == true;
                    else if (input is ComboBox choice) value = choice.IsEditable ? choice.Text.Trim() : choice.SelectedItem?.ToString() ?? "";
                    else
                    {
                        var text = ((TextBox)input).Text.Trim();
                        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        if (type == typeof(decimal))
                        {
                            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var number))
                                throw new ArgumentException($"{property.Name}: กรุณาระบุตัวเลข");
                            value = number;
                        }
                        else if (type == typeof(int))
                        {
                            if (text.Length == 0) value = null;
                            else if (int.TryParse(text, out var integer)) value = integer;
                            else throw new ArgumentException($"{property.Name}: กรุณาระบุจำนวนเต็ม");
                        }
                        else value = text;
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
                input = new DatePicker { SelectedDate = value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null,
                    MinHeight = 32, SelectedDateFormat = DatePickerFormat.Short,
                    ToolTip = "เว้นว่างได้ · กด Delete เพื่อล้างวันที่" };
            else if (key is "Cancelled" or "SourceReviewRequired") input = new CheckBox { IsChecked = (bool)value!, Content = key == "Cancelled" ? "Cancelled" : "ต้องตรวจต้นฉบับ (เอาเครื่องหมายออกเมื่อแก้ไขแล้ว)", MinHeight = 32 };
            else if (key == "QuantityUnit") input = SIExportEditorWindow.Choice(["MT", "KG", "TON", "UNKNOWN"], (string)value!);
            else if (key == "TransportMode") input = SIExportEditorWindow.Choice(["CONTAINER", "BREAKBULK", "SHARED_CONTAINER"], (string)value!);
            else if (_catalog.TryGetValue(char.ToLowerInvariant(key[0]) + key[1..], out var options))
            {
                var choice = SIExportEditorWindow.Choice(options, (string?)value ?? "");
                choice.IsEditable = true; choice.Text = (string?)value ?? "";
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

    internal static void Column(DataGrid grid, string title, string path, double width = 120)
        => grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(path), Width = width });

    internal static DataGrid LineGrid(System.Collections.IEnumerable rows)
    {
        var grid = new DataGrid { ItemsSource = rows, AutoGenerateColumns = false, IsReadOnly = true,
            CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow, RowHeight = 32,
            HeadersVisibility = DataGridHeadersVisibility.Column, Background = Brushes.White };
        foreach (var (label, key) in new[] {
            ("Contract #", "Contract"), ("Customer PO", "CustomerPo"), ("Full Contract A+B", "FullContract"),
            ("Plant", "Plant"), ("Grade", "Grade"), ("Code", "Code"), ("Supplier", "Supplier"), ("Factory", "Factory"),
            ("Contract type", "ContractType"), ("Quantity", "Quantity"), ("Unit", "QuantityUnit"),
            ("Package", "Package"), ("MB units", "MbUnits"), ("POL", "Pol"), ("POD", "Pod"),
            ("Mode", "TransportMode"), ("Containers", "ContainerCount"), ("Container type", "ContainerType"),
            ("Shared group", "SharedContainerGroup"), ("Planned ETD", "PlannedEtd"), ("Planned ETA", "PlannedEta"),
            ("Service", "Service"), ("Carrier", "Carrier"), ("Carrier contract", "CarrierContractId"), ("Vessel", "Vessel"),
            ("2nd Notify", "SecondNotifyParty"), ("Shipment status", "ShipmentStatus"), ("Sent to carrier", "SentToCarrierDate"),
            ("Carrier confirmed", "CarrierConfirmedDate"), ("Actual ETD", "ActualEtd"), ("Actual ETA", "ActualEta"),
            ("Cancelled", "Cancelled"), ("Source review", "SourceReviewRequired"), ("Remark", "Remark"), ("Source", "SourceReference") }) Column(grid, label, key);
        return grid;
    }
}

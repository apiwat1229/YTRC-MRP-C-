using System.Globalization;
using System.Text.Json;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private enum PurchasingFieldKind { Text, Multiline, Number, Date, Choice }

    private sealed record PurchasingChoice(string Value, string Label, string? PostalCode = null)
    {
        public override string ToString() => Label;
    }
    private sealed record PurchasingField(
        string Key, string Label, PurchasingFieldKind Kind = PurchasingFieldKind.Text,
        string InitialValue = "", bool Required = false,
        IReadOnlyList<PurchasingChoice>? Choices = null, bool OptionalDate = false);
    private sealed class PurchasingDateInput : Grid
    {
        public DateTime? SelectedDate { get; set; }
    }

    private async Task LoadPurchasingRecordsAsync()
    {
        var section = _purchasingSection;
        if (section is not ("Supplier" or "Rubber Type" or "Order")) return;
        var version = ++_purchasingLoadVersion;
        if (_session.IsPreview)
        {
            _purchasingAllRecords = [];
            _purchasingMessage = "Sign in to query live purchasing records.";
            RenderCurrentPage();
            return;
        }

        _purchasingLoading = true;
        _purchasingMessage = "";
        RenderCurrentPage();
        var endpoint = section switch
        {
            "Supplier" => "/api/suppliers?includeDeleted=true",
            "Rubber Type" => "/api/rubber-types?includeDeleted=true",
            _ => "/api/orders"
        };
        var resultTask = _apiClient.GetAsync(endpoint, _session.Token);
        var provincesTask = section == "Supplier" && _purchasingProvinces.Count == 0
            ? _apiClient.GetAsync("/api/master/provinces", _session.Token)
            : null;
        var rubberTypesTask = section == "Supplier"
            ? _apiClient.GetAsync("/api/rubber-types?includeDeleted=true", _session.Token)
            : null;
        var result = await resultTask;
        if (provincesTask is not null)
        {
            var provincesResult = await provincesTask;
            if (provincesResult.Success)
            {
                _purchasingProvinces = ExtractArray(provincesResult.Data)
                    .Select(item => new PurchasingChoice(ReadProperty(item, "id"),
                        ReadProperty(item, "name_th", "name_en", "name")))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(item.Label))
                    .ToList();
            }
        }
        if (rubberTypesTask is not null)
        {
            var rubberTypesResult = await rubberTypesTask;
            if (rubberTypesResult.Success)
                _purchasingRubberTypes = ExtractArray(rubberTypesResult.Data);
        }
        if (version != _purchasingLoadVersion || section != _purchasingSection) return;

        _purchasingLoading = false;
        if (!result.Success)
        {
            _purchasingAllRecords = [];
            _purchasingMessage = $"Failed to load {section.ToLowerInvariant()} records: {result.Error}";
        }
        else
        {
            _purchasingAllRecords = ExtractArray(result.Data)
                .Select(item => MapPurchasingRecord(section, item, _purchasingProvinces, _purchasingRubberTypes))
                .ToList();
            _purchasingMessage = $"Loaded {_purchasingAllRecords.Count} {section.ToLowerInvariant()} record(s).";
        }
        _purchasingSelectedRecord = null;
        RenderCurrentPage();
    }

    private async Task OpenPurchasingEditorAsync(string section, PurchasingRecord? record)
    {
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Sign in to create or edit purchasing records.", "Sign in required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (section == "Order") await LoadOrderEditorDependenciesAsync();
        else if (section == "Supplier")
            await LoadSupplierLocationOptionsAsync(record?.Raw ?? default);

        var raw = record?.Raw ?? default;
        var fields = section switch
        {
            "Supplier" => BuildSupplierFields(raw),
            "Rubber Type" => BuildRubberTypeFields(raw),
            _ => await BuildOrderFieldsAsync(raw, record is null)
        };
        var title = (section, record is null) switch
        {
            ("Supplier", true) => "Add Supplier",
            ("Supplier", false) => "Edit Supplier",
            ("Rubber Type", true) => "Add Rubber Type",
            ("Rubber Type", false) => "Edit Rubber Type",
            (_, true) => "Create Order",
            _ => "Edit Order"
        };

        var payload = ShowPurchasingEditor(title, fields);
        if (payload is null) return;

        var route = section switch { "Supplier" => "/api/suppliers", "Rubber Type" => "/api/rubber-types", _ => "/api/orders" };
        var endpoint = record is null ? route : $"{route}/{Uri.EscapeDataString(record.Id)}";
        var response = record is null
            ? await _apiClient.PostJsonAsync(endpoint, payload, _session.Token)
            : await _apiClient.PatchJsonAsync(endpoint, payload, _session.Token);
        if (!response.Success)
        {
            MessageBox.Show(this, response.Error, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _purchasingMessage = record is null ? $"{section} record created successfully." : $"{section} record updated successfully.";
        await LoadPurchasingRecordsAsync();
    }

    private List<PurchasingField> BuildSupplierFields(JsonElement raw)
    {
        return
        [
            new("code", "Code", InitialValue: ReadProperty(raw, "code"), Required: true),
            new("name", "Name", InitialValue: ReadProperty(raw, "name"), Required: true),
            new("provinceId", "Province", PurchasingFieldKind.Choice,
                InitialValue: ReadSupplierProvinceId(raw),
                Required: _purchasingProvinces.Count > 0, Choices: _purchasingProvinces),
            new("districtId", "District", PurchasingFieldKind.Choice,
                InitialValue: ReadSupplierDistrictId(raw),
                Required: _purchasingProvinces.Count > 0, Choices: _purchasingDistricts),
            new("subdistrictId", "Subdistrict", PurchasingFieldKind.Choice,
                InitialValue: ReadSupplierSubdistrictId(raw),
                Required: _purchasingProvinces.Count > 0, Choices: _purchasingSubdistricts),
            new("zipCode", "Zip code", InitialValue: ReadSupplierZipCode(raw),
                Required: _purchasingProvinces.Count > 0),
            new("firstName", "Contact first name", InitialValue: ReadProperty(raw, "firstName")),
            new("lastName", "Contact last name", InitialValue: ReadProperty(raw, "lastName")),
            new("taxId", "Tax ID", InitialValue: ReadProperty(raw, "taxId")),
            new("phone", "Phone", InitialValue: MaskPhoneInput(ReadProperty(raw, "phone"))),
            new("email", "Email", InitialValue: ReadProperty(raw, "email")),
            new("status", "Status", PurchasingFieldKind.Choice,
                DefaultFor(raw, "status", "ACTIVE"), Choices:
                [new("ACTIVE", "Active"), new("INACTIVE", "Inactive"), new("SUSPENDED", "Suspended")]),
            new("address", "Address", PurchasingFieldKind.Multiline, ReadProperty(raw, "address")),
            new("rubberTypeCodes", "Rubber type codes (comma-separated)", PurchasingFieldKind.Text,
                string.Join(", ", ReadArray(raw, "rubberTypeCodes").DefaultIfEmpty(ReadProperty(raw, "rubber_type_codes"))))
        ];
    }

    private static List<PurchasingField> BuildRubberTypeFields(JsonElement raw) =>
    [
        new("code", "Rubber type code", InitialValue: ReadProperty(raw, "code"), Required: true),
        new("name", "Rubber type name", InitialValue: ReadProperty(raw, "name"), Required: true),
        new("category", "Category", PurchasingFieldKind.Choice,
            DefaultFor(raw, "category", "Cuplump"), Required: true,
            Choices: [new("Cuplump", "Cuplump"), new("USS", "USS")])
    ];

    private async Task<List<PurchasingField>> BuildOrderFieldsAsync(JsonElement raw, bool creating)
    {
        var suppliers = _purchasingSuppliers.Select(item => new PurchasingChoice(ReadProperty(item, "id"),
            $"{ReadProperty(item, "code")} · {ReadProperty(item, "name")}".Trim(' ', '·'))).ToList();
        var rubberTypes = _purchasingRubberTypes
            .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt")))
            .Select(item => new PurchasingChoice(ReadProperty(item, "id"),
                $"{ReadProperty(item, "code")} · {ReadProperty(item, "name")}".Trim(' ', '·'))).ToList();
        if (!creating && !string.IsNullOrWhiteSpace(ReadProperty(raw, "supplier_id")) &&
            suppliers.All(item => item.Value != ReadProperty(raw, "supplier_id")))
            suppliers.Add(new(ReadProperty(raw, "supplier_id"), ReadProperty(raw, "supplier_name")));
        if (!creating && !string.IsNullOrWhiteSpace(ReadProperty(raw, "rubber_type_id")) &&
            rubberTypes.All(item => item.Value != ReadProperty(raw, "rubber_type_id")))
            rubberTypes.Add(new(ReadProperty(raw, "rubber_type_id"), ReadProperty(raw, "rubber_type_name")));

        var defaultNumber = "";
        if (creating)
        {
            var latest = await _apiClient.GetAsync("/api/orders/get-latest-order-id?prefix=FW", _session.Token);
            if (latest.Success) defaultNumber = ReadProperty(latest.Data, "order_id");
        }
        return
        [
            new("order_type", "Contract type", PurchasingFieldKind.Choice,
                DefaultFor(raw, "order_type", "FW"), Required: true,
                Choices: [new("FW", "Forward contract (FW)"), new("SP", "Spot contract (SP)")]),
            new("order_no", "Order number", InitialValue: creating ? defaultNumber : ReadProperty(raw, "order_no"), Required: true),
            new("start_date", "Create date", PurchasingFieldKind.Date,
                DefaultFor(raw, "start_date", DateTime.Now.ToString("yyyy-MM-dd")), Required: true),
            new("due_date", "Issue / due date", PurchasingFieldKind.Date, ReadProperty(raw, "due_date"), Required: true),
            new("delivery_date", "Delivery date (optional)", PurchasingFieldKind.Date, ReadProperty(raw, "delivery_date"), OptionalDate: true),
            new("supplier_id", "Name", PurchasingFieldKind.Choice, ReadProperty(raw, "supplier_id"), Required: true, Choices: suppliers),
            new("rubber_type_id", "Rubber type", PurchasingFieldKind.Choice, ReadProperty(raw, "rubber_type_id"), Required: true, Choices: rubberTypes),
            new("gross_wt", "Gross weight (kg)", PurchasingFieldKind.Number, ReadProperty(raw, "gross_wt"), Required: true),
            new("drc", "DRC (%)", PurchasingFieldKind.Number, DefaultFor(raw, "drc", "62.0"), Required: true),
            new("price", "Price per kg", PurchasingFieldKind.Number, ReadProperty(raw, "price"), Required: true),
            new("other_cost", "Other cost", PurchasingFieldKind.Number, DefaultFor(raw, "other_cost", "0")),
            new("status", "Status", PurchasingFieldKind.Choice,
                DefaultFor(raw, "status", "WaitingApproval"), Choices:
                [
                    new("WaitingApproval", "Waiting approval"), new("OutStanding", "Outstanding"),
                    new("PoPri", "PoPri"), new("BakingTest", "Baking test"),
                    new("UssRecieved", "USS received"), new("Completed", "Completed"),
                    new("Cancelled", "Cancelled")
                ]),
            new("description", "Description", PurchasingFieldKind.Multiline, ReadProperty(raw, "description"))
        ];
    }

    private async Task LoadOrderEditorDependenciesAsync()
    {
        var supplierTask = _apiClient.GetAsync("/api/suppliers", _session.Token);
        var rubberTask = _apiClient.GetAsync("/api/rubber-types", _session.Token);
        await Task.WhenAll(supplierTask, rubberTask);
        var suppliersResult = await supplierTask;
        var rubberResult = await rubberTask;
        if (suppliersResult.Success) _purchasingSuppliers = ExtractArray(suppliersResult.Data);
        if (rubberResult.Success) _purchasingRubberTypes = ExtractArray(rubberResult.Data);
    }

    private Dictionary<string, object?>? ShowPurchasingEditor(string title, IReadOnlyList<PurchasingField> fields)
    {
        var isSupplierForm = fields.Any(field => field.Key == "provinceId");
        var isBookingForm = fields.Any(field => field.Key == "slot") &&
                            fields.Any(field => field.Key == "truckRegister");
        var bookingDialogHeight = Math.Min(
            Math.Min(900, Math.Max(780, 210 + fields.Count * 74)),
            Math.Max(640, SystemParameters.WorkArea.Height - 40));
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            Width = isSupplierForm ? 860 : isBookingForm ? 760 : 680,
            Height = isSupplierForm ? 720 : isBookingForm
                ? bookingDialogHeight
                : Math.Min(820, Math.Max(560, 170 + fields.Count * 66)),
            MinWidth = isSupplierForm ? 760 : isBookingForm ? 680 : 560,
            MinHeight = isSupplierForm ? 560 : isBookingForm ? 700 : 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            Background = (Brush)FindResource("CanvasBrush"),
            FontFamily = (FontFamily)FindResource("AppFontFamily")
        };
        var layout = new DockPanel { Margin = new Thickness(22) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var save = new Button { Content = "Save", MinWidth = 100, Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("PrimaryButton") };
        var cancel = new Button { Content = "Cancel", MinWidth = 100, IsCancel = true };
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        layout.Children.Add(footer);

        var form = isSupplierForm || isBookingForm ? new Grid() : null;
        if (form is not null)
        {
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        var linearForm = isSupplierForm ? null : new StackPanel();
        var controls = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
        var rowIndex = 0;
        var columnIndex = 0;
        foreach (var field in fields)
        {
            FrameworkElement input = field.Kind switch
            {
                PurchasingFieldKind.Choice => MakeChoice(field),
                PurchasingFieldKind.Date => MakeDate(field),
                _ => new TextBox
                {
                    Text = field.InitialValue,
                    MinWidth = 250,
                    Height = field.Kind == PurchasingFieldKind.Multiline ? 76 : 32,
                    Focusable = true,
                    IsTabStop = true,
                    IsReadOnly = false,
                    Cursor = Cursors.IBeam,
                    CaretBrush = (Brush)FindResource("TextBrush"),
                    AcceptsReturn = field.Kind == PurchasingFieldKind.Multiline,
                    TextWrapping = field.Kind == PurchasingFieldKind.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    VerticalContentAlignment = field.Kind == PurchasingFieldKind.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                    VerticalScrollBarVisibility = field.Kind == PurchasingFieldKind.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
                }
            };
            if (isBookingForm && (field.Key is "rubberType" or "trailerRubberType") && input is ComboBox bookingRubberType)
            {
                bookingRubberType.Height = 32;
                bookingRubberType.MinHeight = 32;
            }
            if (input is TextBox editableTextBox)
            {
                editableTextBox.PreviewMouseLeftButtonDown += (_, _) =>
                {
                    if (!editableTextBox.IsKeyboardFocusWithin)
                        editableTextBox.Focus();
                };
            }
            if (field.Kind == PurchasingFieldKind.Number && input is TextBox numeric)
            {
                numeric.PreviewTextInput += (_, args) => args.Handled = args.Text.Any(character => !char.IsDigit(character) && character is not ('.' or ',' or '-'));
                if (field.Key == "estimatedWeight")
                    numeric.ToolTip = "Enter the estimated weight in kilograms";
            }
            controls[field.Key] = input;

            var fieldView = Field(field.Required ? $"{field.Label} *" : field.Label, input);
            if (form is null)
            {
                linearForm!.Children.Add(fieldView);
                linearForm.Children.Add(new Border { Height = 9, Opacity = 0 });
                continue;
            }

            if (isBookingForm)
            {
                var section = field.Key switch
                {
                    "supplierId" => "Supplier Info",
                    "truckRegister" => "Truck Info",
                    "rubberType" => "Product Info",
                    _ => null
                };
                if (section is not null)
                    AddBookingSectionHeader(form, section, ref rowIndex);

                var hasBookingDate = fields.Any(item => item.Key == "date");
                var hasEstimatedWeight = fields.Any(item => item.Key == "estimatedWeight");
                var hasTrailerRubberType = fields.Any(item => item.Key == "trailerRubberType");
                var spansBookingColumns = field.Key is "supplierId" or "note" ||
                                          field.Key == "rubberType" && !hasEstimatedWeight && !hasTrailerRubberType ||
                                          field.Key == "slot" && !hasBookingDate;
                if (spansBookingColumns && columnIndex != 0)
                {
                    rowIndex++;
                    columnIndex = 0;
                }
                while (form.RowDefinitions.Count <= rowIndex)
                    form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (fieldView is FrameworkElement bookingFieldElement)
                {
                    bookingFieldElement.HorizontalAlignment = HorizontalAlignment.Stretch;
                    bookingFieldElement.Margin = new Thickness(columnIndex == 1 ? 12 : 0, 0,
                        columnIndex == 0 && !spansBookingColumns ? 12 : 0, 12);
                }
                Grid.SetRow(fieldView, rowIndex);
                Grid.SetColumn(fieldView, columnIndex);
                if (spansBookingColumns) Grid.SetColumnSpan(fieldView, 2);
                form.Children.Add(fieldView);

                if (spansBookingColumns)
                {
                    rowIndex++;
                    columnIndex = 0;
                }
                else if (columnIndex == 0)
                {
                    columnIndex = 1;
                }
                else
                {
                    rowIndex++;
                    columnIndex = 0;
                }
                continue;
            }

            var spansColumns = field.Key is "address" or "rubberTypeCodes";
            if (spansColumns && columnIndex != 0)
            {
                rowIndex++;
                columnIndex = 0;
            }
            while (form.RowDefinitions.Count <= rowIndex)
                form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (fieldView is FrameworkElement fieldElement)
                fieldElement.Margin = new Thickness(0, 0, columnIndex == 0 && !spansColumns ? 14 : 0, 10);
            Grid.SetRow(fieldView, rowIndex);
            Grid.SetColumn(fieldView, columnIndex);
            if (spansColumns) Grid.SetColumnSpan(fieldView, 2);
            form.Children.Add(fieldView);

            if (spansColumns)
            {
                rowIndex++;
                columnIndex = 0;
            }
            else if (columnIndex == 0)
            {
                columnIndex = 1;
            }
            else
            {
                rowIndex++;
                columnIndex = 0;
            }
        }
        UIElement formContent = form is not null ? form : linearForm!;
        layout.Children.Add(new ScrollViewer { Content = formContent, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = layout;
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(new Action(() =>
        {
            var firstEditableTextBox = controls.Values.OfType<TextBox>()
                .FirstOrDefault(textBox => textBox.IsEnabled && !textBox.IsReadOnly);
            if (firstEditableTextBox is null) return;
            firstEditableTextBox.Focus();
            firstEditableTextBox.CaretIndex = firstEditableTextBox.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Input);

        if (isSupplierForm)
        {
            var provinceCombo = controls["provinceId"] as ComboBox;
            var districtCombo = controls["districtId"] as ComboBox;
            var subdistrictCombo = controls["subdistrictId"] as ComboBox;
            var zipCodeBox = controls["zipCode"] as TextBox;
            if (controls["code"] is TextBox supplierCodeBox)
                AttachDigitInputMask(supplierCodeBox, 4);
            if (controls["phone"] is TextBox phoneBox)
                AttachSupplierPhoneMask(phoneBox);
            if (zipCodeBox is not null) zipCodeBox.IsReadOnly = true;
            if (districtCombo is not null)
                districtCombo.IsEnabled = !string.IsNullOrWhiteSpace(provinceCombo?.SelectedValue?.ToString());
            if (subdistrictCombo is not null)
                subdistrictCombo.IsEnabled = !string.IsNullOrWhiteSpace(districtCombo?.SelectedValue?.ToString());

            var provinceRequestVersion = 0;
            if (provinceCombo is not null && districtCombo is not null && subdistrictCombo is not null)
            {
                provinceCombo.SelectionChanged += async (_, _) =>
                {
                    var provinceId = provinceCombo.SelectedValue?.ToString() ?? "";
                    var requestVersion = ++provinceRequestVersion;
                    _purchasingDistricts = [];
                    _purchasingSubdistricts = [];
                    districtCombo.SelectedValue = null;
                    districtCombo.ItemsSource = _purchasingDistricts;
                    districtCombo.IsEnabled = provinceId.Length > 0;
                    subdistrictCombo.SelectedValue = null;
                    subdistrictCombo.ItemsSource = _purchasingSubdistricts;
                    subdistrictCombo.IsEnabled = false;
                    if (zipCodeBox is not null) zipCodeBox.Clear();
                    if (provinceId.Length == 0) return;

                    var districts = await LoadDistrictOptionsAsync(provinceId);
                    if (requestVersion != provinceRequestVersion) return;
                    _purchasingDistricts = districts;
                    districtCombo.ItemsSource = districts;
                };

                var districtRequestVersion = 0;
                districtCombo.SelectionChanged += async (_, _) =>
                {
                    var districtId = districtCombo.SelectedValue?.ToString() ?? "";
                    var requestVersion = ++districtRequestVersion;
                    _purchasingSubdistricts = [];
                    subdistrictCombo.SelectedValue = null;
                    subdistrictCombo.ItemsSource = _purchasingSubdistricts;
                    subdistrictCombo.IsEnabled = districtId.Length > 0;
                    if (zipCodeBox is not null) zipCodeBox.Clear();
                    if (districtId.Length == 0) return;

                    var subdistricts = await LoadSubdistrictOptionsAsync(districtId);
                    if (requestVersion != districtRequestVersion) return;
                    _purchasingSubdistricts = subdistricts;
                    subdistrictCombo.ItemsSource = subdistricts;
                };

                subdistrictCombo.SelectionChanged += (_, _) =>
                {
                    var selectedId = subdistrictCombo.SelectedValue?.ToString();
                    var postalCode = (subdistrictCombo.SelectedItem as PurchasingChoice)?.PostalCode
                        ?? _purchasingSubdistricts.FirstOrDefault(item => item.Value == selectedId)?.PostalCode;
                    if (zipCodeBox is not null && !string.IsNullOrWhiteSpace(postalCode))
                        zipCodeBox.Text = postalCode;
                    else if (zipCodeBox is not null && string.IsNullOrWhiteSpace(selectedId))
                        zipCodeBox.Clear();
                };

                subdistrictCombo.Loaded += (_, _) =>
                {
                    var postalCode = (subdistrictCombo.SelectedItem as PurchasingChoice)?.PostalCode
                        ?? _purchasingSubdistricts.FirstOrDefault(item => item.Value == subdistrictCombo.SelectedValue?.ToString())?.PostalCode;
                    if (zipCodeBox is not null && !string.IsNullOrWhiteSpace(postalCode))
                        zipCodeBox.Text = postalCode;
                };
            }
        }

        save.Click += (_, _) =>
        {
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var value = ReadEditorValue(field, controls[field.Key]);
                if (field.Required && (value is null || value is string text && string.IsNullOrWhiteSpace(text)))
                {
                    MessageBox.Show(dialog, $"{field.Label} is required.", "Required field", MessageBoxButton.OK, MessageBoxImage.Warning);
                    controls[field.Key].Focus();
                    return;
                }
                payload[field.Key] = value;
            }
            if (isSupplierForm && payload["code"] is string supplierCode &&
                (supplierCode.Length != 4 || supplierCode.Any(character => !char.IsDigit(character))))
            {
                MessageBox.Show(dialog, "Code must contain exactly 4 digits.", "Invalid Code",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                controls["code"].Focus();
                return;
            }
            if (fields.Any(field => field.Kind == PurchasingFieldKind.Number) &&
                payload.TryGetValue("drc", out var drc) && drc is decimal drcValue && (drcValue <= 0 || drcValue > 100))
            {
                MessageBox.Show(dialog, "DRC must be greater than 0 and no more than 100.", "Check DRC", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (payload.ContainsKey("gross_wt") && payload.ContainsKey("drc") && payload.ContainsKey("price"))
            {
                var gross = payload["gross_wt"] as decimal? ?? 0;
                var dry = payload["drc"] as decimal? ?? 0;
                var price = payload["price"] as decimal? ?? 0;
                var otherCost = payload["other_cost"] as decimal? ?? 0;
                payload["net_wt"] = Math.Round(gross * dry / 100m, 2);
                payload["total_amount"] = Math.Round(gross * dry / 100m * price, 2);
            }

            dialog.Tag = payload;
            dialog.DialogResult = true;
        };

        return dialog.ShowDialog() == true ? dialog.Tag as Dictionary<string, object?> : null;

        void AddBookingSectionHeader(Grid target, string label, ref int targetRow)
        {
            if (columnIndex != 0)
            {
                targetRow++;
                columnIndex = 0;
            }
            while (target.RowDefinitions.Count <= targetRow)
                target.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new Grid { Margin = new Thickness(0, 8, 0, 10) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lineBrush = CreateBrush("#E5E7EB");
            header.Children.Add(new Border { Height = 1, Background = lineBrush, VerticalAlignment = VerticalAlignment.Center });
            var heading = new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                Margin = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(heading, 1);
            header.Children.Add(heading);
            var rightLine = new Border { Height = 1, Background = lineBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rightLine, 2);
            header.Children.Add(rightLine);
            Grid.SetRow(header, targetRow++);
            Grid.SetColumnSpan(header, 2);
            target.Children.Add(header);
        }

        static FrameworkElement MakeChoice(PurchasingField field)
        {
            var combo = new ComboBox
            {
                ItemsSource = field.Choices ?? [],
                DisplayMemberPath = nameof(PurchasingChoice.Label),
                SelectedValuePath = nameof(PurchasingChoice.Value),
                SelectedValue = field.InitialValue,
                MinWidth = 250,
                Height = 32,
                MaxDropDownHeight = 320,
                IsEditable = field.Key == "supplierId",
                IsTextSearchEnabled = false,
                StaysOpenOnEdit = true,
                ToolTip = field.Key == "supplierId" ? "Type to search suppliers" : "Select an option"
            };
            combo.Loaded += (_, _) =>
            {
                if (combo.Template.FindName("PART_EditableTextBox", combo) is not TextBox searchBox) return;
                var filterDelay = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(180)
                };
                filterDelay.Tick += (_, _) =>
                {
                    filterDelay.Stop();
                    if (!combo.IsDropDownOpen || !searchBox.IsKeyboardFocusWithin) return;
                    var query = searchBox.Text.Trim();
                    if (query.Length == 0)
                    {
                        if (combo.Items.Filter is not null) combo.Items.Filter = null;
                        return;
                    }
                    combo.Items.Filter = item => item is PurchasingChoice choice &&
                        choice.Label.Contains(query, StringComparison.OrdinalIgnoreCase);
                };
                combo.DropDownClosed += (_, _) =>
                {
                    filterDelay.Stop();
                    if (combo.Items.Filter is not null) combo.Items.Filter = null;
                };
                searchBox.TextChanged += (_, _) =>
                {
                    if (!searchBox.IsKeyboardFocusWithin) return;
                    if (!combo.IsDropDownOpen) combo.IsDropDownOpen = true;
                    var query = searchBox.Text.Trim();
                    filterDelay.Stop();
                    if (query.Length == 0)
                    {
                        if (combo.Items.Filter is not null) combo.Items.Filter = null;
                        return;
                    }
                    filterDelay.Start();
                };
            };
            return combo;
        }

        FrameworkElement MakeDate(PurchasingField field)
        {
            DateTime? date = DateTime.TryParse(field.InitialValue, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var parsed) ? parsed : null;
            var dateInput = new PurchasingDateInput
            {
                SelectedDate = date,
                Width = UiTokens.DatePickerWidth,
                MinWidth = UiTokens.DatePickerWidth,
                Height = UiTokens.DatePickerHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Choose booking date"
            };
            var dateText = new TextBlock
            {
                Text = date?.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB")) ?? "Select date",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var dateButton = new Button
            {
                Content = dateText,
                Width = UiTokens.DatePickerWidth,
                Height = UiTokens.DatePickerHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 0, 6, 0),
                Background = Brushes.White,
                BorderBrush = CreateBrush("#CFD9E5"),
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = (Style)FindResource(typeof(Button)),
                ToolTip = "Choose booking date",
                Cursor = Cursors.Hand
            };
            var calendar = new System.Windows.Controls.Calendar
            {
                DisplayDate = date ?? DateTime.Today,
                SelectedDate = date,
                SelectionMode = CalendarSelectionMode.SingleDate,
                IsTodayHighlighted = true,
                Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
                Style = CreateBookingQueueCalendarStyle()
            };
            var popup = new System.Windows.Controls.Primitives.Popup
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
                    CornerRadius = UiTokens.CornerRadiusMedium,
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
                calendar.DisplayDate = dateInput.SelectedDate ?? DateTime.Today;
                calendar.SelectedDate = dateInput.SelectedDate;
                popup.IsOpen = true;
            };
            calendar.SelectedDatesChanged += (_, _) =>
            {
                if (calendar.SelectedDate is not DateTime selectedDate) return;
                dateInput.SelectedDate = selectedDate.Date;
                dateText.Text = selectedDate.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
                popup.IsOpen = false;
            };
            dateInput.Children.Add(dateButton);
            return dateInput;
        }
    }

    private static void AttachDigitInputMask(TextBox textBox, int maxDigits)
    {
        textBox.MaxLength = maxDigits;
        textBox.PreviewTextInput += (_, args) =>
            args.Handled = args.Text.Any(character => !char.IsDigit(character));
        var updating = false;
        textBox.TextChanged += (_, _) =>
        {
            if (updating) return;
            var caret = Math.Clamp(textBox.CaretIndex, 0, textBox.Text.Length);
            var digitsBeforeCaret = textBox.Text.Take(caret).Count(char.IsDigit);
            var digits = new string(textBox.Text.Where(char.IsDigit).Take(maxDigits).ToArray());
            if (digits == textBox.Text) return;
            updating = true;
            textBox.Text = digits;
            textBox.CaretIndex = Math.Min(digitsBeforeCaret, digits.Length);
            updating = false;
        };
    }

    private static void AttachSupplierPhoneMask(TextBox textBox)
    {
        textBox.MaxLength = 32;
        textBox.PreviewTextInput += (_, args) =>
            args.Handled = args.Text.Any(character => !char.IsDigit(character));
        var updating = false;
        textBox.TextChanged += (_, _) =>
        {
            if (updating) return;
            var caret = Math.Clamp(textBox.CaretIndex, 0, textBox.Text.Length);
            var digitsBeforeCaret = textBox.Text.Take(caret).Count(char.IsDigit);
            var formatted = MaskPhoneInput(textBox.Text);
            if (formatted == textBox.Text) return;
            updating = true;
            textBox.Text = formatted;
            var newCaret = 0;
            var digitsSeen = 0;
            while (newCaret < formatted.Length && digitsSeen < digitsBeforeCaret)
            {
                if (char.IsDigit(formatted[newCaret])) digitsSeen++;
                newCaret++;
            }
            if (newCaret < formatted.Length && formatted[newCaret] == '-') newCaret++;
            textBox.CaretIndex = newCaret;
            updating = false;
        };
    }

    private static string MaskPhoneInput(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("66", StringComparison.Ordinal))
            digits = $"0{digits[2..]}";
        if (digits.Length > 10) digits = digits[..10];
        if (digits.Length <= 3) return digits;
        if (digits.Length <= 6) return $"{digits[..3]}-{digits[3..]}";
        return $"{digits[..3]}-{digits[3..6]}-{digits[6..]}";
    }

    private static object? ReadEditorValue(PurchasingField field, FrameworkElement input)
    {
        if (input is ComboBox combo)
        {
            var selected = combo.SelectedValue?.ToString();
            if ((field.Key is "provinceId" or "districtId" or "subdistrictId") &&
                int.TryParse(selected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var locationId))
                return locationId;
            return selected;
        }
        if (input is DatePicker datePicker)
            return datePicker.SelectedDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (input is PurchasingDateInput purchasingDate)
            return purchasingDate.SelectedDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        if (input is not TextBox textBox) return null;
        var text = textBox.Text.Trim();
        if (text.Length == 0) return null;
        if (field.Key == "rubberTypeCodes")
            return text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (field.Kind == PurchasingFieldKind.Number)
        {
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return number;
            if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out number)) return number;
            return null;
        }
        return text;
    }

    private async Task DeleteSelectedPurchasingRecordAsync(string section)
    {
        var record = _purchasingSelectedRecord;
        if (record is null) return;
        var operation = section == "Order" ? "permanently delete this order" : "move this record to the Deleted list";
        if (MessageBox.Show(this, $"Are you sure you want to {operation}?", "Confirm delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var route = section switch { "Supplier" => "/api/suppliers", "Rubber Type" => "/api/rubber-types", _ => "/api/orders" };
        var result = await _apiClient.DeleteAsync($"{route}/{Uri.EscapeDataString(record.Id)}", _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Delete failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _purchasingMessage = section == "Order" ? "Order deleted." : $"{section} moved to Deleted.";
        await LoadPurchasingRecordsAsync();
    }

    private async Task RestoreSelectedPurchasingRecordAsync(string section)
    {
        var record = _purchasingSelectedRecord;
        if (record is null || !record.IsDeleted) return;
        var route = section == "Supplier" ? "/api/suppliers" : "/api/rubber-types";
        var result = await _apiClient.PostJsonAsync($"{route}/{Uri.EscapeDataString(record.Id)}/restore", new { }, _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Restore failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _purchasingMessage = $"{section} restored successfully.";
        await LoadPurchasingRecordsAsync();
    }

    private async Task ApproveSelectedOrderAsync()
    {
        var record = _purchasingSelectedRecord;
        if (record is null || ReadProperty(record.Raw, "status") != "WaitingApproval") return;
        if (MessageBox.Show(this, $"Approve order {record.OrderNo}?", "Approve order",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var result = await _apiClient.PutJsonAsync($"/api/orders/{Uri.EscapeDataString(record.Id)}/approve", new { }, _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error, "Approval failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _purchasingMessage = $"Order {record.OrderNo} approved.";
        await LoadPurchasingRecordsAsync();
    }

    private async Task LoadSupplierLocationOptionsAsync(JsonElement raw)
    {
        if (_purchasingProvinces.Count == 0)
            await LoadPurchasingProvincesAsync();

        _purchasingDistricts = [];
        _purchasingSubdistricts = [];

        var provinceId = ReadSupplierProvinceId(raw);
        if (provinceId.Length == 0) return;
        _purchasingDistricts = await LoadDistrictOptionsAsync(provinceId);

        var districtId = ReadSupplierDistrictId(raw);
        if (districtId.Length > 0)
            _purchasingSubdistricts = await LoadSubdistrictOptionsAsync(districtId);
    }

    private async Task LoadPurchasingProvincesAsync()
    {
        var result = await _apiClient.GetAsync("/api/master/provinces", _session.Token);
        if (!result.Success) return;

        _purchasingProvinces = ExtractArray(result.Data)
            .Select(item => new PurchasingChoice(ReadProperty(item, "id"),
                ReadProperty(item, "name_th", "name_en", "name")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(item.Label))
            .ToList();
    }

    private async Task<List<PurchasingChoice>> LoadDistrictOptionsAsync(string provinceId)
    {
        var result = await _apiClient.GetAsync(
            $"/api/master/provinces/{Uri.EscapeDataString(provinceId)}/districts", _session.Token);
        if (!result.Success) return [];
        return ExtractArray(result.Data)
            .Select(item => new PurchasingChoice(ReadProperty(item, "id"),
                ReadProperty(item, "name_th", "name_en", "name")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(item.Label))
            .ToList();
    }

    private async Task<List<PurchasingChoice>> LoadSubdistrictOptionsAsync(string districtId)
    {
        var result = await _apiClient.GetAsync(
            $"/api/master/districts/{Uri.EscapeDataString(districtId)}/subdistricts", _session.Token);
        if (!result.Success) return [];
        return ExtractArray(result.Data)
            .Select(item => new PurchasingChoice(ReadProperty(item, "id"),
                ReadProperty(item, "name_th", "name_en", "name"),
                ReadProperty(item, "zip_code", "zipCode", "postal_code", "postalCode", "zipcode")))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(item.Label))
            .ToList();
    }

    private static PurchasingRecord MapPurchasingRecord(string section, JsonElement raw,
        IReadOnlyList<PurchasingChoice> provinces, IReadOnlyList<JsonElement> rubberTypes)
    {
        var deleted = !string.IsNullOrWhiteSpace(ReadProperty(raw, "deletedAt", "deleted_at"));
        var status = deleted ? "Deleted" : ReadProperty(raw, "status");
        var rubberTypeCodes = ReadArray(raw, "rubberTypeCodes", "rubber_type_codes");
        var rubberTypeNames = string.Join(", ", rubberTypeCodes.Select(code =>
        {
            var rubberType = rubberTypes.FirstOrDefault(item =>
                string.Equals(ReadProperty(item, "code"), code, StringComparison.OrdinalIgnoreCase));
            var name = ReadProperty(rubberType, "name");
            return string.IsNullOrWhiteSpace(name) ? code : name;
        }));
        var rubberName = ReadProperty(raw, "rubber_type_name");
        var supplierName = ReadProperty(raw, "supplier_name");
        var supplierCode = ReadProperty(raw, "supplierCode", "supplier_code");
        var orderType = ReadProperty(raw, "order_type");
        var provinceId = ReadProperty(raw, "provinceId", "province_id");
        if (provinceId.Length == 0) provinceId = ReadNestedProperty(raw, "province", "id");
        var provinceName = ReadProperty(raw, "provinceName", "province_name");
        if (provinceName.Length == 0)
            provinceName = ReadNestedProperty(raw, "province", "name_th", "name_en", "name");
        if (provinceName.Length == 0)
            provinceName = provinces.FirstOrDefault(item => item.Value == provinceId)?.Label ?? provinceId;
        return section switch
        {
            "Supplier" => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), ReadProperty(raw, "code"), ReadSupplierDisplayName(raw), "", provinceName,
                string.Join(" · ", new[] { FormatPhoneNumber(ReadProperty(raw, "phone")), ReadProperty(raw, "email") }.Where(value => value.Length > 0)),
                rubberTypeNames, "", "", "", "", "", status, "", deleted),
            "Rubber Type" => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), ReadProperty(raw, "code"), ReadProperty(raw, "name"), ReadProperty(raw, "category"), "",
                "", "", "", "", "", "", "", status, "", deleted),
            _ => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), "", rubberName, orderType, "", "", "",
                ReadProperty(raw, "order_no"), supplierName,
                FormatNumber(ReadProperty(raw, "gross_wt")), FormatNumber(ReadProperty(raw, "drc")),
                FormatNumber(ReadProperty(raw, "price")), status,
                FormatDate(ReadProperty(raw, "start_date")), deleted)
            {
                SupplierCode = supplierCode
            }
        };
    }

    private static string ReadSupplierDisplayName(JsonElement raw)
    {
        var name = ReadProperty(raw, "name", "supplierName", "supplier_name");
        var prefix = ReadProperty(raw,
            "prefix", "prefixName", "prefix_name", "namePrefix", "name_prefix",
            "supplierPrefix", "supplier_prefix", "supplierNamePrefix", "supplier_name_prefix",
            "contactPrefix", "contact_prefix", "titleName", "title_name", "nameTitle", "name_title",
            "salutation", "prefixTh", "prefix_th", "titleTh", "title_th");
        if (prefix.Length == 0)
            prefix = ReadNestedProperty(raw, "prefix", "name_th", "name_en", "name", "label", "prefix_name");
        if (prefix.Length == 0)
            prefix = ReadNestedProperty(raw, "title", "name_th", "name_en", "name", "label", "title_name");

        return prefix.Length == 0 || name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? name
            : $"{prefix} {name}".Trim();
    }

    private static string FormatPhoneNumber(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("66", StringComparison.Ordinal))
            digits = $"0{digits[2..]}";
        if (digits.Length != 10) return value;
        return $"{digits[..3]}-{digits[3..6]}-{digits[6..]}";
    }

    private static string FormatNumber(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("N2", CultureInfo.CurrentCulture) : value;

    private static string FormatDate(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date.ToString("dd-MMM-yyyy", CultureInfo.CurrentCulture) : value;

    private static string DefaultFor(JsonElement raw, string key, string fallback)
    {
        var value = ReadProperty(raw, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ReadProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return "";
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Array => string.Join(", ", property.Value.EnumerateArray().Select(item => item.ToString())),
                _ => ""
            };
        }
        return "";
    }

    private static string ReadNestedProperty(JsonElement element, string parentName, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return "";
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, parentName, StringComparison.OrdinalIgnoreCase)) continue;
            if (property.Value.ValueKind == JsonValueKind.Object) return ReadProperty(property.Value, names);
            if (property.Value.ValueKind == JsonValueKind.String) return property.Value.GetString() ?? "";
        }
        return "";
    }

    private static string ReadSupplierProvinceId(JsonElement raw)
    {
        var provinceId = ReadProperty(raw, "provinceId", "province_id");
        return provinceId.Length > 0 ? provinceId : ReadNestedProperty(raw, "province", "id");
    }

    private static string ReadSupplierDistrictId(JsonElement raw)
    {
        var districtId = ReadProperty(raw, "districtId", "district_id");
        return districtId.Length > 0 ? districtId : ReadNestedProperty(raw, "district", "id");
    }

    private static string ReadSupplierSubdistrictId(JsonElement raw)
    {
        var subdistrictId = ReadProperty(raw, "subdistrictId", "subdistrict_id");
        return subdistrictId.Length > 0 ? subdistrictId : ReadNestedProperty(raw, "subdistrict", "id");
    }

    private string ReadSupplierZipCode(JsonElement raw)
    {
        var zipCode = ReadProperty(raw, "zipCode", "zip_code", "postalCode", "postal_code", "zipcode");
        if (zipCode.Length > 0) return zipCode;
        zipCode = ReadNestedProperty(raw, "subdistrict", "zip_code", "zipCode", "postal_code", "postalCode", "zipcode");
        if (zipCode.Length > 0) return zipCode;
        return _purchasingSubdistricts
            .FirstOrDefault(item => item.Value == ReadSupplierSubdistrictId(raw))?.PostalCode ?? "";
    }

    private static IReadOnlyList<string> ReadArray(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return [];
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
            if (property.Value.ValueKind == JsonValueKind.Array)
                return property.Value.EnumerateArray().Select(item => item.ToString()).Where(value => value.Length > 0).ToArray();
            return property.Value.ValueKind == JsonValueKind.String
                ? (property.Value.GetString() ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : [];
        }
        return [];
    }

    private static List<JsonElement> ExtractArray(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array) return payload.EnumerateArray().Select(item => item.Clone()).ToList();
        if (payload.ValueKind != JsonValueKind.Object) return [];
        foreach (var property in payload.EnumerateObject())
        {
            if (!property.Name.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Equals("items", StringComparison.OrdinalIgnoreCase) &&
                !property.Name.Equals("results", StringComparison.OrdinalIgnoreCase)) continue;
            if (property.Value.ValueKind == JsonValueKind.Array)
                return property.Value.EnumerateArray().Select(item => item.Clone()).ToList();
            if (property.Value.ValueKind == JsonValueKind.Object)
                return ExtractArray(property.Value);
        }
        return [payload.Clone()];
    }
}

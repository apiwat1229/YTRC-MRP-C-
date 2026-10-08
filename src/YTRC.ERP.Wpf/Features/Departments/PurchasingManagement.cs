using System.Globalization;
using System.Text.Json;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private enum PurchasingFieldKind { Text, Multiline, Number, Date, Choice }

    private sealed record PurchasingChoice(string Value, string Label);
    private sealed record PurchasingField(
        string Key, string Label, PurchasingFieldKind Kind = PurchasingFieldKind.Text,
        string InitialValue = "", bool Required = false,
        IReadOnlyList<PurchasingChoice>? Choices = null, bool OptionalDate = false);

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
        var result = await _apiClient.GetAsync(endpoint, _session.Token);
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
                .Select(item => MapPurchasingRecord(section, item))
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
        var rubberChoices = _purchasingRubberTypes
            .Where(item => string.IsNullOrWhiteSpace(ReadProperty(item, "deletedAt")))
            .Select(item => new PurchasingChoice(ReadProperty(item, "code"),
                string.IsNullOrWhiteSpace(ReadProperty(item, "code"))
                    ? ReadProperty(item, "name")
                    : $"{ReadProperty(item, "code")} · {ReadProperty(item, "name")}"))
            .ToArray();
        return
        [
            new("code", "Supplier code", InitialValue: ReadProperty(raw, "code"), Required: true),
            new("name", "Supplier name", InitialValue: ReadProperty(raw, "name"), Required: true),
            new("firstName", "Contact first name", InitialValue: ReadProperty(raw, "firstName")),
            new("lastName", "Contact last name", InitialValue: ReadProperty(raw, "lastName")),
            new("taxId", "Tax ID", InitialValue: ReadProperty(raw, "taxId")),
            new("phone", "Phone", InitialValue: ReadProperty(raw, "phone")),
            new("email", "Email", InitialValue: ReadProperty(raw, "email")),
            new("address", "Address", PurchasingFieldKind.Multiline, ReadProperty(raw, "address")),
            new("zipCode", "Postal code", InitialValue: ReadProperty(raw, "zipCode")),
            new("status", "Status", PurchasingFieldKind.Choice,
                DefaultFor(raw, "status", "ACTIVE"), Choices:
                [new("ACTIVE", "Active"), new("INACTIVE", "Inactive"), new("SUSPENDED", "Suspended")]),
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
            new("supplier_id", "Supplier", PurchasingFieldKind.Choice, ReadProperty(raw, "supplier_id"), Required: true, Choices: suppliers),
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
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            Width = 680,
            Height = Math.Min(820, Math.Max(560, 170 + fields.Count * 66)),
            MinWidth = 560,
            MinHeight = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            Background = (Brush)FindResource("CanvasBrush")
        };
        var layout = new DockPanel { Margin = new Thickness(22) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        var save = new Button { Content = "Save", MinWidth = 100, Margin = new Thickness(8, 0, 0, 0), Style = (Style)FindResource("PrimaryButton") };
        var cancel = new Button { Content = "Cancel", MinWidth = 100, IsCancel = true };
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        layout.Children.Add(footer);

        var form = new StackPanel();
        var controls = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
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
                    AcceptsReturn = field.Kind == PurchasingFieldKind.Multiline,
                    TextWrapping = field.Kind == PurchasingFieldKind.Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                    VerticalContentAlignment = field.Kind == PurchasingFieldKind.Multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
                    VerticalScrollBarVisibility = field.Kind == PurchasingFieldKind.Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
                }
            };
            if (field.Kind == PurchasingFieldKind.Number && input is TextBox numeric)
                numeric.PreviewTextInput += (_, args) => args.Handled = args.Text.Any(character => !char.IsDigit(character) && character is not ('.' or ',' or '-'));
            controls[field.Key] = input;
            form.Children.Add(Field(field.Required ? $"{field.Label} *" : field.Label, input));
            form.Children.Add(new Border { Height = 9, Opacity = 0 });
        }
        layout.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        dialog.Content = layout;

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

        static FrameworkElement MakeChoice(PurchasingField field)
        {
            var combo = new ComboBox
            {
                ItemsSource = field.Choices ?? [],
                DisplayMemberPath = nameof(PurchasingChoice.Label),
                SelectedValuePath = nameof(PurchasingChoice.Value),
                SelectedValue = field.InitialValue,
                MinWidth = 250,
                Height = 32
            };
            return combo;
        }

        static FrameworkElement MakeDate(PurchasingField field)
        {
            DateTime? date = DateTime.TryParse(field.InitialValue, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out var parsed) ? parsed : null;
            return new DatePicker { SelectedDate = date, MinWidth = 250, Height = 32, IsTodayHighlighted = true };
        }
    }

    private static object? ReadEditorValue(PurchasingField field, FrameworkElement input)
    {
        if (input is ComboBox combo)
            return combo.SelectedValue?.ToString();
        if (input is DatePicker datePicker)
            return datePicker.SelectedDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
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

    private static PurchasingRecord MapPurchasingRecord(string section, JsonElement raw)
    {
        var deleted = !string.IsNullOrWhiteSpace(ReadProperty(raw, "deletedAt", "deleted_at"));
        var status = deleted ? "Deleted" : ReadProperty(raw, "status");
        var rubberTypeCodes = string.Join(", ", ReadArray(raw, "rubberTypeCodes", "rubber_type_codes"));
        var rubberName = ReadProperty(raw, "rubber_type_name");
        var supplierName = ReadProperty(raw, "supplier_name");
        var orderType = ReadProperty(raw, "order_type");
        return section switch
        {
            "Supplier" => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), ReadProperty(raw, "code"), ReadProperty(raw, "name"), "",
                string.Join(" · ", new[] { ReadProperty(raw, "phone"), ReadProperty(raw, "email") }.Where(value => value.Length > 0)),
                rubberTypeCodes, "", "", "", "", "", status, "", deleted),
            "Rubber Type" => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), ReadProperty(raw, "code"), ReadProperty(raw, "name"), ReadProperty(raw, "category"),
                "", "", "", "", "", "", "", status, "", deleted),
            _ => new PurchasingRecord(raw,
                ReadProperty(raw, "id"), "", rubberName, orderType, "", "",
                ReadProperty(raw, "order_no"), supplierName,
                FormatNumber(ReadProperty(raw, "gross_wt")), FormatNumber(ReadProperty(raw, "drc")),
                FormatNumber(ReadProperty(raw, "price")), status,
                FormatDate(ReadProperty(raw, "start_date")), deleted)
        };
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private async Task OpenLotSamplesAsync(ReceivingLotRow lot)
    {
        if (_session.IsPreview)
        {
            MessageBox.Show(this, "Sign in to view and save lab samples.", "Sign in required",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = await _apiClient.GetAsync(
            $"/api/bookings/{Uri.EscapeDataString(lot.BookingId)}/samples", _session.Token);
        if (!result.Success || result.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            MessageBox.Show(this, result.Error.Length > 0 ? result.Error : "Could not load lab samples.",
                "Lab samples", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var rows = new ObservableCollection<ReceivingSampleEditorRow>();
        foreach (var item in ExtractArray(result.Data))
        {
            var isTrailer = ReadProperty(item, "isTrailer", "is_trailer")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            if (isTrailer != lot.IsTrailer) continue;
            if (!int.TryParse(ReadProperty(item, "sampleNo", "sample_no"), out var sampleNo)) continue;
            rows.Add(new ReceivingSampleEditorRow(
                ReadProperty(item, "id"), sampleNo, lot.IsTrailer, ParseReceivingNumber(lot.Moisture),
                ReadSampleNumber(item, "beforePress", "before_press"),
                ReadSampleNumber(item, "basketWeight", "basket_weight"),
                ReadSampleNumber(item, "afterPress", "after_press"),
                ReadSampleNumber(item, "beforeBaking1", "before_baking_1"),
                ReadSampleNumber(item, "beforeBaking2", "before_baking_2"),
                ReadSampleNumber(item, "beforeBaking3", "before_baking_3"),
                ReadProperty(item, "storage")));
        }
        SortReceivingSamples(rows);

        var dialog = new Window
        {
            Title = $"Lab samples · {lot.BookingCode} · {lot.Kind}",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 1120,
            Height = 610,
            MinWidth = 900,
            MinHeight = 420,
            Background = (Brush)FindResource("CanvasBrush")
        };

        var layout = new DockPanel { Margin = new Thickness(18) };
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        heading.Children.Add(Label($"{lot.BookingCode} · {lot.SupplierName}", 18,
            (Brush)FindResource("TextBrush"), FontWeights.Bold));
        heading.Children.Add(Label($"{lot.Kind} · {lot.RubberType} · Lot {lot.LotNo} · moisture {lot.Moisture}%",
            12, (Brush)FindResource("MutedBrush")));
        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        var grid = new DataGrid
        {
            ItemsSource = rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = false,
            IsReadOnly = false,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            RowHeight = 38,
            MinRowHeight = 34,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 2)
        };
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Sample",
            Binding = new Binding(nameof(ReceivingSampleEditorRow.SampleNo)),
            IsReadOnly = true,
            Width = 75
        });
        AddReceivingSampleNumberColumn(grid, "Before press kg", nameof(ReceivingSampleEditorRow.BeforePress), 115);
        AddReceivingSampleNumberColumn(grid, "Basket kg", nameof(ReceivingSampleEditorRow.BasketWeight), 100);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Cuplump kg",
            Binding = new Binding(nameof(ReceivingSampleEditorRow.CuplumpWeight)) { StringFormat = "N2" },
            IsReadOnly = true,
            Width = 105
        });
        AddReceivingSampleNumberColumn(grid, "After press kg", nameof(ReceivingSampleEditorRow.AfterPress), 110);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "%CP",
            Binding = new Binding(nameof(ReceivingSampleEditorRow.PercentCp)) { StringFormat = "N2" },
            IsReadOnly = true,
            Width = 85
        });
        AddReceivingSampleNumberColumn(grid, "Before bake 1", nameof(ReceivingSampleEditorRow.BeforeBaking1), 105);
        AddReceivingSampleNumberColumn(grid, "Before bake 2", nameof(ReceivingSampleEditorRow.BeforeBaking2), 105);
        AddReceivingSampleNumberColumn(grid, "Before bake 3", nameof(ReceivingSampleEditorRow.BeforeBaking3), 105);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Storage / pool",
            Binding = new Binding(nameof(ReceivingSampleEditorRow.Storage)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 120
        });
        layout.Children.Add(grid);

        var add = CreateReceivingDialogButton("Add sample");
        add.Margin = new Thickness(0, 0, 8, 0);
        add.Click += (_, _) =>
        {
            var next = rows.Count == 0 ? 1 : rows.Max(row => row.SampleNo) + 1;
            var sample = new ReceivingSampleEditorRow("", next, lot.IsTrailer,
                ParseReceivingNumber(lot.Moisture), null, null, null, null, null, null, "");
            rows.Add(sample);
            SortReceivingSamples(rows);
            grid.SelectedItem = sample;
            grid.ScrollIntoView(sample);
        };
        footer.Children.Add(add);

        var remove = CreateReceivingDialogButton("Delete sample");
        remove.Margin = new Thickness(0, 0, 8, 0);
        remove.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not ReceivingSampleEditorRow selected) return;
            if (selected.Id.Length > 0)
            {
                var confirm = MessageBox.Show(dialog,
                    $"Delete sample {selected.SampleNo} from {lot.BookingCode}?",
                    "Delete lab sample", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
                var deleted = await _apiClient.DeleteAsync(
                    $"/api/bookings/{Uri.EscapeDataString(lot.BookingId)}/samples/{Uri.EscapeDataString(selected.Id)}",
                    _session.Token);
                if (!deleted.Success)
                {
                    MessageBox.Show(dialog, deleted.Error, "Could not delete sample",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            rows.Remove(selected);
        };
        footer.Children.Add(remove);

        var cancel = CreateReceivingDialogButton("Close");
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Click += (_, _) => dialog.Close();
        footer.Children.Add(cancel);

        var save = CreateReceivingDialogButton("Save samples", primary: true);
        save.Click += async (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            if (rows.Any(row => row.SampleNo <= 0) || rows.Select(row => row.SampleNo).Distinct().Count() != rows.Count)
            {
                MessageBox.Show(dialog, "Sample numbers must be positive and unique within this lot.",
                    "Invalid sample number", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (rows.Any(row => row.NumericValues.Any(value => value is < 0)))
            {
                MessageBox.Show(dialog, "Lab weights and measurements cannot be negative.",
                    "Invalid measurement", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            save.IsEnabled = false;
            foreach (var row in rows.Where(row => row.HasSampleData))
            {
                var payload = new Dictionary<string, object?>
                {
                    ["id"] = row.Id.Length == 0 ? null : row.Id,
                    ["sampleNo"] = row.SampleNo,
                    ["isTrailer"] = row.IsTrailer,
                    ["beforePress"] = row.BeforePress,
                    ["basketWeight"] = row.BasketWeight,
                    ["cuplumpWeight"] = row.CuplumpWeight,
                    ["afterPress"] = row.AfterPress,
                    ["percentCp"] = row.PercentCp,
                    ["beforeBaking1"] = row.BeforeBaking1,
                    ["beforeBaking2"] = row.BeforeBaking2,
                    ["beforeBaking3"] = row.BeforeBaking3,
                    ["storage"] = row.Storage.Trim(),
                    ["recordedBy"] = _session.User.FullName
                };
                var saved = await _apiClient.PostJsonAsync(
                    $"/api/bookings/{Uri.EscapeDataString(lot.BookingId)}/samples", payload, _session.Token);
                if (!saved.Success)
                {
                    save.IsEnabled = true;
                    MessageBox.Show(dialog, saved.Error, $"Sample {row.SampleNo} was not saved",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                row.Id = ReadProperty(saved.Data, "id");
            }
            dialog.DialogResult = true;
            dialog.Close();
        };
        footer.Children.Add(save);

        dialog.Content = layout;
        dialog.ShowDialog();
    }

    private static void AddReceivingSampleNumberColumn(DataGrid grid, string header, string property, double width)
    {
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property)
            {
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                StringFormat = "0.##"
            },
            Width = width
        });
    }

    private Button CreateReceivingDialogButton(string content, bool primary = false)
    {
        var button = PurchasingButton(content, primary);
        button.MinWidth = 112;
        return button;
    }

    private static void SortReceivingSamples(ObservableCollection<ReceivingSampleEditorRow> rows)
    {
        var ordered = rows.OrderBy(row => row.SampleNo).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var currentIndex = rows.IndexOf(ordered[index]);
            if (currentIndex != index) rows.Move(currentIndex, index);
        }
    }

    private static double? ReadSampleNumber(JsonElement item, params string[] names) =>
        double.TryParse(ReadProperty(item, names), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private sealed class ReceivingSampleEditorRow : INotifyPropertyChanged
    {
        private double? _beforePress;
        private double? _basketWeight;
        private double? _afterPress;
        private double? _beforeBaking1;
        private double? _beforeBaking2;
        private double? _beforeBaking3;
        private string _storage;
        private double? _cuplumpWeight;
        private double? _percentCp;

        public ReceivingSampleEditorRow(string id, int sampleNo, bool isTrailer, double moisture,
            double? beforePress, double? basketWeight, double? afterPress,
            double? beforeBaking1, double? beforeBaking2, double? beforeBaking3, string storage)
        {
            Id = id;
            SampleNo = sampleNo;
            IsTrailer = isTrailer;
            Moisture = moisture;
            _storage = storage;
            _beforePress = beforePress;
            _basketWeight = basketWeight;
            _afterPress = afterPress;
            _beforeBaking1 = beforeBaking1;
            _beforeBaking2 = beforeBaking2;
            _beforeBaking3 = beforeBaking3;
            Recalculate();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public string Id { get; set; }
        public int SampleNo { get; }
        public bool IsTrailer { get; }
        public double Moisture { get; }
        public double? BeforePress { get => _beforePress; set { if (Set(ref _beforePress, value)) Recalculate(); } }
        public double? BasketWeight { get => _basketWeight; set { if (Set(ref _basketWeight, value)) Recalculate(); } }
        public double? AfterPress { get => _afterPress; set { if (Set(ref _afterPress, value)) Recalculate(); } }
        public double? CuplumpWeight { get => _cuplumpWeight; private set => Set(ref _cuplumpWeight, value); }
        public double? PercentCp { get => _percentCp; private set => Set(ref _percentCp, value); }
        public double? BeforeBaking1 { get => _beforeBaking1; set => Set(ref _beforeBaking1, value); }
        public double? BeforeBaking2 { get => _beforeBaking2; set => Set(ref _beforeBaking2, value); }
        public double? BeforeBaking3 { get => _beforeBaking3; set => Set(ref _beforeBaking3, value); }
        public string Storage { get => _storage; set => Set(ref _storage, value ?? ""); }
        public IEnumerable<double?> NumericValues =>
            [BeforePress, BasketWeight, AfterPress, BeforeBaking1, BeforeBaking2, BeforeBaking3];
        public bool HasSampleData => NumericValues.Any(value => value.HasValue) || !string.IsNullOrWhiteSpace(Storage);

        private void Recalculate()
        {
            CuplumpWeight = BeforePress.HasValue && BasketWeight.HasValue
                ? Math.Max(0, BeforePress.Value - BasketWeight.Value)
                : null;
            PercentCp = AfterPress.HasValue && CuplumpWeight is > 0
                ? AfterPress.Value * (100 - Moisture) / CuplumpWeight.Value
                : null;
        }

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}

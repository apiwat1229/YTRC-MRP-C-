using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private async Task PerformNextTruckScaleActionAsync(TruckScaleBooking booking)
    {
        if (!booking.IsArrived)
        {
            await CheckInTruckScaleBookingAsync(booking);
            return;
        }
        if (!booking.IsUss && !booking.HasDrainStopped)
        {
            if (booking.IsDraining) await StopTruckScaleDrainAsync(booking);
            else await StartTruckScaleDrainAsync(booking);
            return;
        }
        if (!booking.HasWeightIn)
        {
            await SaveTruckScaleWeightAsync(booking, inbound: true);
            return;
        }
        if (!booking.HasWeightOut)
        {
            await SaveTruckScaleWeightAsync(booking, inbound: false);
            return;
        }
        MessageBox.Show(this, "This queue has completed its normal weighing steps.", "TruckScale", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CheckInTruckScaleBookingAsync(TruckScaleBooking booking)
    {
        var truckType = TruckScaleChoiceBox(TruckScaleTypes.Select(value => new TruckScaleChoice(value, TranslateTruckScaleType(value))),
            NormalizeTruckType(booking.TruckType));
        var truckRegistration = SplitTruckScaleRegistration(booking.TruckRegister);
        var truckRegister = TruckScaleTextBox(truckRegistration.Plate);
        var truckProvince = TruckScaleChoiceBox(TruckScaleProvinceChoices(truckRegistration.Province), truckRegistration.Province);
        var rubberType = TruckScaleChoiceBox(TruckScaleRubberChoices(booking.RubberType), booking.RubberType);
        var rubberSource = TruckScaleChoiceBox(TruckScaleProvinceChoices(booking.RubberSource), booking.RubberSource);
        var rubberCondition = TruckScaleChoiceBox(
            [new("DRY", "แห้ง"), new("DAMP", "หมาด"), new("WET", "เปียก")],
            TruckScaleNormalizeRubberCondition(booking.RubberCondition));
        var note = TruckScaleTextBox(booking.Note, multiline: true);
        note.Height = 68;

        var trailerCheck = new CheckBox
        {
            Content = "This truck carries a separate trailer rubber type",
            FontSize = 13,
            Margin = new Thickness(0, 8, 0, 7),
            IsChecked = booking.TrailerRegister.Length > 0 || booking.TrailerRubberType.Length > 0 ||
                        NormalizeTruckType(booking.TruckType).Contains("Trailer", StringComparison.OrdinalIgnoreCase)
        };
        var trailerRegistration = SplitTruckScaleRegistration(booking.TrailerRegister);
        var trailerRegister = TruckScaleTextBox(trailerRegistration.Plate);
        var trailerProvince = TruckScaleChoiceBox(TruckScaleProvinceChoices(trailerRegistration.Province), trailerRegistration.Province);
        var trailerRubberType = TruckScaleChoiceBox(TruckScaleRubberChoices(booking.TrailerRubberType), booking.TrailerRubberType);
        var trailerRubberSource = TruckScaleChoiceBox(TruckScaleProvinceChoices(booking.TrailerRubberSource), booking.TrailerRubberSource);
        var trailerFields = new StackPanel();
        trailerFields.Children.Add(TruckScaleFormRow(
            TruckScaleField("Trailer registration", TruckScaleFormRow(
                TruckScaleField("Plate", trailerRegister), TruckScaleField("Province", trailerProvince))),
            TruckScaleField("Trailer rubber type", trailerRubberType)));
        trailerFields.Children.Add(TruckScaleFormRow(
            TruckScaleField("Trailer rubber source", trailerRubberSource),
            Label("Truck and trailer weights are recorded separately at the scale.", 12, (Brush)FindResource("MutedBrush"))));
        trailerFields.Visibility = trailerCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        trailerCheck.Checked += (_, _) => trailerFields.Visibility = Visibility.Visible;
        trailerCheck.Unchecked += (_, _) => trailerFields.Visibility = Visibility.Collapsed;
        var truckHasTrailer = CanTruckScaleTowTrailer(TruckScaleValue(truckType));
        trailerCheck.IsEnabled = truckHasTrailer;
        if (!truckHasTrailer && booking.TrailerRegister.Length == 0 && booking.TrailerRubberType.Length == 0)
        {
            trailerCheck.IsChecked = false;
            trailerFields.Visibility = Visibility.Collapsed;
        }
        truckType.SelectionChanged += (_, _) =>
        {
            trailerCheck.IsEnabled = CanTruckScaleTowTrailer(TruckScaleValue(truckType));
            if (!trailerCheck.IsEnabled)
            {
                trailerCheck.IsChecked = false;
                trailerFields.Visibility = Visibility.Collapsed;
            }
        };

        var body = new StackPanel();
        body.Children.Add(Label($"Queue {booking.Queue} · {booking.SupplierCode} · {booking.SupplierName}", 14,
            (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        body.Children.Add(TruckScaleFormRow(
            TruckScaleField("Truck type", truckType),
            TruckScaleField("Truck registration", TruckScaleFormRow(
                TruckScaleField("Plate", truckRegister), TruckScaleField("Province", truckProvince)))));
        body.Children.Add(TruckScaleFormRow(
            TruckScaleField("Rubber type", rubberType),
            TruckScaleField("Rubber source / province", rubberSource)));
        body.Children.Add(TruckScaleFormRow(
            TruckScaleField("Rubber condition", rubberCondition),
            TruckScaleField("Note", note)));
        body.Children.Add(trailerCheck);
        body.Children.Add(trailerFields);

        var dialog = CreateTruckScaleDialog("Check-In · " + booking.Queue, body, () =>
        {
            if (TruckScaleValue(truckType).Length == 0) return "Select a truck type.";
            if (truckRegister.Text.Trim().Length == 0) return "Enter the truck registration.";
            if (TruckScaleValue(truckProvince).Length == 0) return "Select or enter the truck province.";
            if (TruckScaleValue(rubberType).Length == 0) return "Select a rubber type.";
            if (TruckScaleValue(rubberSource).Length == 0) return "Select or enter the rubber source.";
            if (!booking.IsUss && !TruckScaleValue(rubberType).Contains("USS", StringComparison.OrdinalIgnoreCase) && TruckScaleValue(rubberCondition).Length == 0)
                return "Select the rubber condition.";
            if (trailerCheck.IsChecked == true && trailerRegister.Text.Trim().Length == 0) return "Enter the trailer registration.";
            if (trailerCheck.IsChecked == true && TruckScaleValue(trailerProvince).Length == 0) return "Select or enter the trailer province.";
            if (trailerCheck.IsChecked == true && TruckScaleValue(trailerRubberType).Length == 0) return "Select the trailer rubber type.";
            if (trailerCheck.IsChecked == true && TruckScaleValue(trailerRubberSource).Length == 0) return "Select the trailer rubber source.";
            return null;
        });
        if (dialog.ShowDialog() != true) return;

        var isUss = TruckScaleValue(rubberType).Contains("USS", StringComparison.OrdinalIgnoreCase) || booking.IsUss;
        var payload = new Dictionary<string, object?>
        {
            ["truckType"] = TruckScaleValue(truckType),
            ["truckRegister"] = ComposeTruckScaleRegistration(truckRegister.Text, TruckScaleValue(truckProvince)),
            ["rubberType"] = TruckScaleValue(rubberType),
            ["rubberSource"] = TruckScaleValue(rubberSource),
            ["rubberCondition"] = isUss ? null : TruckScaleValue(rubberCondition),
            ["note"] = NullIfBlank(note.Text)
        };
        if (trailerCheck.IsChecked == true)
        {
            payload["trailerRegister"] = ComposeTruckScaleRegistration(trailerRegister.Text, TruckScaleValue(trailerProvince));
            payload["trailerRubberType"] = TruckScaleValue(trailerRubberType);
            payload["trailerRubberSource"] = TruckScaleValue(trailerRubberSource);
        }
        await PatchTruckScaleAsync(booking, "check-in", payload, "Check-in recorded.");
    }

    private async Task StartTruckScaleDrainAsync(TruckScaleBooking booking)
    {
        var truckType = TruckScaleChoiceBox(TruckScaleTypes.Select(value => new TruckScaleChoice(value, TranslateTruckScaleType(value))),
            NormalizeTruckType(booking.TruckType));
        var truckRegister = TruckScaleTextBox(booking.TruckRegister);
        var startTime = TruckScaleTextBox(DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture));
        var reason = TruckScaleTextBox(multiline: true);
        reason.Height = 64;
        var body = new StackPanel();
        body.Children.Add(Label($"Queue {booking.Queue} · {booking.SupplierName}", 14, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        body.Children.Add(TruckScaleFormRow(TruckScaleField("Truck type", truckType), TruckScaleField("Truck registration", truckRegister)));
        body.Children.Add(TruckScaleFormRow(TruckScaleField("Start time (HH:mm)", startTime), TruckScaleField("Reason, when needed", reason)));
        var dialog = CreateTruckScaleDialog("Start draining · " + booking.Queue, body, () =>
        {
            if (TruckScaleValue(truckType).Length == 0) return "Select a truck type.";
            if (truckRegister.Text.Trim().Length == 0) return "Enter the truck registration.";
            if (!TimeSpan.TryParse(startTime.Text.Trim(), CultureInfo.InvariantCulture, out _)) return "Enter a valid time in HH:mm format.";
            if (booking.CheckInAt is { } checkIn && _truckScaleDate.Date.Add(TimeSpan.Parse(startTime.Text, CultureInfo.InvariantCulture)) < checkIn && reason.Text.Trim().Length == 0)
                return "A reason is required when the draining time is earlier than check-in.";
            return null;
        });
        if (dialog.ShowDialog() != true) return;
        var selectedTime = _truckScaleDate.Date.Add(TimeSpan.Parse(startTime.Text.Trim(), CultureInfo.InvariantCulture));
        var payload = new Dictionary<string, object?>
        {
            ["startDrainAt"] = DateTime.SpecifyKind(selectedTime, DateTimeKind.Local).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["truckType"] = TruckScaleValue(truckType),
            ["truckRegister"] = truckRegister.Text.Trim(),
            ["reason"] = NullIfBlank(reason.Text)
        };
        await PatchTruckScaleAsync(booking, "start-drain", payload, "Draining timer started.");
    }

    private async Task StopTruckScaleDrainAsync(TruckScaleBooking booking)
    {
        var reason = TruckScaleTextBox(multiline: true);
        reason.Height = 84;
        var elapsed = booking.StartDrainAt is { } started ? Math.Max(0, (DateTime.Now - started).TotalMinutes) : 0;
        var body = new StackPanel();
        body.Children.Add(Label($"Queue {booking.Queue} · elapsed {TimeSpan.FromMinutes(elapsed):hh\\:mm}", 14,
            (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        body.Children.Add(Label(elapsed < 40 ? "A reason is required when draining has lasted less than 40 minutes." : "Add a note if the operator needs to record one.",
            12, (Brush)FindResource("MutedBrush")));
        body.Children.Add(TruckScaleField("Stop reason / note", reason));
        var dialog = CreateTruckScaleDialog("Stop draining · " + booking.Queue, body,
            () => elapsed < 40 && reason.Text.Trim().Length == 0 ? "Enter a reason because the draining duration is under 40 minutes." : null);
        if (dialog.ShowDialog() != true) return;
        await PatchTruckScaleAsync(booking, "stop-drain", new { note = NullIfBlank(reason.Text) }, "Draining timer stopped.");
    }

    private async Task SaveTruckScaleWeightAsync(TruckScaleBooking booking, bool inbound)
    {
        if (inbound && !booking.IsUss && !booking.HasDrainStopped)
        {
            MessageBox.Show(this, "Complete the draining step before recording Weight In.", "TruckScale", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var hasTrailer = booking.TrailerRegister.Length > 0 || booking.TrailerRubberType.Length > 0 ||
                         NormalizeTruckType(booking.TruckType).Contains("Trailer", StringComparison.OrdinalIgnoreCase);
        var primary = TruckScaleTextBox(inbound ? ReadNumber(booking.Raw, "weightIn", "weight_in") : ReadNumber(booking.Raw, "weightOut", "weight_out"));
        var trailer = TruckScaleTextBox(inbound ? ReadNumber(booking.Raw, "trailerWeightIn", "trailer_weight_in") : ReadNumber(booking.Raw, "trailerWeightOut", "trailer_weight_out"));
        var body = new StackPanel();
        body.Children.Add(Label($"Queue {booking.Queue} · {booking.SupplierName} · {booking.TruckRegister}", 14,
            (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        body.Children.Add(TruckScaleFormRow(
            TruckScaleWeightField("Truck weight (kg)", primary, 0),
            hasTrailer ? TruckScaleWeightField("Trailer weight (kg)", trailer, 1) : TruckScaleField("Rubber type", TruckScaleReadOnly(lookup: LookupRubberType(booking.RubberType)))));
        if (hasTrailer)
            body.Children.Add(Label("Enter or capture the displayed scale reading for each vehicle section. Both values are saved separately.", 12, (Brush)FindResource("MutedBrush")));
        var dialog = CreateTruckScaleDialog((inbound ? "Weight In" : "Weight Out") + " · " + booking.Queue, body, () =>
        {
            if (!TryReadScaleValue(primary.Text, out _)) return "Enter a valid non-negative truck weight.";
            if (hasTrailer && !TryReadScaleValue(trailer.Text, out _)) return "Enter a valid non-negative trailer weight.";
            return null;
        });
        if (dialog.ShowDialog() != true) return;

        TryReadScaleValue(primary.Text, out var truckWeight);
        var payload = new Dictionary<string, object?>
        {
            [inbound ? "weightIn" : "weightOut"] = truckWeight
        };
        if (hasTrailer)
        {
            TryReadScaleValue(trailer.Text, out var trailerWeight);
            payload[inbound ? "trailerWeightIn" : "trailerWeightOut"] = trailerWeight;
        }
        if (inbound)
        {
            payload["truckRegister"] = booking.TruckRegister;
            payload["rubberSource"] = NullIfBlank(booking.RubberSource);
            payload["rubberType"] = NullIfBlank(booking.RubberType);
            if (hasTrailer)
            {
                payload["trailerRegister"] = booking.TrailerRegister;
                payload["trailerRubberSource"] = NullIfBlank(booking.TrailerRubberSource);
                payload["trailerRubberType"] = NullIfBlank(booking.TrailerRubberType);
            }
        }
        await PatchTruckScaleAsync(booking, inbound ? "weight-in" : "weight-out", payload,
            inbound ? "Weight In recorded." : "Weight Out recorded.");
    }

    private async Task SaveTruckScaleReturnWeightAsync(TruckScaleBooking booking, bool inbound)
    {
        var hasTrailer = booking.TrailerRegister.Length > 0 || booking.TrailerRubberType.Length > 0 ||
                         NormalizeTruckType(booking.TruckType).Contains("Trailer", StringComparison.OrdinalIgnoreCase);
        var mainKey = inbound ? "returnWeightIn" : "returnWeightOut";
        var trailerKey = inbound ? "returnTrailerWeightIn" : "returnTrailerWeightOut";
        var main = TruckScaleTextBox(ReadNumber(booking.Raw, mainKey, ToSnakeCase(mainKey)));
        var trailer = TruckScaleTextBox(ReadNumber(booking.Raw, trailerKey, ToSnakeCase(trailerKey)));
        var body = new StackPanel();
        body.Children.Add(Label($"Queue {booking.Queue} · {booking.SupplierName}", 14, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        body.Children.Add(TruckScaleFormRow(
            TruckScaleWeightField("Return truck weight (kg)", main, 0),
            hasTrailer ? TruckScaleWeightField("Return trailer weight (kg)", trailer, 1) : Label("", 12, Brushes.Transparent)));
        var dialog = CreateTruckScaleDialog((inbound ? "Return Weight In" : "Return Weight Out") + " · " + booking.Queue, body, () =>
        {
            if (!TryReadScaleValue(main.Text, out _)) return "Enter a valid non-negative return weight.";
            if (hasTrailer && !TryReadScaleValue(trailer.Text, out _)) return "Enter a valid non-negative trailer return weight.";
            return null;
        });
        if (dialog.ShowDialog() != true) return;
        TryReadScaleValue(main.Text, out var mainWeight);
        var payload = new Dictionary<string, object?> { [mainKey] = mainWeight };
        if (hasTrailer)
        {
            TryReadScaleValue(trailer.Text, out var trailerWeight);
            payload[trailerKey] = trailerWeight;
        }
        await PatchTruckScaleAsync(booking, inbound ? "return-weight-in" : "return-weight-out", payload,
            inbound ? "Return Weight In recorded." : "Return Weight Out recorded.");
    }

    private async Task PatchTruckScaleAsync(TruckScaleBooking booking, string operation, object payload, string successMessage)
    {
        var result = await _apiClient.PatchJsonAsync($"/api/bookings/{Uri.EscapeDataString(booking.Id)}/{operation}", payload, _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, result.Error.Length > 0 ? result.Error : $"Could not save {operation}.", "TruckScale · Save failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _truckScaleMessage = successMessage;
        await LoadTruckScaleDataAsync();
        _truckScaleSelectedId = booking.Id;
        MessageBox.Show(this, successMessage, "TruckScale", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private Window CreateTruckScaleDialog(string title, UIElement body, Func<string?> validate)
    {
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 740,
            MinWidth = 620,
            SizeToContent = SizeToContent.Height,
            MaxHeight = Math.Max(520, ActualHeight * 0.9),
            ResizeMode = ResizeMode.CanResize,
            Background = CreateBrush("#F4F7FA"),
            FontFamily = FontFamily,
            ShowInTaskbar = false
        };
        var root = new Grid { Margin = new Thickness(20, 18, 20, 16) };
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = Math.Max(300, ActualHeight * 0.68) };
        root.Children.Add(scroll);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Button { Content = "Cancel", Width = 100, Height = 36, Margin = new Thickness(0, 0, 8, 0), Style = (Style)FindResource(typeof(Button)) };
        cancel.Click += (_, _) => dialog.DialogResult = false;
        var save = new Button { Content = "Save", Width = 112, Height = 36, Style = (Style)FindResource("PrimaryButton") };
        save.Click += (_, _) =>
        {
            var error = validate();
            if (!string.IsNullOrWhiteSpace(error))
            {
                MessageBox.Show(dialog, error, "Check the form", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            dialog.DialogResult = true;
        };
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        Grid.SetRow(footer, 1);
        root.Children.Add(footer);
        dialog.Content = root;
        return dialog;
    }

    private UIElement TruckScaleFormRow(UIElement left, UIElement right)
    {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (left is FrameworkElement leftElement) leftElement.Margin = new Thickness(0, 0, 8, 0);
        if (right is FrameworkElement rightElement) rightElement.Margin = new Thickness(8, 0, 0, 0);
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private UIElement TruckScaleField(string title, UIElement input)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(title, 12, CreateBrush("#52647A"), FontWeights.SemiBold));
        stack.Children.Add(input);
        return stack;
    }

    private UIElement TruckScaleWeightField(string title, TextBox input, int scaleIndex)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(title, 12, CreateBrush("#52647A"), FontWeights.SemiBold));

        var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        input.Margin = new Thickness(0);
        row.Children.Add(input);

        var captureButton = new Button
        {
            Content = $"ดึงจากตาชั่ง {scaleIndex + 1}",
            Padding = new Thickness(10, 0, 10, 0),
            Height = 35,
            Margin = new Thickness(6, 0, 0, 0),
            Style = (Style)FindResource(typeof(Button)),
            ToolTip = $"ดึงค่าน้ำหนักจากเครื่องชั่งตัวที่ {scaleIndex + 1}"
        };
        Grid.SetColumn(captureButton, 1);
        row.Children.Add(captureButton);
        stack.Children.Add(row);

        var statusText = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(2, 2, 0, 4),
            Foreground = (Brush)FindResource("MutedBrush")
        };
        stack.Children.Add(statusText);

        void UpdateStatus()
        {
            var isConnected = _scaleService.IsConnected(scaleIndex);
            var reading = _scaleService.LatestReading(scaleIndex);
            if (!isConnected)
            {
                var cfg = AppSettings.Current.Scales.Length > scaleIndex ? AppSettings.Current.Scales[scaleIndex] : null;
                var port = cfg?.PortName ?? $"COM{scaleIndex + 1}";
                statusText.Text = $"เครื่องชั่ง {scaleIndex + 1} ({port}): ออฟไลน์";
                statusText.Foreground = CreateBrush("#94A3B8");
            }
            else if (reading is not null)
            {
                var stability = reading.IsStable ? "เสถียร (Stable)" : "ไม่นิ่ง (Unstable)";
                statusText.Text = $"เครื่องชั่ง {scaleIndex + 1}: {reading.Formatted} {reading.Unit} · {stability}";
                statusText.Foreground = reading.IsStable ? CreateBrush("#0F766E") : CreateBrush("#D97706");
            }
            else
            {
                statusText.Text = $"เครื่องชั่ง {scaleIndex + 1}: เชื่อมต่อแล้ว (รอสัญญาณข้อมูล)";
                statusText.Foreground = CreateBrush("#0F766E");
            }
        }

        UpdateStatus();

        captureButton.Click += async (_, _) =>
        {
            if (!_scaleService.IsConnected(scaleIndex))
            {
                if (AppSettings.Current.Scales.Length > scaleIndex)
                {
                    try
                    {
                        await _scaleService.ConnectAsync(scaleIndex, AppSettings.Current.Scales[scaleIndex]);
                    }
                    catch (Exception ex)
                    {
                        var parentWin = Window.GetWindow(stack);
                        MessageBox.Show(parentWin ?? this,
                            $"ไม่สามารถเชื่อมต่อเครื่องชั่ง {scaleIndex + 1}: {ex.Message}",
                            "เครื่องชั่ง", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            var reading = _scaleService.LatestReading(scaleIndex);
            if (reading is not null)
            {
                input.Text = reading.Weight.ToString(CultureInfo.InvariantCulture);
                UpdateStatus();
            }
            else
            {
                var parentWin = Window.GetWindow(stack);
                MessageBox.Show(parentWin ?? this,
                    $"ยังไม่มีค่าน้ำหนักส่งมาจากเครื่องชั่ง {scaleIndex + 1}",
                    "เครื่องชั่ง", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        };

        EventHandler<ScaleChangedEventArgs> handler = (_, e) =>
        {
            if (e.Index == scaleIndex)
            {
                Dispatcher.InvokeAsync(UpdateStatus);
            }
        };

        _scaleService.ReadingChanged += handler;
        stack.Unloaded += (_, _) => _scaleService.ReadingChanged -= handler;

        return stack;
    }

    private static TextBox TruckScaleTextBox(string? value = null, bool multiline = false)
    {
        var box = new TextBox
        {
            Text = value ?? "",
            FontSize = 14,
            MinHeight = 35,
            Padding = new Thickness(9, 5, 9, 5),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 2, 0, 2)
        };
        return box;
    }

    private static TextBlock TruckScaleReadOnly(string lookup) => new() { Text = lookup, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 7, 0, 0) };

    private static ComboBox TruckScaleChoiceBox(IEnumerable<TruckScaleChoice> values, string selectedValue)
    {
        var choices = values.ToList();
        if (selectedValue.Length > 0 && choices.All(item => !item.Value.Equals(selectedValue, StringComparison.OrdinalIgnoreCase)))
            choices.Add(new(selectedValue, selectedValue));
        var combo = new ComboBox { IsEditable = true, StaysOpenOnEdit = false, FontSize = 13, MinHeight = 35,
            Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 2, 0, 2), VerticalContentAlignment = VerticalAlignment.Center };
        foreach (var value in choices) combo.Items.Add(new ComboBoxItem { Content = value.Label, Tag = value.Value });
        var selected = choices.FindIndex(item => item.Value.Equals(selectedValue, StringComparison.OrdinalIgnoreCase) ||
                                                  item.Label.Equals(selectedValue, StringComparison.OrdinalIgnoreCase));
        if (selected >= 0) combo.SelectedIndex = selected;
        else combo.Text = selectedValue;
        return combo;
    }

    private IEnumerable<TruckScaleChoice> TruckScaleRubberChoices(string current)
    {
        var choices = _truckScaleRubberTypes.ToList();
        if (current.Length > 0 && choices.All(item => !item.Value.Equals(current, StringComparison.OrdinalIgnoreCase))) choices.Add(new(current, current));
        return choices;
    }

    private IEnumerable<TruckScaleChoice> TruckScaleProvinceChoices(string current)
    {
        var choices = _truckScaleProvinces.ToList();
        if (current.Length > 0 && choices.All(item => !item.Value.Equals(current, StringComparison.OrdinalIgnoreCase))) choices.Add(new(current, current));
        return choices;
    }

    private (string Plate, string Province) SplitTruckScaleRegistration(string registration)
    {
        var value = registration.Trim();
        foreach (var province in _truckScaleProvinces.OrderByDescending(item => Math.Max(item.Value.Length, item.Label.Length)))
        {
            foreach (var suffix in new[] { province.Value, province.Label }.Where(item => item.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tail = " " + suffix;
                if (value.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                    return (value[..^tail.Length].Trim(), province.Value);
            }
        }
        return (value, "");
    }

    private static string ComposeTruckScaleRegistration(string plate, string province)
    {
        var registration = plate.Trim();
        var locality = province.Trim();
        if (locality.Length == 0) return registration;
        return registration.EndsWith(" " + locality, StringComparison.OrdinalIgnoreCase) ? registration : registration + " " + locality;
    }

    private static string TruckScaleValue(ComboBox combo) => combo.SelectedItem is ComboBoxItem item && item.Tag is string value
        ? value : combo.Text.Trim();

    private string TranslateTruckScaleType(string value) => value switch
    {
        "Pickup" => "รถกระบะ",
        "6 Wheels" => "รถ 6 ล้อ",
        "10 Wheels" => "รถ 10 ล้อ",
        "10 Wheels Trailer" => "รถพ่วง 10 ล้อ",
        "18 Wheels Trailer" => "รถพ่วง 18 ล้อ",
        "Trailer" => "รถพ่วง",
        _ => value
    };

    private static bool CanTruckScaleTowTrailer(string value)
    {
        var type = NormalizeTruckType(value);
        return type.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("Wheels Trailer", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("18-wheel", StringComparison.OrdinalIgnoreCase);
    }

    private static string TruckScaleNormalizeRubberCondition(string value) => value.Trim().ToUpperInvariant() switch
    {
        "แห้ง" or "DRY" => "DRY", "หมาด" or "DAMP" => "DAMP", "เปียก" or "WET" => "WET", _ => ""
    };

    private static string ReadNumber(System.Text.Json.JsonElement raw, params string[] keys)
    {
        var value = ReadProperty(raw, keys);
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("0.###", CultureInfo.InvariantCulture) : "";
    }

    private static bool TryReadScaleValue(string text, out double value) =>
        double.TryParse(text.Replace(",", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value) && value >= 0;

    private static string ToSnakeCase(string value)
    {
        var result = new System.Text.StringBuilder();
        foreach (var character in value)
        {
            if (char.IsUpper(character)) result.Append('_').Append(char.ToLowerInvariant(character));
            else result.Append(character);
        }
        return result.ToString();
    }

    private static object? NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

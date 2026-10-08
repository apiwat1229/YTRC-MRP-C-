namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private async void SettingsAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string action) return;
        switch (action)
        {
            case "SettingsSave":
                AppSettings.Current.ApiBaseUrl = ServerUrl.NormalizeBaseUrl(_apiBaseInput?.Text);
                AppSettings.Current.EnvironmentMode = _environmentInput?.SelectedItem?.ToString() ?? "production";
                AppSettings.Current.UpdateServerUrl = _updateUrlInput?.Text.Trim() ?? AppSettings.Current.UpdateServerUrl;
                AppSettings.Current.RememberUsername = _rememberUsernameInput?.IsChecked == true;
                if (!AppSettings.Current.RememberUsername) AppSettings.Current.SavedUsername = "";
                App.SettingsStore.Save(AppSettings.Current);
                FooterText.Text = $"YTRC-MRP  |  Client {ApplicationInfo.Version}";
                SetPageMessage("Settings saved to this device."); RenderCurrentPage(); break;
            case "ConnectionTest":
                SetPageMessage("Checking server…"); RenderCurrentPage();
                var health = await _apiClient.TestConnectionAsync(_apiBaseInput?.Text ?? AppSettings.Current.ApiBaseUrl);
                _apiOnline = health.IsOnline;
                SetConnection(health.IsOnline, health.IsOnline ? $"API connected · v{health.Version}" : "API offline");
                SetPageMessage(health.Message); RenderCurrentPage(); break;
            case "UpdateCheck": await CheckForUpdatesAsync(); break;
            case "PortsRefresh": RefreshScalePorts(); break;
            case "ScaleSave": SaveScaleConfigurations(); SetPageMessage("Scale settings saved."); RenderCurrentPage(); break;
            case "PrinterSave": SavePrinterSettings(); break;
        }
    }

    private void SetPageMessage(string message)
    {
        _pageMessage = message;
        RenderCurrentPage();
    }

    private async Task CheckForUpdatesAsync()
    {
        SetPageMessage("Checking update manifest…"); RenderCurrentPage();
        var result = await _updateService.CheckAsync();
        SetPageMessage(result.Message); RenderCurrentPage();
        if (!result.Success || !result.HasUpdate || result.Manifest is null) return;
        var manifest = result.Manifest;
        var prompt = $"Version {manifest.LatestVersion} is available.\n\n{manifest.ReleaseNotes}\n\nDownload this update?";
        if (MessageBox.Show(prompt, "YTRC-MRP update", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        try
        {
            var path = await _updateService.DownloadAsync(manifest);
            if (MessageBox.Show($"Update downloaded and verified:\n{path}\n\nLaunch the installer and close YTRC-MRP?",
                    "Update ready", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _updateService.LaunchInstaller(path);
                Close();
            }
        }
        catch (Exception ex) { MessageBox.Show($"Update download failed: {ex.Message}", "Update", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportRows()
    {
        if (_activeRows.Count == 0) return;
        var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = $"YTRC_{_settingsSection}_{DateTime.Now:yyyyMMdd_HHmm}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, CsvExportService.ToCsv(_activeRows), new System.Text.UTF8Encoding(true));
        SetPageMessage($"Exported {_activeRows.Count} rows to {dialog.FileName}.");
    }

    private void SavePrinterSettings()
    {
        AppSettings.Current.PrinterName = _printerNameInput?.Text.Trim() ?? "Default Windows Printer";
        AppSettings.Current.PrintCopies = int.TryParse(_printerCopiesInput?.Text, out var count) ? Math.Clamp(count, 1, 99) : 1;
        AppSettings.Current.SilentPrint = _printerSilentInput?.IsChecked == true;
        App.SettingsStore.Save(AppSettings.Current); SetPageMessage("Printer defaults saved on this device."); RenderCurrentPage();
    }

    private void SaveScaleConfigurations()
    {
        foreach (var editor in _scaleEditors) AppSettings.Current.Scales[editor.Index] = ReadScaleSettings(editor);
        App.SettingsStore.Save(AppSettings.Current);
    }

    private async void ScaleToggle_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not int index || index >= _scaleEditors.Count) return;
        var editor = _scaleEditors[index];
        try
        {
            if (_scaleService.IsConnected(index)) await _scaleService.DisconnectAsync(index);
            else
            {
                var config = ReadScaleSettings(editor);
                AppSettings.Current.Scales[index] = config;
                await _scaleService.ConnectAsync(index, config);
                App.SettingsStore.Save(AppSettings.Current);
            }
            editor.Toggle.Content = _scaleService.IsConnected(index) ? "Disconnect" : "Connect";
            editor.Status.Text = _scaleService.IsConnected(index) ? "Connected" : "Offline";
        }
        catch (Exception ex)
        {
            editor.Status.Text = $"Connection failed: {ex.Message}";
            editor.Status.Foreground = Brushes.IndianRed;
        }
    }

    private static ScaleSettings ReadScaleSettings(ScaleEditor editor) => new()
    {
        PortName = editor.Port.SelectedItem?.ToString() ?? "COM1",
        BaudRate = editor.Baud.SelectedItem is int baud ? baud : 9600,
        DataBits = AppSettings.Current.Scales[editor.Index].DataBits,
        Parity = AppSettings.Current.Scales[editor.Index].Parity,
        StopBits = AppSettings.Current.Scales[editor.Index].StopBits,
        DecimalPlaces = editor.Decimals.SelectedItem is int places ? places : 2,
        Simulation = editor.Simulation.IsChecked == true,
        Model = editor.Model.SelectedItem?.ToString() ?? "Generic",
        Enabled = true
    };

    private void RefreshScalePorts()
    {
        var ports = _scaleService.EnumeratePorts().ToList();
        if (ports.Count == 0) ports.AddRange(["COM1", "COM2", "COM3"]);
        foreach (var editor in _scaleEditors)
        {
            var selected = editor.Port.SelectedItem?.ToString();
            editor.Port.ItemsSource = ports;
            if (selected is not null && ports.Contains(selected)) editor.Port.SelectedItem = selected;
        }
    }

    private void ScaleService_ReadingChanged(object? sender, ScaleChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (e.Index >= _scaleEditors.Count) return;
            var editor = _scaleEditors[e.Index];
            editor.Status.Text = e.Connected ? (e.Reading?.IsStable == true ? "Stable" : "Unstable") : "Offline";
            editor.Status.Foreground = e.Connected ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : (Brush)FindResource("MutedBrush");
            editor.Reading.Text = e.Reading is null ? "—" : $"{e.Reading.Formatted} {e.Reading.Unit}";
            editor.Toggle.Content = e.Connected ? "Disconnect" : "Connect";
        });
    }
}

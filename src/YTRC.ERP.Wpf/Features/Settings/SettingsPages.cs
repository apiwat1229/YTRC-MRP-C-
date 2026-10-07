namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildSettingsPage()
    {
        var page = PageStack();
        page.Children.Add(TitleBlock("Tools & Settings", "Application configuration, access administration and connected devices"));
        page.Children.Add(_settingsSection switch
        {
            "System Config" => BuildSystemConfig(),
            "Users" => BuildRemoteWorkspace("User accounts", "GET /api/users"),
            "Roles" => BuildRemoteWorkspace("Roles & permissions", "GET /api/roles"),
            "Staff" => BuildRemoteWorkspace("Staff directory", "GET /api/access-control/staff · fallback GET /api/users"),
            "Scales" => BuildScaleSettings(),
            "Printer" => BuildPrinterSettings(),
            _ => BuildSystemConfig()
        });
        return page;
    }

    private UIElement BuildSystemConfig()
    {
        var form = new StackPanel();
        form.Children.Add(SectionHeading("Connection settings"));
        _apiBaseInput = new TextBox { Text = AppSettings.Current.ApiBaseUrl, Margin = new Thickness(0, 0, 0, 14) };
        form.Children.Add(Field("API base URL", _apiBaseInput));
        _environmentInput = new ComboBox { ItemsSource = new[] { "production", "staging", "local" },
            SelectedItem = AppSettings.Current.EnvironmentMode, Margin = new Thickness(0, 0, 0, 14) };
        form.Children.Add(Field("Environment", _environmentInput));
        _updateUrlInput = new TextBox { Text = AppSettings.Current.UpdateServerUrl, Margin = new Thickness(0, 0, 0, 12) };
        form.Children.Add(Field("Update manifest URL", _updateUrlInput));
        _rememberUsernameInput = new CheckBox { Content = "Remember username on this device", IsChecked = AppSettings.Current.RememberUsername,
            Margin = new Thickness(0, 0, 0, 18), Foreground = (Brush)FindResource("MutedBrush") };
        form.Children.Add(_rememberUsernameInput);
        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Save settings", "SettingsSave", primary: true));
        actions.Children.Add(ActionButton("Test server connection", "ConnectionTest"));
        actions.Children.Add(ActionButton("Check for updates", "UpdateCheck"));
        form.Children.Add(actions);
        form.Children.Add(Label("Preferences are stored in your local AppData folder. Authentication tokens are kept in memory only.",
            12, (Brush)FindResource("MutedBrush")));
        if (!string.IsNullOrEmpty(_pageMessage)) form.Children.Add(Banner("Status", _pageMessage, _pageMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E"));
        return Card(form, "#0F766E");
    }

    private UIElement BuildRemoteWorkspace(string title, string endpoint)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading(title));
        panel.Children.Add(Label(endpoint, 12, (Brush)FindResource("MutedBrush")));
        if (_session.IsPreview)
        {
            panel.Children.Add(Banner("Sign in required", "Guest preview cannot query account, role or staff data.", "#B7791F"));
            return Card(panel, "#297691");
        }
        if (!string.IsNullOrWhiteSpace(_pageMessage))
            panel.Children.Add(Banner("Workspace status", _pageMessage, _pageMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E"));
        var grid = new DataGrid { ItemsSource = _activeRows, AutoGenerateColumns = false, CanUserAddRows = false,
            IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, MinHeight = 260,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        grid.Columns.Add(new DataGridTextColumn { Header = "Name / role", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Title)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Account / department", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Subtitle)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Detail)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Status)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        panel.Children.Add(grid);
        return Card(panel, "#297691");
    }

    private UIElement BuildScaleSettings()
    {
        _scaleEditors.Clear();
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading("Industrial scales"));
        panel.Children.Add(Label("Configure up to two COM-port scales or use simulation mode. Supported parsers: Tiger TI-01/TWT-01, Toledo and generic numeric frames.",
            12, (Brush)FindResource("MutedBrush")));
        var cards = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 7, -6, 4) };
        for (var i = 0; i < 2; i++) cards.Children.Add(BuildScaleCard(i));
        panel.Children.Add(cards);
        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 4) };
        actions.Children.Add(ActionButton("Refresh COM ports", "PortsRefresh"));
        actions.Children.Add(ActionButton("Save scale settings", "ScaleSave", primary: true));
        panel.Children.Add(actions);
        return panel;
    }

    private UIElement BuildScaleCard(int index)
    {
        var config = AppSettings.Current.Scales[index];
        var ports = _scaleService.EnumeratePorts().ToList();
        if (!ports.Contains(config.PortName, StringComparer.OrdinalIgnoreCase)) ports.Add(config.PortName);
        if (ports.Count == 0) ports.AddRange(["COM1", "COM2", "COM3"]);
        var editor = new ScaleEditor(index)
        {
            Port = new ComboBox { ItemsSource = ports, SelectedItem = config.PortName, MinWidth = 110, Margin = new Thickness(0, 0, 10, 8) },
            Baud = new ComboBox { ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200 }, SelectedItem = config.BaudRate, MinWidth = 115, Margin = new Thickness(0, 0, 10, 8) },
            Model = new ComboBox { ItemsSource = new[] { "Tiger TI-01", "Tiger TWT-01", "Toledo", "Generic" }, SelectedItem = config.Model, MinWidth = 145, Margin = new Thickness(0, 0, 10, 8) },
            Decimals = new ComboBox { ItemsSource = new[] { 0, 1, 2, 3 }, SelectedItem = config.DecimalPlaces, MinWidth = 85, Margin = new Thickness(0, 0, 10, 8) },
            Simulation = new CheckBox { Content = "Simulation mode", IsChecked = config.Simulation, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8) },
            Status = Label(_scaleService.IsConnected(index) ? "Connected" : "Offline", 12, (Brush)FindResource("MutedBrush")),
            Reading = Label(_scaleService.LatestReading(index)?.Formatted ?? "—", 25, (Brush)FindResource("TextBrush"), FontWeights.SemiBold),
            Toggle = new Button { Content = _scaleService.IsConnected(index) ? "Disconnect" : "Connect", Tag = index, Margin = new Thickness(0, 0, 8, 0) }
        };
        editor.Toggle.Click += ScaleToggle_Click;
        _scaleEditors.Add(editor);
        var fields = new WrapPanel();
        fields.Children.Add(FieldCompact("COM port", editor.Port));
        fields.Children.Add(FieldCompact("Baud", editor.Baud));
        fields.Children.Add(FieldCompact("Scale model", editor.Model));
        fields.Children.Add(FieldCompact("Decimals", editor.Decimals));
        fields.Children.Add(editor.Simulation);
        var stack = new StackPanel();
        stack.Children.Add(Label($"Scale {index + 1}", 16, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        stack.Children.Add(fields);
        var lower = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(editor.Toggle, Dock.Right); lower.Children.Add(editor.Toggle);
        var read = new StackPanel(); read.Children.Add(editor.Status); read.Children.Add(editor.Reading); lower.Children.Add(read);
        stack.Children.Add(lower);
        var card = Card(stack, "#D97706"); card.Margin = new Thickness(6); card.Padding = new Thickness(15);
        return card;
    }

    private UIElement BuildPrinterSettings()
    {
        var stack = new StackPanel();
        stack.Children.Add(SectionHeading("Printers & reports"));
        stack.Children.Add(Label("Printer defaults are stored locally. Report layouts and print workflows are not wired in the current source project.",
            12, (Brush)FindResource("MutedBrush")));
        _printerNameInput = new TextBox { Text = AppSettings.Current.PrinterName, Margin = new Thickness(0, 0, 0, 14) };
        stack.Children.Add(Field("Printer name", _printerNameInput));
        _printerCopiesInput = new TextBox { Text = AppSettings.Current.PrintCopies.ToString(), Margin = new Thickness(0, 0, 0, 12), Width = 110, HorizontalAlignment = HorizontalAlignment.Left };
        stack.Children.Add(Field("Number of copies", _printerCopiesInput));
        _printerSilentInput = new CheckBox { Content = "Print silently when supported", IsChecked = AppSettings.Current.SilentPrint,
            Margin = new Thickness(0, 0, 0, 18), Foreground = (Brush)FindResource("MutedBrush") };
        stack.Children.Add(_printerSilentInput);
        var save = ActionButton("Save printer settings", "PrinterSave", primary: true);
        stack.Children.Add(save);
        return Card(stack, "#D97706");
    }
}

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildSettingsPage()
    {
        var page = PageStack();
        page.Children.Add(_settingsSection switch
        {
            "System Config" => BuildSystemConfig(),
            "Users" => BuildUsersPage(),
            "Security Overview" or "Role Access" or "Permission Catalog" or "User Access" or "Access Health" => BuildSecuritySettingsPage(),
            "Staff" => BuildRemoteWorkspace("Staff directory", "GET /api/access-control/staff · fallback GET /api/users"),
            "Scales" => BuildScaleSettings(),
            "Printer" => BuildPrinterSettings(),
            _ => BuildSystemConfig()
        });
        return page;
    }

    private UIElement BuildSystemConfig()
    {
        var form = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 7; row++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = SectionHeading("Connection settings");
        Grid.SetColumnSpan(heading, 2);
        form.Children.Add(heading);
        _apiBaseInput = new TextBox { Text = AppSettings.Current.ApiBaseUrl, Height = 34, MinHeight = 34 };
        var apiField = (FrameworkElement)Field("API base URL", _apiBaseInput);
        apiField.Margin = new Thickness(0, 0, 14, 12);
        Grid.SetRow(apiField, 1);
        form.Children.Add(apiField);
        _environmentInput = new ComboBox { ItemsSource = new[] { "production", "staging", "local" },
            SelectedItem = AppSettings.Current.EnvironmentMode, Height = 36, MinHeight = 36 };
        var environmentField = (FrameworkElement)Field("Environment", _environmentInput);
        environmentField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(environmentField, 1);
        Grid.SetColumn(environmentField, 1);
        form.Children.Add(environmentField);
        _updateUrlInput = new TextBox { Text = AppSettings.Current.UpdateServerUrl, Height = 34, MinHeight = 34 };
        var updateField = (FrameworkElement)Field("Update manifest URL", _updateUrlInput);
        updateField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(updateField, 2);
        Grid.SetColumnSpan(updateField, 2);
        form.Children.Add(updateField);
        _rememberUsernameInput = new CheckBox { Content = "Remember username on this device", IsChecked = AppSettings.Current.RememberUsername,
            Margin = new Thickness(0, 2, 0, 12), Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(_rememberUsernameInput, 3);
        Grid.SetColumnSpan(_rememberUsernameInput, 2);
        form.Children.Add(_rememberUsernameInput);
        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Save settings", "SettingsSave", primary: true));
        actions.Children.Add(ActionButton("Test server connection", "ConnectionTest"));
        actions.Children.Add(ActionButton("Check for updates", "UpdateCheck"));
        Grid.SetRow(actions, 4);
        Grid.SetColumnSpan(actions, 2);
        form.Children.Add(actions);
        var note = Label("Preferences are stored in your local AppData folder. Authentication tokens are kept in memory only.",
            12, (Brush)FindResource("MutedBrush"));
        note.Margin = new Thickness(0, 4, 0, 0);
        Grid.SetRow(note, 5);
        Grid.SetColumnSpan(note, 2);
        form.Children.Add(note);
        if (!string.IsNullOrEmpty(_pageMessage))
        {
            var status = Banner("Status", _pageMessage, _pageMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E");
            Grid.SetRow(status, 6);
            Grid.SetColumnSpan(status, 2);
            form.Children.Add(status);
        }
        return Card(form, "#0F766E");
    }

    private UIElement BuildRemoteWorkspace(string title, string endpoint)
    {
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading(title));
        var route = Label(endpoint, 12, (Brush)FindResource("MutedBrush"));
        route.Margin = new Thickness(0, 0, 0, 10);
        panel.Children.Add(route);
        if (_session.IsPreview)
        {
            panel.Children.Add(Banner("Sign in required", "Guest preview cannot query account, role or staff data.", "#B7791F"));
            return Card(panel, "#297691");
        }
        if (!string.IsNullOrWhiteSpace(_pageMessage))
            panel.Children.Add(Banner("Workspace status", _pageMessage, _pageMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ? "#DC2626" : "#0F766E"));
        var grid = CreateSettingsDataGrid(_activeRows, minHeight: 300);
        grid.Columns.Add(new DataGridTextColumn { Header = "Name / role", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Title)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Account / department", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Subtitle)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Detail)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Status)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        _remoteWorkspaceGrid = grid;
        panel.Children.Add(SettingsTableCard(grid, "#297691"));
        return panel;
    }

    private UIElement BuildScaleSettings()
    {
        _scaleEditors.Clear();
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading("Industrial scales"));
        panel.Children.Add(Label("Configure up to two COM-port scales or use simulation mode. Supported parsers: Tiger TI-01/TWT-01, Toledo and generic numeric frames.",
            12, (Brush)FindResource("MutedBrush")));
        var cards = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 8, -6, 4) };
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
            Port = new ComboBox { ItemsSource = ports, SelectedItem = config.PortName, Width = 120, Height = 36, MinHeight = 36, Margin = new Thickness(0, 0, 10, 8) },
            Baud = new ComboBox { ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200 }, SelectedItem = config.BaudRate, Width = 120, Height = 36, MinHeight = 36, Margin = new Thickness(0, 0, 10, 8) },
            Model = new ComboBox { ItemsSource = new[] { "Tiger TI-01", "Tiger TWT-01", "Toledo", "Generic" }, SelectedItem = config.Model, Width = 155, Height = 36, MinHeight = 36, Margin = new Thickness(0, 0, 10, 8) },
            Decimals = new ComboBox { ItemsSource = new[] { 0, 1, 2, 3 }, SelectedItem = config.DecimalPlaces, Width = 95, Height = 36, MinHeight = 36, Margin = new Thickness(0, 0, 10, 8) },
            Simulation = new CheckBox { Content = "Simulation mode", IsChecked = config.Simulation, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8) },
            Status = Label(_scaleService.IsConnected(index) ? "Connected" : "Offline", 12, (Brush)FindResource("MutedBrush")),
            Reading = Label(_scaleService.LatestReading(index)?.Formatted ?? "—", 25, (Brush)FindResource("TextBrush"), FontWeights.SemiBold),
            Toggle = new Button { Content = _scaleService.IsConnected(index) ? "Disconnect" : "Connect", Tag = index, Height = 34, MinWidth = 92, Margin = new Thickness(0, 0, 8, 0) }
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
        var title = Label($"Scale {index + 1}", 16, (Brush)FindResource("TextBrush"), FontWeights.SemiBold);
        title.Margin = new Thickness(0, 0, 0, 7);
        stack.Children.Add(title);
        stack.Children.Add(fields);
        var lower = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        DockPanel.SetDock(editor.Toggle, Dock.Right); lower.Children.Add(editor.Toggle);
        var read = new StackPanel(); read.Children.Add(editor.Status); read.Children.Add(editor.Reading); lower.Children.Add(read);
        stack.Children.Add(lower);
        var card = Card(stack, "#D97706");
        card.Margin = new Thickness(6);
        if (card.Child is DockPanel cardBody && cardBody.Children.Count > 1 && cardBody.Children[1] is Border cardContent)
            cardContent.Padding = new Thickness(14, 10, 12, 12);
        return card;
    }

    private UIElement BuildPrinterSettings()
    {
        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 5; row++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = SectionHeading("Printers & reports");
        Grid.SetColumnSpan(heading, 2);
        form.Children.Add(heading);
        var note = Label("Printer defaults are stored locally. Report layouts and print workflows are not wired in the current source project.",
            12, (Brush)FindResource("MutedBrush"));
        note.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(note, 1);
        Grid.SetColumnSpan(note, 2);
        form.Children.Add(note);
        _printerNameInput = new TextBox { Text = AppSettings.Current.PrinterName, Height = 34, MinHeight = 34 };
        var printerField = (FrameworkElement)Field("Printer name", _printerNameInput);
        printerField.Margin = new Thickness(0, 0, 14, 12);
        Grid.SetRow(printerField, 2);
        form.Children.Add(printerField);
        _printerCopiesInput = new TextBox { Text = AppSettings.Current.PrintCopies.ToString(), Width = 140, Height = 34, MinHeight = 34,
            HorizontalAlignment = HorizontalAlignment.Left };
        var copiesField = (FrameworkElement)Field("Number of copies", _printerCopiesInput);
        copiesField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(copiesField, 2);
        Grid.SetColumn(copiesField, 1);
        form.Children.Add(copiesField);
        _printerSilentInput = new CheckBox { Content = "Print silently when supported", IsChecked = AppSettings.Current.SilentPrint,
            Foreground = (Brush)FindResource("MutedBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 12) };
        Grid.SetRow(_printerSilentInput, 3);
        Grid.SetColumnSpan(_printerSilentInput, 2);
        form.Children.Add(_printerSilentInput);
        var actions = new WrapPanel();
        actions.Children.Add(ActionButton("Save printer settings", "PrinterSave", primary: true));
        Grid.SetRow(actions, 4);
        Grid.SetColumnSpan(actions, 2);
        form.Children.Add(actions);
        return Card(form, "#D97706");
    }

    private DataGrid CreateSettingsDataGrid(System.Collections.IEnumerable rows, double minHeight = 320, double maxHeight = 520)
        => new()
        {
            ItemsSource = rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,
            RowHeaderWidth = 0,
            RowHeight = 36,
            ColumnHeaderHeight = 34,
            FontSize = 13,
            MinHeight = minHeight,
            MaxHeight = maxHeight,
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0),
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            HorizontalGridLinesBrush = (Brush)FindResource("BorderBrushSoft"),
            VerticalGridLinesBrush = CreateBrush("#EDF1F5")
        };

    private Border SettingsTableCard(DataGrid grid, string accent)
    {
        var card = Card(grid, accent);
        if (card.Child is DockPanel body && body.Children.Count > 1 && body.Children[1] is Border content)
            content.Padding = new Thickness(0);
        return card;
    }
}

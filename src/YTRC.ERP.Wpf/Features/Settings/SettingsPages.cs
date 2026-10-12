using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentIcons.Common;
using Symbol = FluentIcons.Common.Icon;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildSettingsPage()
    {
        var page = new Grid { Margin = new Thickness(24, 16, 24, 24) };
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        page.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sidebar = BuildSettingsSidebar();
        Grid.SetColumn(sidebar, 0);
        page.Children.Add(sidebar);

        var contentHost = new Border
        {
            Margin = new Thickness(18, 0, 0, 0),
            Child = _settingsSection switch
            {
                "System Config" => BuildSystemConfig(),
                "Users" => BuildUsersPage(),
                "Security Overview" or "Role Access" or "Permission Catalog" or "User Access" or "Access Health" => BuildSecuritySettingsPage(),
                "Staff" => BuildRemoteWorkspace("Staff directory", "GET /api/access-control/staff · fallback GET /api/users"),
                "Scales" => BuildScaleSettings(),
                "Printer" => BuildPrinterSettings(),
                _ => BuildSystemConfig()
            }
        };
        Grid.SetColumn(contentHost, 1);
        page.Children.Add(contentHost);
        return page;
    }

    private UIElement BuildSettingsSidebar()
    {
        var sidebar = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(10, 14, 10, 16),
            VerticalAlignment = VerticalAlignment.Top
        };

        var stack = new StackPanel();

        // Header
        var titleStack = new StackPanel { Margin = new Thickness(6, 2, 6, 12) };
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headerRow.Children.Add(new FluentIcon
        {
            Icon = Symbol.Settings,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        headerRow.Children.Add(Label("Settings", 17, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        titleStack.Children.Add(headerRow);
        titleStack.Children.Add(Label("System config & administration", 11.5, (Brush)FindResource("MutedBrush")));
        stack.Children.Add(titleStack);

        void AddCategory(string categoryName)
        {
            var header = Label(categoryName.ToUpperInvariant(), 10.5, (Brush)FindResource("MutedBrush"), FontWeights.Bold);
            header.Margin = new Thickness(8, 12, 8, 4);
            stack.Children.Add(header);
        }

        void AddItem(string key, string title, Symbol icon)
        {
            var isSelected = string.Equals(_settingsSection, key, StringComparison.OrdinalIgnoreCase);

            var button = new Button
            {
                Height = 36,
                Margin = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(10, 0, 10, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = isSelected ? (Brush)FindResource("ControlPressedBackground") : Brushes.Transparent,
                BorderBrush = isSelected ? (Brush)FindResource("ControlPressedBorder") : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var itemPanel = new DockPanel();
            var iconControl = new FluentIcon
            {
                Icon = icon,
                IconSize = IconSize.Size16,
                IconVariant = IconVariant.Regular,
                Foreground = isSelected ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedBrush"),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(iconControl, Dock.Left);
            itemPanel.Children.Add(iconControl);

            var label = Label(title, 13, (Brush)FindResource("TextBrush"),
                isSelected ? FontWeights.SemiBold : FontWeights.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            itemPanel.Children.Add(label);

            button.Content = itemPanel;
            button.Click += (_, _) => NavigateToSettings(key);
            stack.Children.Add(button);
        }

        AddCategory("System & Network");
        AddItem("System Config", "Connection & System", Symbol.Wrench);

        AddCategory("Hardware & Devices");
        AddItem("Scales", "Industrial Scales", Symbol.Scales);
        AddItem("Printer", "Printers & Reports", Symbol.Print);

        AddCategory("Identity & Access");
        AddItem("Users", "User Accounts", Symbol.People);
        AddItem("Staff", "Staff Directory", Symbol.Person);

        AddCategory("Security & Governance");
        AddItem("Security Overview", "Security Overview", Symbol.Shield);
        AddItem("Role Access", "Roles & Permissions", Symbol.Key);
        AddItem("Permission Catalog", "Permission Catalog", Symbol.DocumentBulletList);
        AddItem("User Access", "User Direct Access", Symbol.Person);
        AddItem("Access Health", "Access Health", Symbol.HeartPulse);

        sidebar.Child = stack;
        return sidebar;
    }

    private UIElement BuildSystemConfig()
    {
        var panel = new StackPanel();

        // Section Title Header
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(Label("Connection & System Configuration", 20, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        titleBlock.Children.Add(Label("Configure FastAPI backend URL, active deployment tier, and workstation preferences.", 12, (Brush)FindResource("MutedBrush")));
        headerGrid.Children.Add(titleBlock);

        var statusBadge = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            Background = _apiOnline ? (Brush)FindResource("ControlPressedBackground") : new SolidColorBrush(Color.FromRgb(254, 242, 242)),
            BorderBrush = _apiOnline ? (Brush)FindResource("ControlPressedBorder") : new SolidColorBrush(Color.FromRgb(254, 202, 202)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var statusText = new StackPanel { Orientation = Orientation.Horizontal };
        statusText.Children.Add(new FluentIcon
        {
            Icon = _apiOnline ? Symbol.Checkmark : Symbol.Dismiss,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            Foreground = _apiOnline ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromRgb(220, 38, 38)),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        statusText.Children.Add(Label(_apiOnline ? "API Online" : "API Offline", 12,
            _apiOnline ? (Brush)FindResource("AccentBrush") : new SolidColorBrush(Color.FromRgb(220, 38, 38)), FontWeights.SemiBold));
        statusBadge.Child = statusText;
        Grid.SetColumn(statusBadge, 1);
        headerGrid.Children.Add(statusBadge);
        panel.Children.Add(headerGrid);

        // Form Fields
        var form = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 6; row++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _apiBaseInput = new TextBox { Text = AppSettings.Current.ApiBaseUrl, Height = 34, MinHeight = 34 };
        var apiField = (FrameworkElement)Field("API base URL", _apiBaseInput);
        apiField.Margin = new Thickness(0, 0, 14, 12);
        Grid.SetRow(apiField, 0);
        form.Children.Add(apiField);

        _environmentInput = new ComboBox
        {
            ItemsSource = new[] { "production", "staging", "local" },
            SelectedItem = AppSettings.Current.EnvironmentMode,
            Height = 36,
            MinHeight = 36
        };
        var environmentField = (FrameworkElement)Field("Environment mode", _environmentInput);
        environmentField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(environmentField, 0);
        Grid.SetColumn(environmentField, 1);
        form.Children.Add(environmentField);

        _updateUrlInput = new TextBox { Text = AppSettings.Current.UpdateServerUrl, Height = 34, MinHeight = 34 };
        var updateField = (FrameworkElement)Field("Software update manifest URL", _updateUrlInput);
        updateField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(updateField, 1);
        Grid.SetColumnSpan(updateField, 2);
        form.Children.Add(updateField);

        _rememberUsernameInput = new CheckBox
        {
            Content = "Remember username on this workstation",
            IsChecked = AppSettings.Current.RememberUsername,
            Margin = new Thickness(0, 4, 0, 12),
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(_rememberUsernameInput, 2);
        Grid.SetColumnSpan(_rememberUsernameInput, 2);
        form.Children.Add(_rememberUsernameInput);

        var note = Label("Client preferences are saved in your local AppData folder. Authentication tokens are kept in memory only.",
            12, (Brush)FindResource("MutedBrush"));
        note.Margin = new Thickness(0, 0, 0, 14);
        Grid.SetRow(note, 3);
        Grid.SetColumnSpan(note, 2);
        form.Children.Add(note);

        // Action Bar (Bottom)
        var actionsGrid = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var leftActions = new StackPanel { Orientation = Orientation.Horizontal };
        leftActions.Children.Add(ActionButton("Test server connection", "ConnectionTest"));
        leftActions.Children.Add(ActionButton("Check for updates", "UpdateCheck"));
        actionsGrid.Children.Add(leftActions);

        var saveButton = ActionButton("Save settings", "SettingsSave", primary: true);
        DockPanel.SetDock(saveButton, Dock.Right);
        actionsGrid.Children.Add(saveButton);

        Grid.SetRow(actionsGrid, 4);
        Grid.SetColumnSpan(actionsGrid, 2);
        form.Children.Add(actionsGrid);

        if (!string.IsNullOrEmpty(_pageMessage))
        {
            var isFail = _pageMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                         _pageMessage.Contains("offline", StringComparison.OrdinalIgnoreCase);
            var status = Banner("Status notification", _pageMessage, isFail ? "#DC2626" : "#0F766E");
            Grid.SetRow(status, 5);
            Grid.SetColumnSpan(status, 2);
            form.Children.Add(status);
        }

        panel.Children.Add(Card(form, "#0F766E"));
        return panel;
    }

    private UIElement BuildRemoteWorkspace(string title, string endpoint)
    {
        var panel = new StackPanel();
        panel.Children.Add(Label(title, 20, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
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
        var grid = CreateSettingsDataGrid(_activeRows, minHeight: 340);
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
        panel.Children.Add(Label("Industrial Weighbridge Scales", 20, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        panel.Children.Add(Label("Configure up to two serial COM-port scales or simulation mode. Supported parsers: Tiger TI-01/TWT-01, Toledo and generic frames.",
            12, (Brush)FindResource("MutedBrush")));

        var cards = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 12, -6, 10) };
        for (var i = 0; i < 2; i++) cards.Children.Add(BuildScaleCard(i));
        panel.Children.Add(cards);

        var actions = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        actions.Children.Add(ActionButton("Refresh COM ports", "PortsRefresh"));
        var saveBtn = ActionButton("Save scale settings", "ScaleSave", primary: true);
        DockPanel.SetDock(saveBtn, Dock.Right);
        actions.Children.Add(saveBtn);
        panel.Children.Add(actions);
        return panel;
    }

    private UIElement BuildScaleCard(int index)
    {
        var config = AppSettings.Current.Scales[index];
        var ports = _scaleService.EnumeratePorts().ToList();
        if (!ports.Contains(config.PortName, StringComparer.OrdinalIgnoreCase)) ports.Add(config.PortName);
        if (ports.Count == 0) ports.AddRange(["COM1", "COM2", "COM3"]);
        var isConn = _scaleService.IsConnected(index);
        var editor = new ScaleEditor(index)
        {
            Port = new ComboBox { ItemsSource = ports, SelectedItem = config.PortName, Height = 34, MinHeight = 34 },
            Baud = new ComboBox { ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200 }, SelectedItem = config.BaudRate, Height = 34, MinHeight = 34 },
            Model = new ComboBox { ItemsSource = new[] { "Tiger TI-01", "Tiger TWT-01", "Toledo", "Generic" }, SelectedItem = config.Model, Height = 34, MinHeight = 34 },
            Decimals = new ComboBox { ItemsSource = new[] { 0, 1, 2, 3 }, SelectedItem = config.DecimalPlaces, Height = 34, MinHeight = 34 },
            Simulation = new CheckBox { Content = "Enable simulation mode (test without hardware)", IsChecked = config.Simulation, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 0, 4) },
            Status = Label(isConn ? "Connected" : "Offline", 11.5, isConn ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("MutedBrush"), FontWeights.SemiBold),
            Reading = Label(_scaleService.LatestReading(index)?.Formatted ?? "0.0 kg", 26, (Brush)FindResource("AccentBrush"), FontWeights.Bold),
            Toggle = new Button
            {
                Content = isConn ? "Disconnect" : "Connect",
                Tag = index,
                Height = 32,
                MinWidth = 88,
                Style = (Style)FindResource(isConn ? typeof(Button) : "PrimaryButton"),
                Margin = new Thickness(0)
            }
        };
        editor.Toggle.Click += ScaleToggle_Click;
        _scaleEditors.Add(editor);

        var stack = new StackPanel();

        // Top Header
        var topRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = new StackPanel();
        titleBlock.Children.Add(Label($"Scale {index + 1} (Weighbridge {index + 1})", 15, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        titleBlock.Children.Add(editor.Status);
        topRow.Children.Add(titleBlock);

        Grid.SetColumn(editor.Toggle, 1);
        topRow.Children.Add(editor.Toggle);
        stack.Children.Add(topRow);

        // Digital Weight Reading Display
        var readingBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 244)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(187, 247, 208)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var readDock = new DockPanel();
        var readLabel = Label("Live Weight Reading", 11, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold);
        readLabel.VerticalAlignment = VerticalAlignment.Center;
        readDock.Children.Add(readLabel);
        DockPanel.SetDock(editor.Reading, Dock.Right);
        editor.Reading.HorizontalAlignment = HorizontalAlignment.Right;
        readDock.Children.Add(editor.Reading);
        readingBox.Child = readDock;
        stack.Children.Add(readingBox);

        // Grid form fields
        var formGrid = new Grid();
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var portField = (FrameworkElement)Field("COM Port", editor.Port);
        portField.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(portField, 0); Grid.SetColumn(portField, 0);
        formGrid.Children.Add(portField);

        var baudField = (FrameworkElement)Field("Baud rate", editor.Baud);
        baudField.Margin = new Thickness(8, 0, 0, 8);
        Grid.SetRow(baudField, 0); Grid.SetColumn(baudField, 1);
        formGrid.Children.Add(baudField);

        var modelField = (FrameworkElement)Field("Protocol model", editor.Model);
        modelField.Margin = new Thickness(0, 0, 8, 8);
        Grid.SetRow(modelField, 1); Grid.SetColumn(modelField, 0);
        formGrid.Children.Add(modelField);

        var decimalsField = (FrameworkElement)Field("Decimals", editor.Decimals);
        decimalsField.Margin = new Thickness(8, 0, 0, 8);
        Grid.SetRow(decimalsField, 1); Grid.SetColumn(decimalsField, 1);
        formGrid.Children.Add(decimalsField);

        Grid.SetRow(editor.Simulation, 2);
        Grid.SetColumnSpan(editor.Simulation, 2);
        formGrid.Children.Add(editor.Simulation);

        stack.Children.Add(formGrid);

        var card = Card(stack, index == 0 ? "#0F766E" : "#0284C7");
        card.Margin = new Thickness(6);
        return card;
    }

    private UIElement BuildPrinterSettings()
    {
        var panel = new StackPanel();
        panel.Children.Add(Label("Printers & Document Output", 20, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        panel.Children.Add(Label("Configure local printer defaults for queue tickets, weight receipts, and summary slips.",
            12, (Brush)FindResource("MutedBrush")));

        var form = new Grid { Margin = new Thickness(0, 10, 0, 4) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 5; row++)
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _printerNameInput = new TextBox { Text = AppSettings.Current.PrinterName, Height = 34, MinHeight = 34 };
        var printerField = (FrameworkElement)Field("Default printer name", _printerNameInput);
        printerField.Margin = new Thickness(0, 0, 14, 12);
        Grid.SetRow(printerField, 0);
        form.Children.Add(printerField);

        _printerCopiesInput = new TextBox
        {
            Text = AppSettings.Current.PrintCopies.ToString(),
            Height = 34,
            MinHeight = 34
        };
        var copiesField = (FrameworkElement)Field("Print copies", _printerCopiesInput);
        copiesField.Margin = new Thickness(0, 0, 0, 12);
        Grid.SetRow(copiesField, 0);
        Grid.SetColumn(copiesField, 1);
        form.Children.Add(copiesField);

        _printerSilentInput = new CheckBox
        {
            Content = "Silent print (print directly without dialog prompt when supported)",
            IsChecked = AppSettings.Current.SilentPrint,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(_printerSilentInput, 1);
        Grid.SetColumnSpan(_printerSilentInput, 2);
        form.Children.Add(_printerSilentInput);

        var note = Label("Printer preferences are saved locally on this machine.", 12, (Brush)FindResource("MutedBrush"));
        note.Margin = new Thickness(0, 0, 0, 14);
        Grid.SetRow(note, 2);
        Grid.SetColumnSpan(note, 2);
        form.Children.Add(note);

        var actions = new DockPanel();
        var saveBtn = ActionButton("Save printer settings", "PrinterSave", primary: true);
        DockPanel.SetDock(saveBtn, Dock.Right);
        actions.Children.Add(saveBtn);
        Grid.SetRow(actions, 3);
        Grid.SetColumnSpan(actions, 2);
        form.Children.Add(actions);

        panel.Children.Add(Card(form, "#D97706"));
        return panel;
    }

    private DataGrid CreateSettingsDataGrid(System.Collections.IEnumerable rows, double minHeight = 320, double maxHeight = 560)
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
            VerticalGridLinesBrush = Brushes.Transparent,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

    private Border SettingsTableCard(DataGrid grid, string accent)
    {
        var card = Card(grid, accent);
        if (card.Child is DockPanel body && body.Children.Count > 1 && body.Children[1] is Border content)
            content.Padding = new Thickness(0);
        return card;
    }
}

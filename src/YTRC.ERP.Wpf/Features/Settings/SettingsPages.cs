using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using FluentIcons.Common;
using Symbol = FluentIcons.Common.Icon;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildSettingsPage()
    {
        var page = new Grid { Margin = UiTokens.PageMargin };
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
                "System Config" => WrapInSettingsScrollViewer(BuildSystemConfig()),
                "Users" => BuildUsersPage(),
                "Security Overview" or "Role Access" or "Permission Catalog" or "User Access" or "Access Health" => BuildSecuritySettingsPage(),
                "Staff" => BuildRemoteWorkspace("Staff Directory", "GET /api/access-control/staff · fallback GET /api/users"),
                "Scales" => WrapInSettingsScrollViewer(BuildScaleSettings()),
                "Printer" => WrapInSettingsScrollViewer(BuildPrinterSettings()),
                _ => WrapInSettingsScrollViewer(BuildSystemConfig())
            }
        };
        Grid.SetColumn(contentHost, 1);
        page.Children.Add(contentHost);
        return page;
    }

    private static UIElement WrapInSettingsScrollViewer(UIElement element) =>
        new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = element
        };

    private UIElement BuildSettingsSidebar()
    {
        var sidebar = new Border
        {
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
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
            Foreground = UiTokens.Brushes.Accent,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        headerRow.Children.Add(Label("Settings", UiTokens.FontSizes.Section, UiTokens.Brushes.TextPrimary, UiTokens.Weights.SemiBold));
        titleStack.Children.Add(headerRow);
        titleStack.Children.Add(Label("System config & administration", UiTokens.FontSizes.Small, UiTokens.Brushes.TextMuted));
        stack.Children.Add(titleStack);

        void AddCategory(string categoryName)
        {
            var header = Label(categoryName.ToUpperInvariant(), UiTokens.FontSizes.Badge, UiTokens.Brushes.TextMuted, UiTokens.Weights.Bold);
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

        var topActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var testBtn = ActionButton("Test server connection", "ConnectionTest");
        testBtn.Margin = new Thickness(0, 0, 8, 0);
        topActions.Children.Add(testBtn);

        var saveBtn = ActionButton("Save settings", "SettingsSave", primary: true);
        saveBtn.Margin = new Thickness(0);
        topActions.Children.Add(saveBtn);

        var hero = CreatePageHeroHeader(
            string.Empty,
            "Connection & System Configuration",
            "Configure FastAPI backend URL, active deployment tier, and workstation preferences.",
            Symbol.Wrench,
            topActions);
        panel.Children.Add(hero);

        if (!string.IsNullOrEmpty(_pageMessage))
        {
            var isFail = _pageMessage.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                         _pageMessage.Contains("offline", StringComparison.OrdinalIgnoreCase);
            var notice = Banner(isFail ? "Connection warning" : "System notification", _pageMessage, isFail ? "#DC2626" : "#0F766E");
            panel.Children.Add(notice);
        }

        // Two-column layout
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Panel: Server & Health Monitor Card
        var monitorCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var monitorDock = new DockPanel();

        var monitorHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var mhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        mhTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.CloudCheckmark,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        mhTitle.Children.Add(new TextBlock { Text = "Server Health", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        monitorHeader.Child = mhTitle;
        DockPanel.SetDock(monitorHeader, Dock.Top);
        monitorDock.Children.Add(monitorHeader);

        var monitorBody = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };

        // Hero Status Indicator
        var heroBox = new Border
        {
            Background = _apiOnline ? new SolidColorBrush(Color.FromRgb(240, 253, 244)) : new SolidColorBrush(Color.FromRgb(254, 242, 242)),
            BorderBrush = _apiOnline ? new SolidColorBrush(Color.FromRgb(187, 247, 208)) : new SolidColorBrush(Color.FromRgb(254, 202, 202)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var heroStack = new StackPanel();
        var heroRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 4) };
        heroRow.Children.Add(new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = _apiOnline ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            Margin = new Thickness(0, 0, 8, 0)
        });
        heroRow.Children.Add(new TextBlock
        {
            Text = _apiOnline ? "FastAPI Online" : "Service Offline",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = _apiOnline ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(185, 28, 28))
        });
        heroStack.Children.Add(heroRow);
        heroStack.Children.Add(new TextBlock
        {
            Text = _apiOnline ? "Ready to accept ERP transactions and queries." : "Cannot establish socket or HTTP link to backend.",
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedBrush")
        });
        heroBox.Child = heroStack;
        monitorBody.Children.Add(heroBox);

        void AddMetaRow(string label, string val, Brush valBrush)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)FindResource("MutedBrush") });
            var valText = new TextBlock { Text = val, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = valBrush, TextAlignment = TextAlignment.Right };
            DockPanel.SetDock(valText, Dock.Right);
            row.Children.Add(valText);
            monitorBody.Children.Add(row);
        }
        AddMetaRow("Environment", AppSettings.Current.EnvironmentMode.ToUpperInvariant(), (Brush)FindResource("TextBrush"));
        AddMetaRow("Client Version", ApplicationInfo.Version, (Brush)FindResource("TextBrush"));
        AddMetaRow("Protocol", AppSettings.Current.ApiBaseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "TLS / HTTPS" : "HTTP", (Brush)FindResource("AccentBrush"));

        var updateBtn = ActionButton("Check for updates", "UpdateCheck");
        updateBtn.Margin = new Thickness(0, 16, 0, 0);
        updateBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        monitorBody.Children.Add(updateBtn);

        monitorDock.Children.Add(monitorBody);
        monitorCard.Child = monitorDock;
        Grid.SetColumn(monitorCard, 0);
        body.Children.Add(monitorCard);

        // Right Panel: Form Card
        var formCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var formDock = new DockPanel();

        var formHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 12, 18, 12)
        };
        var fhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fhTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.Wrench,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        fhTitle.Children.Add(new TextBlock { Text = "Configuration Parameters", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        formHeader.Child = fhTitle;
        DockPanel.SetDock(formHeader, Dock.Top);
        formDock.Children.Add(formHeader);

        var formBody = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

        _apiBaseInput = new TextBox { Text = AppSettings.Current.ApiBaseUrl, Height = 34, MinHeight = 34, Padding = new Thickness(8, 2, 8, 2) };
        var apiField = (FrameworkElement)Field("API base URL", _apiBaseInput);
        apiField.Margin = new Thickness(0, 0, 0, 14);
        formBody.Children.Add(apiField);

        _environmentInput = new ComboBox
        {
            ItemsSource = new[] { "production", "staging", "local" },
            SelectedItem = AppSettings.Current.EnvironmentMode,
            Height = 34,
            MinHeight = 34
        };
        var envField = (FrameworkElement)Field("Environment mode", _environmentInput);
        envField.Margin = new Thickness(0, 0, 0, 14);
        formBody.Children.Add(envField);

        _updateUrlInput = new TextBox { Text = AppSettings.Current.UpdateServerUrl, Height = 34, MinHeight = 34, Padding = new Thickness(8, 2, 8, 2) };
        var updateField = (FrameworkElement)Field("Software update manifest URL", _updateUrlInput);
        updateField.Margin = new Thickness(0, 0, 0, 16);
        formBody.Children.Add(updateField);

        // Workstation Box
        var workBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var wbStack = new StackPanel();
        wbStack.Children.Add(new TextBlock
        {
            Text = "Workstation Preferences",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _rememberUsernameInput = new CheckBox
        {
            Content = "Remember username on this workstation",
            IsChecked = AppSettings.Current.RememberUsername,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)FindResource("TextBrush")
        };
        wbStack.Children.Add(_rememberUsernameInput);
        wbStack.Children.Add(new TextBlock
        {
            Text = "Preferences are saved in your local AppData directory. Authentication tokens are kept in memory only.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedBrush")
        });
        workBox.Child = wbStack;
        formBody.Children.Add(workBox);

        formDock.Children.Add(formBody);
        formCard.Child = formDock;
        Grid.SetColumn(formCard, 2);
        body.Children.Add(formCard);

        panel.Children.Add(body);
        return panel;
    }

    private UIElement BuildRemoteWorkspace(string title, string endpoint)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var searchIcon = new FluentIcon
        {
            Icon = Symbol.Search,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            FontSize = 16,
            Width = 16,
            Height = 16,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(8, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(searchIcon, 0);
        searchGrid.Children.Add(searchIcon);

        var staffSearchBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 8, 2),
            ToolTip = "Search staff directory"
        };
        var staffPlaceholder = new TextBlock
        {
            Text = "Search...",
            FontSize = 13,
            Foreground = (Brush)FindResource("MutedBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            IsHitTestVisible = false
        };
        staffSearchBox.TextChanged += (_, _) =>
        {
            staffPlaceholder.Visibility = string.IsNullOrEmpty(staffSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            var q = staffSearchBox.Text.Trim();
            if (_remoteWorkspaceGrid != null)
            {
                _remoteWorkspaceGrid.ItemsSource = string.IsNullOrEmpty(q)
                    ? _activeRows
                    : _activeRows.Where(r => (r.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                             (r.Subtitle?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                             (r.Detail?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                             (r.Status?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }
        };

        var inputHost = new Grid();
        inputHost.Children.Add(staffPlaceholder);
        inputHost.Children.Add(staffSearchBox);
        Grid.SetColumn(inputHost, 1);
        searchGrid.Children.Add(inputHost);

        var searchBoxContainer = new Border
        {
            Width = 190,
            Height = 34,
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = searchGrid
        };

        var refreshBtn = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new FluentIcon { Icon = Symbol.ArrowClockwise, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) },
                    new TextBlock { Text = "Refresh", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Height = 34,
            Padding = new Thickness(14, 0, 14, 0),
            Style = (Style)FindResource(typeof(Button))
        };
        refreshBtn.Click += async (_, _) => await LoadRemoteWorkspaceAsync();

        var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        headerActions.Children.Add(searchBoxContainer);
        headerActions.Children.Add(refreshBtn);

        var countBadge = UiTokens.CreateBadge(
            $"{_activeRows.Count} active records",
            UiTokens.Brushes.MintSurface,
            UiTokens.Brushes.Accent,
            UiTokens.Brushes.MintBorder,
            UiTokens.Radii.Medium,
            new Thickness(10, 3, 10, 3));
        countBadge.VerticalAlignment = VerticalAlignment.Center;

        var hero = CreatePageHeroHeader(
            string.Empty,
            title,
            "Company staff directory, departmental assignments and user link status.",
            Symbol.Person,
            headerActions,
            titleBadge: countBadge);
        Grid.SetRow(hero, 0);
        root.Children.Add(hero);

        if (_session.IsPreview)
        {
            var p = Banner("Sign in required", "Guest preview cannot query account, role or staff data.", "#B7791F");
            Grid.SetRow(p, 1);
            root.Children.Add(p);
            return root;
        }

        // Table Card
        var tableCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true
        };
        var tableDock = new DockPanel();

        if (!string.IsNullOrWhiteSpace(_pageMessage) && _pageMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase))
        {
            var errNotice = Banner("Workspace status", _pageMessage, "#DC2626");
            DockPanel.SetDock(errNotice, Dock.Top);
            tableDock.Children.Add(errNotice);
        }

        var grid = CreateSettingsDataGrid(_activeRows, minHeight: 150, maxHeight: double.PositiveInfinity);
        grid.Columns.Add(new DataGridTextColumn { Header = "Name / role", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Title)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Account / department", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Subtitle)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Details", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Detail)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding(nameof(ApiRow.Status)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.VerticalAlignment = VerticalAlignment.Stretch;
        _remoteWorkspaceGrid = grid;

        tableDock.Children.Add(grid);
        tableCard.Child = tableDock;

        Grid.SetRow(tableCard, 1);
        root.Children.Add(tableCard);

        return root;
    }

    private UIElement BuildScaleSettings(bool isStandalonePage = false)
    {
        _scaleEditors.Clear();
        var panel = new StackPanel();

        var topActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var refreshPorts = ActionButton("Refresh COM ports", "PortsRefresh");
        refreshPorts.Margin = new Thickness(0, 0, 8, 0);
        topActions.Children.Add(refreshPorts);

        var saveScales = ActionButton("Save scale settings", "ScaleSave", primary: true);
        saveScales.Margin = new Thickness(0);
        topActions.Children.Add(saveScales);

        var hero = CreatePageHeroHeader(
            string.Empty,
            "Industrial Weighbridge Scales",
            isStandalonePage
                ? "Configure dual serial COM-port digital indicators or simulation mode for RRC raw rubber weighbridge operations."
                : "Configure dual serial COM-port digital indicators or simulation mode. Supported protocols: Tiger TI-01 / TWT-01, Toledo, Generic.",
            Symbol.Scales,
            topActions);
        panel.Children.Add(hero);

        if (!string.IsNullOrEmpty(_pageMessage))
            panel.Children.Add(Banner("Scale notification", _pageMessage, "#0F766E"));

        var cards = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 0, -6, 10) };
        for (var i = 0; i < 2; i++) cards.Children.Add(BuildScaleCard(i));
        panel.Children.Add(cards);

        return panel;
    }

    private UIElement BuildRrcScaleSettingsPage()
    {
        var page = PageStack();
        page.Children.Add(BuildScaleSettings(isStandalonePage: true));
        return page;
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
            Simulation = new CheckBox { Content = "Enable simulation mode (test without hardware)", IsChecked = config.Simulation, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) },
            Status = Label(isConn ? "Connected" : "Offline", 11.5, isConn ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontWeights.SemiBold),
            Reading = new TextBlock
            {
                Text = _scaleService.LatestReading(index)?.Formatted ?? "0.0 kg",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = isConn ? new SolidColorBrush(Color.FromRgb(56, 189, 248)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontFamily = (FontFamily)FindResource("AppFontFamily"),
                VerticalAlignment = VerticalAlignment.Center
            },
            Toggle = new Button
            {
                Content = isConn ? "Disconnect" : "Connect",
                Tag = index,
                Height = 32,
                Width = 96,
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)FindResource(isConn ? typeof(Button) : "PrimaryButton"),
                Margin = new Thickness(0)
            }
        };
        editor.Toggle.Click += ScaleToggle_Click;
        _scaleEditors.Add(editor);

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            Margin = new Thickness(6)
        };
        var mainDock = new DockPanel();

        // Card Header
        var cardHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var chDock = new DockPanel();
        var chTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        chTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.Scales,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        var titleBlock = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleBlock.Children.Add(new TextBlock { Text = $"Scale {index + 1} (Weighbridge {index + 1})", FontSize = 14.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        titleBlock.Children.Add(editor.Status);
        chTitle.Children.Add(titleBlock);

        DockPanel.SetDock(editor.Toggle, Dock.Right);
        chDock.Children.Add(editor.Toggle);
        chDock.Children.Add(chTitle);
        cardHeader.Child = chDock;
        DockPanel.SetDock(cardHeader, Dock.Top);
        mainDock.Children.Add(cardHeader);

        // Body
        var body = new StackPanel { Margin = new Thickness(18, 14, 18, 18) };

        // Digital LED Display Box
        var ledBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // Slate 900
            BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var ledDock = new DockPanel();
        var ledLabelStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        ledLabelStack.Children.Add(new TextBlock { Text = "LIVE WEIGHBRIDGE READING", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)) });
        ledLabelStack.Children.Add(new TextBlock { Text = isConn ? "Active COM stream" : "Sensor idle", FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
        ledDock.Children.Add(ledLabelStack);

        DockPanel.SetDock(editor.Reading, Dock.Right);
        editor.Reading.HorizontalAlignment = HorizontalAlignment.Right;
        ledDock.Children.Add(editor.Reading);
        ledBox.Child = ledDock;
        body.Children.Add(ledBox);

        // Form Fields Grid
        var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var portField = (FrameworkElement)Field("COM Port", editor.Port);
        portField.Margin = new Thickness(0, 0, 8, 10);
        Grid.SetRow(portField, 0); Grid.SetColumn(portField, 0);
        formGrid.Children.Add(portField);

        var baudField = (FrameworkElement)Field("Baud rate", editor.Baud);
        baudField.Margin = new Thickness(8, 0, 0, 10);
        Grid.SetRow(baudField, 0); Grid.SetColumn(baudField, 1);
        formGrid.Children.Add(baudField);

        var modelField = (FrameworkElement)Field("Protocol model", editor.Model);
        modelField.Margin = new Thickness(0, 0, 8, 10);
        Grid.SetRow(modelField, 1); Grid.SetColumn(modelField, 0);
        formGrid.Children.Add(modelField);

        var decimalsField = (FrameworkElement)Field("Decimals", editor.Decimals);
        decimalsField.Margin = new Thickness(8, 0, 0, 10);
        Grid.SetRow(decimalsField, 1); Grid.SetColumn(decimalsField, 1);
        formGrid.Children.Add(decimalsField);
        body.Children.Add(formGrid);

        // Simulation Box
        var simBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(12, 8, 12, 8)
        };
        simBox.Child = editor.Simulation;
        body.Children.Add(simBox);

        mainDock.Children.Add(body);
        card.Child = mainDock;
        return card;
    }

    private UIElement BuildPrinterSettings()
    {
        var panel = new StackPanel();

        var saveBtn = ActionButton("Save printer settings", "PrinterSave", primary: true);
        saveBtn.Margin = new Thickness(0);

        var hero = CreatePageHeroHeader(
            string.Empty,
            "Printers & Document Output",
            "Configure workstation printing defaults for queue tickets, weight receipts, and summary slips.",
            Symbol.Print,
            saveBtn);
        panel.Children.Add(hero);

        if (!string.IsNullOrEmpty(_pageMessage))
            panel.Children.Add(Banner("Printer notification", _pageMessage, "#0F766E"));

        // Two-column layout
        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Panel: Active Destination Overview
        var overviewCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var ovDock = new DockPanel();

        var ovHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var ovTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        ovTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.Print,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        ovTitle.Children.Add(new TextBlock { Text = "Active Print Destination", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        ovHeader.Child = ovTitle;
        DockPanel.SetDock(ovHeader, Dock.Top);
        ovDock.Children.Add(ovHeader);

        var ovBody = new StackPanel { Margin = new Thickness(16, 14, 16, 16) };

        var targetBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var tbStack = new StackPanel();
        tbStack.Children.Add(new TextBlock { Text = "Default Target", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)) });
        tbStack.Children.Add(new TextBlock { Text = AppSettings.Current.PrinterName, FontSize = 15, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(0, 2, 0, 4) });
        tbStack.Children.Add(new TextBlock { Text = $"{AppSettings.Current.PrintCopies} copy · {(AppSettings.Current.SilentPrint ? "Silent printing enabled" : "Dialog prompt enabled")}", FontSize = 11.5, Foreground = (Brush)FindResource("MutedBrush") });
        targetBox.Child = tbStack;
        ovBody.Children.Add(targetBox);

        ovBody.Children.Add(new TextBlock
        {
            Text = "Receipt and weight ticket documents are rendered using standard Windows printing services. Thermal receipt printers should be installed with generic text or vendor raster drivers.",
            FontSize = 11.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedBrush"),
            LineHeight = 16
        });

        ovDock.Children.Add(ovBody);
        overviewCard.Child = ovDock;
        Grid.SetColumn(overviewCard, 0);
        body.Children.Add(overviewCard);

        // Right Panel: Printer Configuration Card
        var formCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var formDock = new DockPanel();

        var formHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 12, 18, 12)
        };
        var fhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        fhTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.Document,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        fhTitle.Children.Add(new TextBlock { Text = "Spooler & Output Parameters", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        formHeader.Child = fhTitle;
        DockPanel.SetDock(formHeader, Dock.Top);
        formDock.Children.Add(formHeader);

        var formBody = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

        _printerNameInput = new TextBox { Text = AppSettings.Current.PrinterName, Height = 34, MinHeight = 34, Padding = new Thickness(8, 2, 8, 2) };
        var printerField = (FrameworkElement)Field("Default printer name (Windows Spooler Name)", _printerNameInput);
        printerField.Margin = new Thickness(0, 0, 0, 14);
        formBody.Children.Add(printerField);

        _printerCopiesInput = new TextBox { Text = AppSettings.Current.PrintCopies.ToString(), Height = 34, MinHeight = 34, Padding = new Thickness(8, 2, 8, 2) };
        var copiesField = (FrameworkElement)Field("Default copies per print transaction", _printerCopiesInput);
        copiesField.Margin = new Thickness(0, 0, 0, 16);
        formBody.Children.Add(copiesField);

        var optBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var optStack = new StackPanel();
        optStack.Children.Add(new TextBlock { Text = "Printing Behavior", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 6) });
        _printerSilentInput = new CheckBox
        {
            Content = "Silent print (print directly without dialog prompt when supported)",
            IsChecked = AppSettings.Current.SilentPrint,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };
        optStack.Children.Add(_printerSilentInput);
        optStack.Children.Add(new TextBlock { Text = "Printer preferences are saved locally on this workstation profile.", FontSize = 11, Foreground = (Brush)FindResource("MutedBrush") });
        optBox.Child = optStack;
        formBody.Children.Add(optBox);

        formDock.Children.Add(formBody);
        formCard.Child = formDock;
        Grid.SetColumn(formCard, 2);
        body.Children.Add(formCard);

        panel.Children.Add(body);
        return panel;
    }

    private DataGrid CreateSettingsDataGrid(System.Collections.IEnumerable rows, double minHeight = 200, double maxHeight = double.PositiveInfinity)
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

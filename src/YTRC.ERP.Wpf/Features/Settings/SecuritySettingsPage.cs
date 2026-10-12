using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FluentIcons.Common;
using FluentIcons.Wpf;
using Symbol = FluentIcons.Common.Icon;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private const string SecurityRolesRead = "roles:read";
    private const string SecurityRolesCreate = "roles:create";
    private const string SecurityRolesUpdate = "roles:update";
    private const string SecurityRolesDelete = "roles:delete";
    private const string SecurityCatalogUpdate = "permission_definitions:update";
    private const string SecurityDirectGrant = "access_control:manage";

    private List<SecurityRole> _securityRoles = [];
    private List<SecurityPermission> _securityPermissions = [];
    private List<SecurityUser> _securityUsers = [];
    private List<SecurityOrphanRole> _securityOrphans = [];
    private SecurityRole? _securitySelectedRole;
    private SecurityPermission? _securitySelectedPermission;
    private SecurityUser? _securitySelectedUser;
    private SecurityOrphanRole? _securitySelectedOrphan;
    private bool _securityCreatingRole;
    private bool _securityCreatingPermission;
    private bool _securityLoading;
    private string _securityMessage = "";
    private JsonElement _securityStats;
    private JsonElement _securityHealth;
    private TextBox? _securityRoleNameInput;
    private TextBox? _securityRoleDescriptionInput;
    private CheckBox? _securityRoleActiveInput;
    private readonly List<CheckBox> _securityRolePermissionChecks = [];
    private DataGrid? _securityRoleGrid;
    private DataGrid? _securityPermissionGrid;
    private DataGrid? _securityUserGrid;
    private DataGrid? _securityHealthGrid;
    private TextBox? _securityPermissionModuleInput;
    private TextBox? _securityPermissionActionInput;
    private TextBox? _securityPermissionModuleLabelInput;
    private TextBox? _securityPermissionLabelInput;
    private TextBox? _securityPermissionDescriptionInput;
    private CheckBox? _securityPermissionActiveInput;
    private readonly List<CheckBox> _securityUserPermissionChecks = [];

    private static bool IsSecuritySettingsSection(string section) => section is
        "Security Overview" or "Role Access" or "Permission Catalog" or "User Access" or "Access Health";

    private bool SecurityHasPermission(string code) =>
        _session.User.Role == YTRC.ERP.Domain.Models.UserRole.SystemAdmin ||
        _session.User.RawPermissions.Contains(code, StringComparer.Ordinal);

    private async Task LoadSecuritySettingsAsync()
    {
        if (_session.IsPreview)
        {
            _securityMessage = "Sign in to load security and permission data.";
            RenderCurrentPage();
            return;
        }

        if (!SecurityHasPermission(SecurityRolesRead))
        {
            _securityMessage = "Access denied. This workspace requires roles:read.";
            RenderCurrentPage();
            return;
        }

        _securityLoading = true;
        _securityMessage = "Loading security data from YTRC FastAPI…";
        RenderCurrentPage();
        try
        {
            switch (_settingsSection)
            {
                case "Security Overview":
                    _securityStats = await SecurityGetAsync("/api/access-control/stats");
                    _securityHealth = await SecurityGetAsync("/api/roles/health/orphans");
                    var overviewRolesTask = SecurityGetAsync("/api/roles");
                    var overviewPermissionsTask = SecurityGetAsync("/api/permission-definitions?includeInactive=true");
                    await Task.WhenAll(overviewRolesTask, overviewPermissionsTask);
                    _securityRoles = ReadRoles(overviewRolesTask.Result);
                    _securityPermissions = ReadPermissions(overviewPermissionsTask.Result);
                    break;
                case "Role Access":
                    await LoadSecurityRolesAndPermissionsAsync();
                    break;
                case "Permission Catalog":
                    _securityPermissions = ReadPermissions(await SecurityGetAsync("/api/permission-definitions?includeInactive=true"));
                    break;
                case "User Access":
                    _securityUsers = ReadUsers(await SecurityGetAsync("/api/access-control/users"));
                    _securityPermissions = ReadPermissions(await SecurityGetAsync("/api/permission-definitions?includeInactive=true"));
                    break;
                case "Access Health":
                    _securityHealth = await SecurityGetAsync("/api/roles/health/orphans");
                    _securityOrphans = ReadOrphans(_securityHealth);
                    var usersTask = SecurityGetAsync("/api/access-control/users");
                    var definitionsTask = SecurityGetAsync("/api/permission-definitions?includeInactive=true");
                    await Task.WhenAll(usersTask, definitionsTask);
                    _securityUsers = ReadUsers(usersTask.Result);
                    _securityPermissions = ReadPermissions(definitionsTask.Result);
                    break;
            }
            _securityMessage = "";
        }
        catch (Exception ex)
        {
            _securityMessage = ex.Message;
        }
        finally
        {
            _securityLoading = false;
            if (_currentPage == "Settings" && IsSecuritySettingsSection(_settingsSection)) RenderCurrentPage();
        }
    }

    private async Task LoadSecurityRolesAndPermissionsAsync()
    {
        var rolesTask = SecurityGetAsync("/api/roles");
        var permissionsTask = SecurityGetAsync("/api/permission-definitions?includeInactive=true");
        await Task.WhenAll(rolesTask, permissionsTask);
        var selectedId = _securitySelectedRole?.Id;
        _securityRoles = ReadRoles(rolesTask.Result);
        _securityPermissions = ReadPermissions(permissionsTask.Result);
        _securityCreatingRole = false;
        _securitySelectedRole = _securityRoles.FirstOrDefault(role => role.Id == selectedId) ?? _securityRoles.FirstOrDefault();
    }

    private async Task<JsonElement> SecurityGetAsync(string endpoint)
    {
        var result = await _apiClient.GetAsync(endpoint, _session.Token);
        if (!result.Success) throw new InvalidOperationException(SecurityError(result.Error, result.StatusCode));
        return result.Data.Clone();
    }

    private static string SecurityError(string error, int statusCode)
    {
        if (statusCode == 403) return "The backend denied this action. Check the permission assigned to your role.";
        if (statusCode == 401) return "Your session has expired. Sign in again to continue.";
        return string.IsNullOrWhiteSpace(error) ? $"Request failed (HTTP {statusCode})." : error;
    }

    private UIElement BuildSecuritySettingsPage()
    {
        var page = new Grid();
        page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerStack = new StackPanel();
        headerStack.Children.Add(BuildSecuritySettingsHeader());

        if (!string.IsNullOrWhiteSpace(_securityMessage))
            headerStack.Children.Add(Banner(_securityLoading ? "Loading" : "Security workspace", _securityMessage,
                _securityLoading ? "#297691" : "#DC2626"));

        Grid.SetRow(headerStack, 0);
        page.Children.Add(headerStack);

        if (_session.IsPreview)
        {
            var p = Banner("Sign in required", "Security administration uses authenticated YTRC FastAPI endpoints.", "#D97706");
            Grid.SetRow(p, 1);
            page.Children.Add(p);
            return page;
        }
        if (!SecurityHasPermission(SecurityRolesRead))
        {
            var p = Banner("Access restricted", "This workspace requires roles:read. The backend remains the authority for every operation.", "#DC2626");
            Grid.SetRow(p, 1);
            page.Children.Add(p);
            return page;
        }

        if (_securityLoading) return page;

        UIElement content = _settingsSection switch
        {
            "Role Access" => BuildSecurityRolesPage(),
            "Permission Catalog" => BuildPermissionCatalogPage(),
            "User Access" => BuildUserAccessPage(),
            "Access Health" => BuildAccessHealthPage(),
            _ => new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = BuildSecurityOverviewPage() }
        };
        Grid.SetRow(content, 1);
        page.Children.Add(content);
        return page;
    }

    private UIElement BuildSecuritySettingsHeader()
    {
        var card = new Border
        {
            Tag = StickyPageHeaderTag,
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = new Thickness(16, 14, 16, 12),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var mainStack = new StackPanel();

        // Top Row: Mint Icon + Title/Subtitle on Left, Action Buttons on Right
        var topDock = new DockPanel();

        var actions = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            ItemHeight = 34
        };

        var backend = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"Backend · {AppSettings.Current.ApiBaseUrl}",
                FontSize = 11,
                Foreground = (Brush)FindResource("MutedBrush"),
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        actions.Children.Add(backend);

        var refresh = _securityLoading
            ? new Button { Content = "…", IsEnabled = false, Height = 34, Width = 36, Padding = new Thickness(0), ToolTip = "Loading" }
            : IconOnlyActionButton("Refresh security data", FluentIcons.Common.Icon.ArrowClockwise, new Thickness(0, 0, 8, 0));
        refresh.IsEnabled = !_securityLoading && !_session.IsPreview && SecurityHasPermission(SecurityRolesRead);
        refresh.Click += async (_, _) => await LoadSecuritySettingsAsync();
        actions.Children.Add(refresh);

        if (!_session.IsPreview)
        {
            switch (_settingsSection)
            {
                case "Role Access":
                    var newRole = SecurityHeaderAction("New role", primary: true);
                    newRole.IsEnabled = SecurityHasPermission(SecurityRolesCreate);
                    newRole.Click += (_, _) =>
                    {
                        _securityCreatingRole = true;
                        _securitySelectedRole = new SecurityRole("", "", "", [], true, 0);
                        RenderCurrentPage();
                    };
                    actions.Children.Add(newRole);
                    var saveRole = SecurityHeaderAction("Save role", primary: true);
                    saveRole.IsEnabled = _securityCreatingRole ? SecurityHasPermission(SecurityRolesCreate) : SecurityHasPermission(SecurityRolesUpdate);
                    saveRole.Click += async (_, _) => await SaveSecurityRoleAsync();
                    actions.Children.Add(saveRole);
                    var deleteRole = IconOnlyActionButton("Delete role", FluentIcons.Common.Icon.Delete, new Thickness(0, 0, 0, 0));
                    deleteRole.IsEnabled = !_securityCreatingRole && _securitySelectedRole is { UserCount: 0 } && SecurityHasPermission(SecurityRolesDelete);
                    deleteRole.Click += async (_, _) => await DeleteSecurityRoleAsync();
                    actions.Children.Add(deleteRole);
                    break;

                case "Permission Catalog":
                    var addDefinition = SecurityHeaderAction("Add definition", primary: true);
                    addDefinition.IsEnabled = SecurityHasPermission(SecurityCatalogUpdate);
                    addDefinition.Click += (_, _) =>
                    {
                        _securityCreatingPermission = true;
                        _securitySelectedPermission = new SecurityPermission("", "", "", "", "", "", 0, true);
                        RenderCurrentPage();
                    };
                    actions.Children.Add(addDefinition);
                    var saveDefinition = SecurityHeaderAction("Save definition");
                    saveDefinition.IsEnabled = SecurityHasPermission(SecurityCatalogUpdate);
                    saveDefinition.Click += async (_, _) => await SavePermissionDefinitionAsync();
                    actions.Children.Add(saveDefinition);
                    break;

                case "User Access":
                    var saveGrants = SecurityHeaderAction("Save direct grants", primary: true);
                    saveGrants.IsEnabled = SecurityHasPermission(SecurityDirectGrant);
                    saveGrants.Click += async (_, _) => await SaveUserDirectPermissionsAsync();
                    actions.Children.Add(saveGrants);
                    break;
            }
        }

        DockPanel.SetDock(actions, Dock.Right);
        topDock.Children.Add(actions);

        var leftContent = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = UiTokens.HeroIconSize,
            Height = UiTokens.HeroIconSize,
            CornerRadius = UiTokens.Radii.Medium,
            Background = UiTokens.Brushes.MintSurface,
            BorderBrush = UiTokens.Brushes.MintBorder,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FluentIcon
            {
                Icon = FluentIcons.Common.Icon.Shield,
                IconSize = IconSize.Size20,
                IconVariant = IconVariant.Regular,
                Foreground = UiTokens.Brushes.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        leftContent.Children.Add(iconBox);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = "Security & Access Control",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = UiTokens.Brushes.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });
        leftContent.Children.Add(titleStack);
        topDock.Children.Add(leftContent);

        mainStack.Children.Add(topDock);

        // Sub-tabs Row
        var tabRow = new WrapPanel { Margin = new Thickness(0, 12, 0, 2) };
        (string Key, string Label)[] tabs =
        [
            ("Security Overview", "Overview"),
            ("Role Access", "Roles & Permissions"),
            ("Permission Catalog", "Permission Catalog"),
            ("User Access", "User Direct Access"),
            ("Access Health", "Access Health")
        ];
        foreach (var tab in tabs)
        {
            var isCurrent = string.Equals(_settingsSection, tab.Key, StringComparison.OrdinalIgnoreCase);
            var btn = new Button
            {
                Content = tab.Label,
                Height = 32,
                Margin = new Thickness(0, 0, 6, 4),
                Padding = new Thickness(14, 0, 14, 0),
                Background = isCurrent ? (Brush)FindResource("ControlPressedBackground") : Brushes.White,
                BorderBrush = isCurrent ? (Brush)FindResource("ControlPressedBorder") : (Brush)FindResource("BorderBrushSoft"),
                Foreground = isCurrent ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextBrush"),
                FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btn.Click += (_, _) => NavigateToSettings(tab.Key);
            tabRow.Children.Add(btn);
        }
        mainStack.Children.Add(tabRow);

        card.Child = mainStack;
        return card;
    }

    private Button SecurityHeaderAction(string text, bool primary = false)
        => new()
        {
            Content = text,
            Height = 34,
            MinWidth = 90,
            Padding = new Thickness(11, 1, 11, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)FindResource(primary ? "PrimaryButton" : typeof(Button))
        };

    private UIElement BuildSecurityOverviewPage()
    {
        var content = new StackPanel();

        // Top 4 Metric Cards
        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(-5, 0, -5, 12) };
        metrics.Children.Add(SecurityMetric("Defined Roles", SecurityNestedInt(_securityStats, "roles", "total"), $"Active: {SecurityNestedInt(_securityStats, "roles", "active")}", Symbol.Key, "#0F766E"));
        metrics.Children.Add(SecurityMetric("Permission Catalog", SecurityNestedInt(_securityStats, "permissions", "total"), "Active API definitions", Symbol.DocumentBulletList, "#0284C7"));
        metrics.Children.Add(SecurityMetric("Direct Overrides", SecurityJsonInt(_securityStats, "usersWithOverrides"), "Users with personal grants", Symbol.Person, "#D97706"));
        metrics.Children.Add(SecurityMetric("Audit Health", SecurityJsonInt(_securityHealth, "rolesWithOrphans"), SecurityJsonString(_securityHealth, "health", "Unknown"), Symbol.HeartPulse, "#7C3AED"));
        content.Children.Add(metrics);

        var highRiskCodes = new HashSet<string>(["users:delete", "roles:delete", "access_control:manage", "permission_definitions:update", "workflow:manage"], StringComparer.Ordinal);
        var broadlyGrantedRoles = _securityRoles.Where(role => !role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase) &&
            role.Permissions.Any(highRiskCodes.Contains)).Select(role => role.Name).ToList();
        if (broadlyGrantedRoles.Count > 0)
            content.Children.Add(Banner("Review elevated role grants", $"{broadlyGrantedRoles.Count} non-Administrator role(s) contain administrative rights (user/role deletion, permission catalog, or access management): {string.Join(", ", broadlyGrantedRoles)}. Review and narrow these grants for principle of least privilege.", "#DC2626"));

        content.Children.Add(SectionHeading("Security Architecture & Access Evaluation"));

        // 3 Modern Architecture Guide Cards
        var guideGrid = new UniformGrid { Columns = 3, Margin = new Thickness(-6, 0, -6, 16) };
        guideGrid.Children.Add(SecurityGuideCard("Role-Based Access Control",
            "The backend evaluates exact module:action codes against protected API routes. Updates to role definitions take effect immediately upon subsequent requests.",
            Symbol.Shield, "#0F766E"));
        guideGrid.Children.Add(SecurityGuideCard("Direct User Overrides",
            "Direct personal grants are additive to role permissions. They serve as documented exceptions for unique operational needs without creating one-off roles.",
            Symbol.PersonKey, "#0284C7"));
        guideGrid.Children.Add(SecurityGuideCard("Catalog & Lifecycle",
            "Deactivating a permission definition locks it from new assignments. Existing grants must be audited and retired via Role Access and Access Health checks.",
            Symbol.DocumentBulletList, "#D97706"));
        content.Children.Add(guideGrid);

        // Quick Navigation Tiles
        content.Children.Add(SectionHeading("Quick Navigation"));
        var navPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        navPanel.Children.Add(SecurityNavButton("Roles & Permissions Studio", "Role Access", Symbol.Key));
        navPanel.Children.Add(SecurityNavButton("Permission Definitions Catalog", "Permission Catalog", Symbol.DocumentBulletList));
        navPanel.Children.Add(SecurityNavButton("User Direct Grants", "User Access", Symbol.Person));
        navPanel.Children.Add(SecurityNavButton("Access Health & Orphan Audit", "Access Health", Symbol.HeartPulse));
        content.Children.Add(navPanel);

        return content;
    }

    private UIElement SecurityMetric(string title, int value, string detail, Symbol icon, string accentHex)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(5),
            ClipToBounds = true
        };

        var dock = new DockPanel();

        var iconBox = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)!) { Opacity = 0.12 },
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        iconBox.Child = new FluentIcon
        {
            Icon = icon,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)!),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(iconBox, Dock.Left);
        dock.Children.Add(iconBox);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(new TextBlock { Text = title, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("MutedBrush") });
        textStack.Children.Add(new TextBlock { Text = value.ToString(), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(0, 1, 0, 1) });
        textStack.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = (Brush)FindResource("MutedBrush") });
        dock.Children.Add(textStack);

        card.Child = dock;
        return card;
    }

    private UIElement SecurityGuideCard(string title, string description, Symbol icon, string accentHex)
    {
        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(6)
        };

        var stack = new StackPanel();
        var headRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), VerticalAlignment = VerticalAlignment.Center };
        headRow.Children.Add(new FluentIcon
        {
            Icon = icon,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex)!),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        headRow.Children.Add(new TextBlock { Text = title, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        stack.Children.Add(headRow);

        stack.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16
        });

        card.Child = stack;
        return card;
    }

    private Button SecurityNavButton(string label, string section, Symbol icon)
    {
        var button = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new FluentIcon { Icon = icon, IconSize = IconSize.Size20, IconVariant = IconVariant.Regular, Width = 16, Height = 16, FontSize = 16, Margin = new Thickness(0, 0, 6, 0), Foreground = (Brush)FindResource("AccentBrush") },
                    new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Margin = new Thickness(0, 2, 8, 4),
            Padding = new Thickness(12, 6, 12, 6),
            Height = 34,
            Style = (Style)FindResource(typeof(Button))
        };
        button.Click += (_, _) => NavigateToSettings(section);
        return button;
    }

    private UIElement BuildSecurityRolesPage()
    {
        var layout = new Grid { MinHeight = 620, Margin = new Thickness(0, 0, 0, 16) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(350) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Panel: Roles Directory Card
        var directoryCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };

        var dirDock = new DockPanel();

        // Card Header
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };
        DockPanel.SetDock(headerBorder, Dock.Top);

        var headerDock = new DockPanel();
        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new FluentIcon
        {
            Icon = Symbol.PeopleTeam,
            FontSize = 16,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        titleStack.Children.Add(new TextBlock
        {
            Text = "Defined Roles",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var countBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"{_securityRoles.Count}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110))
            }
        };
        titleStack.Children.Add(countBadge);
        headerDock.Children.Add(titleStack);

        var newRoleBtn = new Button
        {
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            Style = (Style)FindResource("PrimaryButton"),
            IsEnabled = SecurityHasPermission(SecurityRolesCreate),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var newRoleBtnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        newRoleBtnStack.Children.Add(new FluentIcon
        {
            Icon = Symbol.Add,
            FontSize = 13,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        newRoleBtnStack.Children.Add(new TextBlock
        {
            Text = "New role",
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        newRoleBtn.Content = newRoleBtnStack;
        newRoleBtn.Click += (_, _) =>
        {
            _securityCreatingRole = true;
            _securitySelectedRole = new SecurityRole("", "", "", [], true, 0);
            RenderCurrentPage();
        };
        DockPanel.SetDock(newRoleBtn, Dock.Right);
        headerDock.Children.Add(newRoleBtn);
        headerBorder.Child = headerDock;
        dirDock.Children.Add(headerBorder);

        // Search Filter Box
        var searchBoxBorder = new Border
        {
            Margin = new Thickness(12, 10, 12, 8),
            Height = 32,
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            BorderBrush = new SolidColorBrush(Color.FromRgb(216, 224, 234)),
            BorderThickness = new Thickness(1),
            Background = Brushes.White
        };
        DockPanel.SetDock(searchBoxBorder, Dock.Top);

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var searchIcon = new FluentIcon
        {
            Icon = Symbol.Search,
            FontSize = 13,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(8, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(searchIcon, 0);
        searchGrid.Children.Add(searchIcon);

        var searchInput = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 8, 2)
        };
        Grid.SetColumn(searchInput, 1);
        searchGrid.Children.Add(searchInput);
        searchBoxBorder.Child = searchGrid;
        dirDock.Children.Add(searchBoxBorder);

        // Footer helper
        var footerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(12, 6, 12, 6)
        };
        DockPanel.SetDock(footerBorder, Dock.Bottom);
        footerBorder.Child = new TextBlock
        {
            Text = "Select a role to inspect or update grants.",
            FontSize = 11,
            Foreground = (Brush)FindResource("MutedBrush")
        };
        dirDock.Children.Add(footerBorder);

        // Roles DataGrid
        var roleGrid = CreateSettingsDataGrid(_securityRoles, minHeight: 460, maxHeight: 680);
        roleGrid.BorderThickness = new Thickness(0, 1, 0, 0);
        roleGrid.VerticalAlignment = VerticalAlignment.Stretch;

        var roleNameStyle = new Style(typeof(TextBlock));
        roleNameStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        roleNameStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        roleNameStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(8, 0, 4, 0)));
        roleNameStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, (Brush)FindResource("TextBrush")));

        roleGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Role",
            Binding = new Binding(nameof(SecurityRole.Name)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = roleNameStyle
        });
        roleGrid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Users",
            Width = 65,
            CellTemplate = CreateBadgeDataTemplate(nameof(SecurityRole.UserCount), "#F1F5F9", "#475569")
        });
        roleGrid.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Grants",
            Width = 70,
            CellTemplate = CreateBadgeDataTemplate(nameof(SecurityRole.PermissionCount), "#F0FDFA", "#0F766E")
        });

        roleGrid.SelectedItem = _securityCreatingRole
            ? null
            : (_securityRoles.FirstOrDefault(x => x.Id == _securitySelectedRole?.Id) ?? _securityRoles.FirstOrDefault());

        roleGrid.SelectionChanged += (_, _) =>
        {
            if (roleGrid.SelectedItem is SecurityRole selected && selected.Id != _securitySelectedRole?.Id)
            {
                _securityCreatingRole = false;
                _securitySelectedRole = selected;
                RenderCurrentPage();
            }
        };

        searchInput.TextChanged += (_, _) =>
        {
            var q = searchInput.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q))
            {
                roleGrid.ItemsSource = _securityRoles;
            }
            else
            {
                roleGrid.ItemsSource = _securityRoles.Where(r =>
                    r.Name.ToLowerInvariant().Contains(q) ||
                    r.Description.ToLowerInvariant().Contains(q) ||
                    r.Id.ToLowerInvariant().Contains(q)).ToList();
            }
        };

        _securityRoleGrid = roleGrid;
        dirDock.Children.Add(roleGrid);
        directoryCard.Child = dirDock;

        Grid.SetColumn(directoryCard, 0);
        layout.Children.Add(directoryCard);

        // Right Panel: Role Configuration Studio Card
        var editor = BuildSecurityRoleEditor(_securitySelectedRole);
        Grid.SetColumn(editor, 2);
        layout.Children.Add(editor);

        return layout;
    }

    private UIElement BuildSecurityRoleEditor(SecurityRole? role)
    {
        if (role is null)
        {
            return EmptyState("Choose a role", "Select a role from the directory on the left or click '+ New role' to create one.");
        }

        _securityRolePermissionChecks.Clear();

        var editorCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };

        var mainDock = new DockPanel();

        // 1. Studio Header (Dock.Top)
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 14, 18, 14)
        };
        DockPanel.SetDock(headerBorder, Dock.Top);

        var headerDock = new DockPanel();

        // Left of Header: Icon + Title + Status badge + Subtitle
        var infoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            Child = new FluentIcon
            {
                Icon = Symbol.ShieldKeyhole,
                FontSize = 20,
                Foreground = (Brush)FindResource("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        infoStack.Children.Add(iconBox);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(new TextBlock
        {
            Text = _securityCreatingRole ? "Create New Role" : role.Name,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextBrush"),
            Margin = new Thickness(0, 0, 8, 0)
        });

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2, 8, 2),
            Background = role.IsActive
                ? new SolidColorBrush(Color.FromRgb(236, 253, 245))
                : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
            BorderBrush = role.IsActive
                ? new SolidColorBrush(Color.FromRgb(167, 243, 208))
                : new SolidColorBrush(Color.FromRgb(229, 231, 235)),
            Child = new TextBlock
            {
                Text = role.IsActive ? "● Active" : "● Inactive",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = role.IsActive
                    ? new SolidColorBrush(Color.FromRgb(5, 150, 105))
                    : new SolidColorBrush(Color.FromRgb(107, 114, 128))
            }
        };
        titleRow.Children.Add(statusBadge);
        titleStack.Children.Add(titleRow);

        var subtitleText = new TextBlock
        {
            Text = _securityCreatingRole
                ? "Configure role identity and assign module permissions below."
                : $"Role ID: {role.Id}  ·  {role.UserCount} user(s) assigned  ·  {role.Permissions.Count} granted code(s)",
            FontSize = 11.5,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 2, 0, 0)
        };
        titleStack.Children.Add(subtitleText);
        infoStack.Children.Add(titleStack);
        headerDock.Children.Add(infoStack);

        // Right of Header: Contextual Actions
        var actionsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(actionsStack, Dock.Right);

        if (!_securityCreatingRole)
        {
            var deleteBtn = new Button
            {
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 0, 12, 0),
                Style = (Style)FindResource(typeof(Button)),
                IsEnabled = role.UserCount == 0 && SecurityHasPermission(SecurityRolesDelete),
                ToolTip = role.UserCount > 0 ? "Cannot delete role while users are assigned." : "Delete role"
            };
            var delStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            delStack.Children.Add(new FluentIcon
            {
                Icon = Symbol.Delete,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            delStack.Children.Add(new TextBlock
            {
                Text = "Delete",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                VerticalAlignment = VerticalAlignment.Center
            });
            deleteBtn.Content = delStack;
            deleteBtn.Click += async (_, _) => await DeleteSecurityRoleAsync();
            actionsStack.Children.Add(deleteBtn);
        }

        var saveBtn = new Button
        {
            Height = 34,
            Padding = new Thickness(16, 0, 16, 0),
            Style = (Style)FindResource("PrimaryButton"),
            IsEnabled = _securityCreatingRole ? SecurityHasPermission(SecurityRolesCreate) : SecurityHasPermission(SecurityRolesUpdate),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var saveStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        saveStack.Children.Add(new FluentIcon
        {
            Icon = Symbol.Save,
            FontSize = 14,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        saveStack.Children.Add(new TextBlock
        {
            Text = _securityCreatingRole ? "Create role" : "Save role",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });
        saveBtn.Content = saveStack;
        saveBtn.Click += async (_, _) => await SaveSecurityRoleAsync();
        actionsStack.Children.Add(saveBtn);

        headerDock.Children.Add(actionsStack);
        headerBorder.Child = headerDock;
        mainDock.Children.Add(headerBorder);

        // 2. Form Body (Scrollable)
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 650
        };

        var bodyPanel = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

        // General Information Card
        var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftForm = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        _securityRoleNameInput = new TextBox
        {
            Text = role.Name,
            Height = 32,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 4, 0, 10)
        };
        leftForm.Children.Add(Field("Role name *", _securityRoleNameInput));

        _securityRoleDescriptionInput = new TextBox
        {
            Text = role.Description,
            Height = 32,
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 4, 0, 0)
        };
        leftForm.Children.Add(Field("Description", _securityRoleDescriptionInput));
        Grid.SetColumn(leftForm, 0);
        formGrid.Children.Add(leftForm);

        var rightBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(12, 10, 12, 10),
            VerticalAlignment = VerticalAlignment.Top
        };
        var rightStack = new StackPanel();
        rightStack.Children.Add(new TextBlock
        {
            Text = "Assignment State",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            Margin = new Thickness(0, 0, 0, 6)
        });
        _securityRoleActiveInput = new CheckBox
        {
            Content = "Role is active",
            IsChecked = role.IsActive,
            Margin = new Thickness(0, 0, 0, 4),
            IsEnabled = SecurityHasPermission(_securityCreatingRole ? SecurityRolesCreate : SecurityRolesUpdate)
        };
        rightStack.Children.Add(_securityRoleActiveInput);
        rightStack.Children.Add(new TextBlock
        {
            Text = "Inactive roles cannot be assigned to new users or used for API authorization.",
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("MutedBrush")
        });
        rightBox.Child = rightStack;
        Grid.SetColumn(rightBox, 1);
        formGrid.Children.Add(rightBox);
        bodyPanel.Children.Add(formGrid);

        // Unknown / Inactive Grants Banner
        var inactiveCount = role.Permissions.Count(code => !_securityPermissions.Any(p => p.Code == code && p.IsActive));
        if (inactiveCount > 0)
        {
            bodyPanel.Children.Add(Banner("Inactive or unknown grants detected",
                $"This role references {inactiveCount} code(s) missing or inactive in the current catalog. Saving will clean up these invalid codes.",
                "#D97706"));
        }

        // Divider
        bodyPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            Margin = new Thickness(0, 4, 0, 14)
        });

        // Permission Grants Header with Actions
        var grantsHeader = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grantsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grantsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var grantsTitleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        grantsTitleStack.Children.Add(new FluentIcon
        {
            Icon = Symbol.LockClosed,
            FontSize = 15,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        grantsTitleStack.Children.Add(new TextBlock
        {
            Text = "Permission Grants",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });

        var activeCountText = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110))
        };
        var grantsCountBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = activeCountText
        };
        grantsTitleStack.Children.Add(grantsCountBadge);
        Grid.SetColumn(grantsTitleStack, 0);
        grantsHeader.Children.Add(grantsTitleStack);

        var permActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var filterInput = new TextBox
        {
            Height = 28,
            Width = 160,
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        permActions.Children.Add(FieldCompact("Filter:", filterInput));

        var canEditPerms = SecurityHasPermission(_securityCreatingRole ? SecurityRolesCreate : SecurityRolesUpdate);

        var selectAllBtn = new Button
        {
            Content = "Select All",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 11.5,
            Margin = new Thickness(0, 0, 6, 0),
            IsEnabled = canEditPerms,
            VerticalAlignment = VerticalAlignment.Center
        };
        permActions.Children.Add(selectAllBtn);

        var clearAllBtn = new Button
        {
            Content = "Clear All",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            FontSize = 11.5,
            IsEnabled = canEditPerms,
            VerticalAlignment = VerticalAlignment.Center
        };
        permActions.Children.Add(clearAllBtn);

        Grid.SetColumn(permActions, 1);
        grantsHeader.Children.Add(permActions);
        bodyPanel.Children.Add(grantsHeader);

        void UpdateSummary()
        {
            var granted = _securityRolePermissionChecks.Count(c => c.IsChecked == true && c.IsEnabled);
            var total = _securityPermissions.Count(p => p.IsActive);
            activeCountText.Text = $"{granted} of {total} active grants";
        }

        selectAllBtn.Click += (_, _) =>
        {
            foreach (var cb in _securityRolePermissionChecks.Where(c => c.IsEnabled))
                cb.IsChecked = true;
            UpdateSummary();
        };

        clearAllBtn.Click += (_, _) =>
        {
            foreach (var cb in _securityRolePermissionChecks.Where(c => c.IsEnabled))
                cb.IsChecked = false;
            UpdateSummary();
        };

        var permGroups = BuildPermissionGroups(
            _securityPermissions,
            role.Permissions.ToHashSet(StringComparer.Ordinal),
            canEditPerms,
            _securityRolePermissionChecks,
            UpdateSummary,
            filterInput);

        bodyPanel.Children.Add(permGroups);
        UpdateSummary();

        scroll.Content = bodyPanel;
        mainDock.Children.Add(scroll);
        editorCard.Child = mainDock;

        return editorCard;
    }

    private async Task SaveSecurityRoleAsync()
    {
        var role = _securitySelectedRole;
        var name = _securityRoleNameInput?.Text.Trim() ?? "";
        if (role is null || name.Length == 0)
        {
            _securityMessage = "Enter a role name before saving.";
            RenderCurrentPage();
            return;
        }
        var create = _securityCreatingRole;
        if (!SecurityHasPermission(create ? SecurityRolesCreate : SecurityRolesUpdate)) return;
        var permissions = _securityRolePermissionChecks.Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var payload = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["description"] = _securityRoleDescriptionInput?.Text.Trim(),
            ["permissions"] = permissions,
            ["isActive"] = _securityRoleActiveInput?.IsChecked == true
        };
        try
        {
            var result = create
                ? await _apiClient.PostJsonAsync("/api/roles", payload, _session.Token)
                : await _apiClient.PatchJsonAsync($"/api/roles/{Uri.EscapeDataString(role.Id)}", payload, _session.Token);
            if (!result.Success) throw new InvalidOperationException(SecurityError(result.Error, result.StatusCode));
            var newId = result.Data is JsonElement data ? SecurityJsonString(data, "id", role.Id) : role.Id;
            _securityMessage = create ? "Role created." : "Role saved.";
            await LoadSecurityRolesAndPermissionsAsync();
            _securitySelectedRole = _securityRoles.FirstOrDefault(x => x.Id == newId) ?? _securityRoles.FirstOrDefault(x => x.Name == name);
            _securityCreatingRole = false;
        }
        catch (Exception ex) { _securityMessage = ex.Message; }
        RenderCurrentPage();
    }

    private async Task DeleteSecurityRoleAsync()
    {
        if (_securitySelectedRole is not { UserCount: 0 } role || !SecurityHasPermission(SecurityRolesDelete)) return;
        if (MessageBox.Show($"Delete role '{role.Name}'? This cannot be undone.", "Delete role", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var result = await _apiClient.DeleteAsync($"/api/roles/{Uri.EscapeDataString(role.Id)}", _session.Token);
        _securityMessage = result.Success ? "Role deleted." : SecurityError(result.Error, result.StatusCode);
        if (result.Success) await LoadSecurityRolesAndPermissionsAsync();
        RenderCurrentPage();
    }

    private StackPanel BuildPermissionGroups(
        IReadOnlyList<SecurityPermission> all,
        HashSet<string> selected,
        bool canEdit,
        List<CheckBox> checks,
        Action? onGrantsChanged = null,
        TextBox? filterInput = null)
    {
        var root = new StackPanel();
        var filterContainers = new List<(Expander Exp, string ModuleName, List<(Border Card, CheckBox Box, SecurityPermission Perm)> Items)>();

        foreach (var group in all.GroupBy(p => string.IsNullOrWhiteSpace(p.ModuleLabel) ? p.Module : p.ModuleLabel)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var groupItems = new List<(Border Card, CheckBox Box, SecurityPermission Perm)>();
            var groupChecks = new List<CheckBox>();

            var wrap = new WrapPanel { Margin = new Thickness(6, 6, 6, 8) };

            foreach (var permission in group.OrderBy(p => p.SortOrder).ThenBy(p => p.Code, StringComparer.Ordinal))
            {
                var labelText = string.IsNullOrWhiteSpace(permission.Label) ? permission.Action : permission.Label;
                var textStack = new StackPanel();
                textStack.Children.Add(new TextBlock
                {
                    Text = labelText,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("TextBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = permission.Code,
                    FontSize = 10.5,
                    Foreground = (Brush)FindResource("MutedBrush"),
                    Margin = new Thickness(0, 1, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                var checkbox = new CheckBox
                {
                    Content = textStack,
                    Tag = permission.Code,
                    IsChecked = permission.IsActive && selected.Contains(permission.Code),
                    IsEnabled = canEdit && permission.IsActive,
                    ToolTip = permission.Description ?? permission.Code,
                    VerticalAlignment = VerticalAlignment.Center
                };

                checks.Add(checkbox);
                groupChecks.Add(checkbox);

                var itemCard = new Border
                {
                    Width = 280,
                    Margin = new Thickness(3),
                    Padding = new Thickness(8, 6, 8, 6),
                    Background = new SolidColorBrush(Color.FromRgb(249, 250, 251)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Child = checkbox
                };

                wrap.Children.Add(itemCard);
                groupItems.Add((itemCard, checkbox, permission));
            }

            // Group Expander Header
            var headerDock = new DockPanel();
            var titleText = new TextBlock
            {
                Text = group.Key,
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            headerDock.Children.Add(titleText);

            var headerActions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(headerActions, Dock.Right);

            var badgeText = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold };
            var badgeBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = badgeText
            };

            void UpdateGroupBadge()
            {
                var granted = groupChecks.Count(c => c.IsChecked == true);
                var total = groupChecks.Count;
                badgeText.Text = $"{granted} / {total} granted";

                if (granted == total && total > 0)
                {
                    badgeBorder.Background = new SolidColorBrush(Color.FromRgb(236, 253, 245));
                    badgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208));
                    badgeText.Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                }
                else if (granted > 0)
                {
                    badgeBorder.Background = new SolidColorBrush(Color.FromRgb(240, 253, 250));
                    badgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241));
                    badgeText.Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110));
                }
                else
                {
                    badgeBorder.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));
                    badgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    badgeText.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }
            }

            foreach (var cb in groupChecks)
            {
                cb.Checked += (_, _) => { UpdateGroupBadge(); onGrantsChanged?.Invoke(); };
                cb.Unchecked += (_, _) => { UpdateGroupBadge(); onGrantsChanged?.Invoke(); };
            }

            UpdateGroupBadge();
            headerActions.Children.Add(badgeBorder);

            if (canEdit)
            {
                var toggleBtn = new Button
                {
                    Content = "Toggle all",
                    Height = 24,
                    Padding = new Thickness(6, 0, 6, 0),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                toggleBtn.Click += (_, _) =>
                {
                    var allChecked = groupChecks.All(c => c.IsChecked == true);
                    foreach (var c in groupChecks.Where(c => c.IsEnabled))
                        c.IsChecked = !allChecked;
                    UpdateGroupBadge();
                    onGrantsChanged?.Invoke();
                };
                headerActions.Children.Add(toggleBtn);
            }

            headerDock.Children.Add(headerActions);

            var expander = new Expander
            {
                Header = headerDock,
                Content = wrap,
                IsExpanded = true,
                Margin = new Thickness(0, 3, 0, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                FontWeight = FontWeights.Normal
            };

            root.Children.Add(expander);
            filterContainers.Add((expander, group.Key, groupItems));
        }

        if (filterInput != null)
        {
            filterInput.TextChanged += (_, _) =>
            {
                var q = filterInput.Text.Trim().ToLowerInvariant();
                foreach (var container in filterContainers)
                {
                    var anyMatch = false;
                    foreach (var (card, _, perm) in container.Items)
                    {
                        var match = string.IsNullOrEmpty(q) ||
                                    perm.Label.ToLowerInvariant().Contains(q) ||
                                    perm.Code.ToLowerInvariant().Contains(q) ||
                                    perm.Action.ToLowerInvariant().Contains(q) ||
                                    container.ModuleName.ToLowerInvariant().Contains(q);
                        card.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                        if (match) anyMatch = true;
                    }
                    container.Exp.Visibility = anyMatch ? Visibility.Visible : Visibility.Collapsed;
                    if (!string.IsNullOrEmpty(q) && anyMatch)
                        container.Exp.IsExpanded = true;
                }
            };
        }

        return root;
    }

    private static DataTemplate CreateBadgeDataTemplate(string propertyName, string bgHex, string fgHex)
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.BackgroundProperty, (Brush)new BrushConverter().ConvertFromString(bgHex)!);
        border.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
        border.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        border.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetBinding(TextBlock.TextProperty, new Binding(propertyName));
        text.SetValue(TextBlock.FontSizeProperty, 11.5);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.ForegroundProperty, (Brush)new BrushConverter().ConvertFromString(fgHex)!);
        text.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);

        border.AppendChild(text);
        return new DataTemplate { VisualTree = border };
    }

    private UIElement BuildPermissionCatalogPage()
    {
        var outer = new StackPanel();

        var layout = new Grid { MinHeight = 600, Margin = new Thickness(0, 0, 0, 16) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Panel: Catalog Directory Card
        var directoryCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var dirDock = new DockPanel();

        var dirHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var dhDock = new DockPanel();
        var dhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        dhTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.DocumentBulletList,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        dhTitle.Children.Add(new TextBlock { Text = "Permission Definitions", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        dhDock.Children.Add(dhTitle);

        var countBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        countBadge.Child = new TextBlock { Text = $"{_securityPermissions.Count}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)) };
        DockPanel.SetDock(countBadge, Dock.Right);
        dhDock.Children.Add(countBadge);
        dirHeader.Child = dhDock;
        DockPanel.SetDock(dirHeader, Dock.Top);
        dirDock.Children.Add(dirHeader);

        var grid = CreateSettingsDataGrid(_securityPermissions, minHeight: 450, maxHeight: 680);
        grid.Columns.Add(new DataGridTextColumn { Header = "Module", Binding = new Binding(nameof(SecurityPermission.ModuleDisplay)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Code", Binding = new Binding(nameof(SecurityPermission.Code)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Label", Binding = new Binding(nameof(SecurityPermission.Label)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding(nameof(SecurityPermission.Status)), Width = 70 });
        grid.SelectedItem = _securityCreatingPermission ? null : _securityPermissions.FirstOrDefault(x => x.Id == _securitySelectedPermission?.Id);
        grid.SelectionChanged += (_, _) =>
        {
            if (grid.SelectedItem is SecurityPermission selected && selected.Id != _securitySelectedPermission?.Id)
            {
                _securityCreatingPermission = false;
                _securitySelectedPermission = selected;
                RenderCurrentPage();
            }
        };
        _securityPermissionGrid = grid;
        dirDock.Children.Add(grid);
        directoryCard.Child = dirDock;
        Grid.SetColumn(directoryCard, 0);
        layout.Children.Add(directoryCard);

        var editor = BuildPermissionDefinitionEditor(_securitySelectedPermission);
        Grid.SetColumn(editor, 2);
        layout.Children.Add(editor);

        outer.Children.Add(layout);
        return outer;
    }

    private UIElement BuildPermissionDefinitionEditor(SecurityPermission? permission)
    {
        if (permission is null) return EmptyState("Choose a permission definition", "Select a definition from the directory to review, update labels or toggle active state.");

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var mainDock = new DockPanel();

        // Header
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 12, 18, 12)
        };
        var headDock = new DockPanel();

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBox.Child = new FluentIcon { Icon = Symbol.Key, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(iconBox);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(new TextBlock { Text = _securityCreatingPermission ? "New Permission Definition" : permission.Code, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });

        var statusBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var isAct = permission.IsActive;
        statusBadge.Background = new SolidColorBrush(isAct ? Color.FromRgb(236, 253, 245) : Color.FromRgb(241, 245, 249));
        statusBadge.BorderBrush = new SolidColorBrush(isAct ? Color.FromRgb(167, 243, 208) : Color.FromRgb(226, 232, 240));
        statusBadge.Child = new TextBlock { Text = isAct ? "• Active" : "• Inactive", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(isAct ? Color.FromRgb(5, 150, 105) : Color.FromRgb(100, 116, 139)) };
        titleRow.Children.Add(statusBadge);
        textStack.Children.Add(titleRow);
        textStack.Children.Add(new TextBlock { Text = $"Module: {permission.ModuleDisplay} · Action: {permission.Action}", FontSize = 11.5, Foreground = (Brush)FindResource("MutedBrush") });
        titleStack.Children.Add(textStack);
        headDock.Children.Add(titleStack);

        var saveBtn = new Button
        {
            Content = "Save definition",
            Style = (Style)FindResource("PrimaryButton"),
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = SecurityHasPermission(SecurityCatalogUpdate)
        };
        saveBtn.Click += async (_, _) => await SavePermissionDefinitionAsync();
        DockPanel.SetDock(saveBtn, Dock.Right);
        headDock.Children.Add(saveBtn);

        headerBorder.Child = headDock;
        DockPanel.SetDock(headerBorder, Dock.Top);
        mainDock.Children.Add(headerBorder);

        // Body
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

        // Code Preview Box
        var previewBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 14)
        };
        var pStack = new StackPanel();
        pStack.Children.Add(new TextBlock { Text = "CANONICAL API PERMISSION CODE", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
        _securityPermissionModuleInput = new TextBox { Text = permission.Module, IsReadOnly = !_securityCreatingPermission, Height = 32, MinHeight = 32, Padding = new Thickness(8, 2, 8, 2) };
        _securityPermissionActionInput = new TextBox { Text = permission.Action, IsReadOnly = !_securityCreatingPermission, Height = 32, MinHeight = 32, Padding = new Thickness(8, 2, 8, 2) };
        var codePreview = new TextBlock
        {
            Text = $"{permission.Module}:{permission.Action}",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 3, 0, 0)
        };
        pStack.Children.Add(codePreview);
        previewBox.Child = pStack;
        body.Children.Add(previewBox);

        // 2-column input fields
        var formGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var modField = (FrameworkElement)Field("Module slug", _securityPermissionModuleInput);
        modField.Margin = new Thickness(0, 0, 8, 10);
        Grid.SetRow(modField, 0); Grid.SetColumn(modField, 0);
        formGrid.Children.Add(modField);

        var actField = (FrameworkElement)Field("Action slug", _securityPermissionActionInput);
        actField.Margin = new Thickness(8, 0, 0, 10);
        Grid.SetRow(actField, 0); Grid.SetColumn(actField, 1);
        formGrid.Children.Add(actField);

        _securityPermissionModuleLabelInput = new TextBox { Text = permission.ModuleLabel, Height = 32, MinHeight = 32, Padding = new Thickness(8, 2, 8, 2) };
        var modLblField = (FrameworkElement)Field("Module display name", _securityPermissionModuleLabelInput);
        modLblField.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetRow(modLblField, 1); Grid.SetColumn(modLblField, 0);
        formGrid.Children.Add(modLblField);

        _securityPermissionLabelInput = new TextBox { Text = permission.Label, Height = 32, MinHeight = 32, Padding = new Thickness(8, 2, 8, 2) };
        var lblField = (FrameworkElement)Field("Permission label", _securityPermissionLabelInput);
        lblField.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetRow(lblField, 1); Grid.SetColumn(lblField, 1);
        formGrid.Children.Add(lblField);
        body.Children.Add(formGrid);

        _securityPermissionDescriptionInput = new TextBox { Text = permission.Description, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 12) };
        body.Children.Add(Field("Description", _securityPermissionDescriptionInput));

        var stateBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var sStack = new StackPanel();
        sStack.Children.Add(new TextBlock { Text = "Assignment State", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 4) });
        _securityPermissionActiveInput = new CheckBox
        {
            Content = "Definition is active",
            IsChecked = permission.IsActive,
            IsEnabled = SecurityHasPermission(SecurityCatalogUpdate),
            Margin = new Thickness(0, 0, 0, 2)
        };
        sStack.Children.Add(_securityPermissionActiveInput);
        sStack.Children.Add(new TextBlock { Text = "Deactivating prevents new role or user assignments. Existing grants must be audited in Access Health.", FontSize = 11, Foreground = (Brush)FindResource("MutedBrush") });
        stateBox.Child = sStack;
        body.Children.Add(stateBox);

        mainDock.Children.Add(body);
        card.Child = mainDock;
        return card;
    }

    private async Task SavePermissionDefinitionAsync()
    {
        if (!SecurityHasPermission(SecurityCatalogUpdate) || _securitySelectedPermission is not { } permission) return;
        var module = _securityPermissionModuleInput?.Text.Trim() ?? "";
        var action = _securityPermissionActionInput?.Text.Trim() ?? "";
        if (module.Length == 0 || action.Length == 0)
        {
            _securityMessage = "Module and action are required.";
            RenderCurrentPage();
            return;
        }
        var payload = new Dictionary<string, object?>
        {
            ["module"] = module,
            ["action"] = action,
            ["code"] = $"{module}:{action}",
            ["moduleLabel"] = _securityPermissionModuleLabelInput?.Text.Trim(),
            ["label"] = _securityPermissionLabelInput?.Text.Trim(),
            ["description"] = _securityPermissionDescriptionInput?.Text.Trim(),
            ["isActive"] = _securityPermissionActiveInput?.IsChecked == true,
            ["sortOrder"] = permission.SortOrder
        };
        try
        {
            var result = _securityCreatingPermission
                ? await _apiClient.PostJsonAsync("/api/permission-definitions", payload, _session.Token)
                : await _apiClient.PatchJsonAsync($"/api/permission-definitions/{Uri.EscapeDataString(permission.Id)}",
                    new Dictionary<string, object?> { ["moduleLabel"] = payload["moduleLabel"], ["label"] = payload["label"], ["description"] = payload["description"], ["isActive"] = payload["isActive"], ["sortOrder"] = payload["sortOrder"] }, _session.Token);
            if (!result.Success) throw new InvalidOperationException(SecurityError(result.Error, result.StatusCode));
            _securityMessage = _securityCreatingPermission ? "Permission definition created." : "Permission definition saved.";
            var selectedId = result.Data is JsonElement data ? SecurityJsonString(data, "id", permission.Id) : permission.Id;
            var refreshed = await SecurityGetAsync("/api/permission-definitions?includeInactive=true");
            _securityPermissions = ReadPermissions(refreshed);
            _securitySelectedPermission = _securityPermissions.FirstOrDefault(x => x.Id == selectedId);
            _securityCreatingPermission = false;
        }
        catch (Exception ex) { _securityMessage = ex.Message; }
        RenderCurrentPage();
    }

    private UIElement BuildUserAccessPage()
    {
        var outer = new StackPanel();

        var layout = new Grid { MinHeight = 600, Margin = new Thickness(0, 0, 0, 16) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left Panel: User Directory Card
        var directoryCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var dirDock = new DockPanel();

        var dirHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 10, 14, 10)
        };
        var dhDock = new DockPanel();
        var dhTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        dhTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.People,
            IconSize = IconSize.Size20,
            IconVariant = IconVariant.Regular,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        dhTitle.Children.Add(new TextBlock { Text = "User Directory", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        dhDock.Children.Add(dhTitle);

        var countBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        countBadge.Child = new TextBlock { Text = $"{_securityUsers.Count}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 118, 110)) };
        DockPanel.SetDock(countBadge, Dock.Right);
        dhDock.Children.Add(countBadge);
        dirHeader.Child = dhDock;
        DockPanel.SetDock(dirHeader, Dock.Top);
        dirDock.Children.Add(dirHeader);

        var grid = CreateSettingsDataGrid(_securityUsers, minHeight: 450, maxHeight: 680);
        grid.Columns.Add(new DataGridTextColumn { Header = "User", Binding = new Binding(nameof(SecurityUser.DisplayName)), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Role", Binding = new Binding(nameof(SecurityUser.RoleName)), Width = new DataGridLength(.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Direct Grants", Binding = new Binding(nameof(SecurityUser.DirectCount)), Width = 90 });
        grid.SelectedItem = _securityUsers.FirstOrDefault(x => x.UserId == _securitySelectedUser?.UserId);
        grid.SelectionChanged += (_, _) =>
        {
            if (grid.SelectedItem is SecurityUser selected && selected.UserId != _securitySelectedUser?.UserId)
            {
                _securitySelectedUser = selected;
                RenderCurrentPage();
            }
        };
        _securityUserGrid = grid;
        dirDock.Children.Add(grid);
        directoryCard.Child = dirDock;
        Grid.SetColumn(directoryCard, 0);
        layout.Children.Add(directoryCard);

        var editor = BuildUserAccessEditor(_securitySelectedUser);
        Grid.SetColumn(editor, 2);
        layout.Children.Add(editor);

        outer.Children.Add(layout);
        return outer;
    }

    private UIElement BuildUserAccessEditor(SecurityUser? user)
    {
        if (user is null) return EmptyState("Choose a user", "Select a user from the directory to review effective grants and configure direct overrides.");

        _securityUserPermissionChecks.Clear();

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Top
        };
        var mainDock = new DockPanel();

        // Header
        var headerBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(18, 12, 18, 12)
        };
        var headDock = new DockPanel();

        var titleStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromRgb(240, 253, 250)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(204, 251, 241)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconBox.Child = new FluentIcon { Icon = Symbol.PersonKey, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(iconBox);

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titleRow.Children.Add(new TextBlock { Text = user.DisplayName, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("TextBrush") });

        var roleBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(236, 253, 245)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(167, 243, 208)),
            Padding = new Thickness(7, 2, 7, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        roleBadge.Child = new TextBlock { Text = user.RoleName, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(5, 150, 105)) };
        titleRow.Children.Add(roleBadge);
        textStack.Children.Add(titleRow);
        textStack.Children.Add(new TextBlock { Text = $"{user.Email} · {user.RolePermissions.Count} role-derived grants · {user.DirectPermissions.Count} direct override(s)", FontSize = 11.5, Foreground = (Brush)FindResource("MutedBrush") });
        titleStack.Children.Add(textStack);
        headDock.Children.Add(titleStack);

        var saveBtn = new Button
        {
            Content = "Save direct grants",
            Style = (Style)FindResource("PrimaryButton"),
            Height = 32,
            Padding = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = SecurityHasPermission(SecurityDirectGrant)
        };
        saveBtn.Click += async (_, _) => await SaveUserDirectPermissionsAsync();
        DockPanel.SetDock(saveBtn, Dock.Right);
        headDock.Children.Add(saveBtn);

        headerBorder.Child = headDock;
        DockPanel.SetDock(headerBorder, Dock.Top);
        mainDock.Children.Add(headerBorder);

        // Body
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 650 };
        var body = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

        var staleDirect = user.DirectPermissions.Count(code => !_securityPermissions.Any(p => p.Code == code && p.IsActive));
        if (staleDirect > 0)
            body.Children.Add(Banner("Inactive or unknown direct grants", $"This user has {staleDirect} direct grant(s) missing from the active catalog. Saving will clean up these invalid codes.", "#D97706"));

        // Role-derived Box
        var roleBox = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var rbStack = new StackPanel();
        rbStack.Children.Add(new TextBlock { Text = "ROLE-DERIVED INHERITED GRANTS (READ-ONLY)", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 0, 0, 6) });
        rbStack.Children.Add(new TextBlock
        {
            Text = user.RolePermissions.Count == 0 ? "No active permissions inherited from role." : string.Join("  •  ", user.RolePermissions),
            FontSize = 11.5,
            Foreground = (Brush)FindResource("TextBrush"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16
        });
        roleBox.Child = rbStack;
        body.Children.Add(roleBox);

        // Direct Overrides Section Header
        body.Children.Add(new TextBlock { Text = "Personal Direct Permission Grants", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush"), Margin = new Thickness(0, 0, 0, 10) });

        var groups = BuildPermissionGroups(
            _securityPermissions,
            user.DirectPermissions.ToHashSet(StringComparer.Ordinal),
            SecurityHasPermission(SecurityDirectGrant),
            _securityUserPermissionChecks);
        body.Children.Add(groups);

        scroll.Content = body;
        mainDock.Children.Add(scroll);
        card.Child = mainDock;
        return card;
    }

    private async Task SaveUserDirectPermissionsAsync()
    {
        if (_securitySelectedUser is not { } user || !SecurityHasPermission(SecurityDirectGrant)) return;
        var permissions = _securityUserPermissionChecks.Where(c => c.IsChecked == true && c.Tag is string)
            .Select(c => (string)c.Tag).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var result = await _apiClient.PutJsonAsync($"/api/access-control/users/{Uri.EscapeDataString(user.UserId)}/permissions",
            new Dictionary<string, object?> { ["permissions"] = permissions }, _session.Token);
        if (!result.Success) _securityMessage = SecurityError(result.Error, result.StatusCode);
        else
        {
            _securityMessage = "Direct permission overrides saved.";
            var data = await SecurityGetAsync("/api/access-control/users");
            _securityUsers = ReadUsers(data);
            _securitySelectedUser = _securityUsers.FirstOrDefault(x => x.UserId == user.UserId);
        }
        RenderCurrentPage();
    }

    private UIElement BuildAccessHealthPage()
    {
        var outer = new StackPanel();
        var health = SecurityJsonString(_securityHealth, "health", "Unknown");
        var count = SecurityJsonInt(_securityHealth, "rolesWithOrphans");
        var isHealthy = health == "healthy";

        // Top Diagnostic Hero Card
        var heroCard = new Border
        {
            Background = isHealthy ? new SolidColorBrush(Color.FromRgb(240, 253, 244)) : new SolidColorBrush(Color.FromRgb(254, 243, 199)),
            BorderBrush = isHealthy ? new SolidColorBrush(Color.FromRgb(187, 247, 208)) : new SolidColorBrush(Color.FromRgb(253, 230, 138)),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            Padding = new Thickness(18, 14, 18, 14),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var heroDock = new DockPanel();
        var heroTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        heroTitle.Children.Add(new FluentIcon
        {
            Icon = Symbol.HeartPulse,
            IconSize = IconSize.Size24,
            IconVariant = IconVariant.Regular,
            Foreground = isHealthy ? new SolidColorBrush(Color.FromRgb(5, 150, 105)) : new SolidColorBrush(Color.FromRgb(217, 119, 6)),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        var hTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        hTextStack.Children.Add(new TextBlock
        {
            Text = isHealthy ? "Permission Assignments are Healthy" : $"{count} Role(s) Reference Inactive or Unknown Grants",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = isHealthy ? new SolidColorBrush(Color.FromRgb(6, 95, 70)) : new SolidColorBrush(Color.FromRgb(146, 64, 14))
        });
        hTextStack.Children.Add(new TextBlock
        {
            Text = "The backend diagnostic guard compares active definitions against database role assignments and direct grants.",
            FontSize = 12,
            Foreground = isHealthy ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(180, 83, 9))
        });
        heroTitle.Children.Add(hTextStack);
        heroDock.Children.Add(heroTitle);
        heroCard.Child = heroDock;
        outer.Children.Add(heroCard);

        // Card 1: Role Inconsistencies Table
        var roleCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 0, 16)
        };
        var rcDock = new DockPanel();
        var rcHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var rchDock = new DockPanel();
        var rchTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        rchTitle.Children.Add(new FluentIcon { Icon = Symbol.Shield, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        rchTitle.Children.Add(new TextBlock { Text = "Role Grant Inconsistencies", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        rchDock.Children.Add(rchTitle);

        var removeBtn = new Button
        {
            Content = "Repair selected role",
            Height = 28,
            Padding = new Thickness(10, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _securitySelectedOrphan is not null && SecurityHasPermission(SecurityRolesUpdate)
        };
        removeBtn.Click += async (_, _) => await RemoveOrphanRoleGrantsAsync();
        DockPanel.SetDock(removeBtn, Dock.Right);
        rchDock.Children.Add(removeBtn);
        rcHeader.Child = rchDock;
        DockPanel.SetDock(rcHeader, Dock.Top);
        rcDock.Children.Add(rcHeader);

        var grid = CreateSettingsDataGrid(_securityOrphans, minHeight: 180, maxHeight: 320);
        grid.Columns.Add(new DataGridTextColumn { Header = "Role Name", Binding = new Binding(nameof(SecurityOrphanRole.RoleName)), Width = new DataGridLength(.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Assigned Users", Binding = new Binding(nameof(SecurityOrphanRole.UserCount)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Inactive / Unknown Codes", Binding = new Binding(nameof(SecurityOrphanRole.OrphanSummary)), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });
        grid.SelectedItem = _securityOrphans.FirstOrDefault(x => x.RoleId == _securitySelectedOrphan?.RoleId);
        grid.SelectionChanged += (_, _) =>
        {
            _securitySelectedOrphan = grid.SelectedItem as SecurityOrphanRole;
            removeBtn.IsEnabled = _securitySelectedOrphan is not null && SecurityHasPermission(SecurityRolesUpdate);
        };
        _securityHealthGrid = grid;
        rcDock.Children.Add(grid);
        roleCard.Child = rcDock;
        outer.Children.Add(roleCard);

        // Card 2: Personal Direct Overrides Inconsistencies
        var activeCodes = _securityPermissions.Where(p => p.IsActive).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var directIssues = _securityUsers.Select(user => new SecurityUserAccessIssue(
                user.UserId, user.DisplayName, user.Email,
                user.DirectPermissions.Where(code => !activeCodes.Contains(code)).ToList()))
            .Where(user => user.InactivePermissions.Count > 0).ToList();

        var directCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"),
            ClipToBounds = true
        };
        var dcDock = new DockPanel();
        var dcHeader = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = (Brush)FindResource("BorderBrushSoft"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 10, 16, 10)
        };
        var dchDock = new DockPanel();
        var dchTitle = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        dchTitle.Children.Add(new FluentIcon { Icon = Symbol.Person, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
        dchTitle.Children.Add(new TextBlock { Text = $"Direct Overrides Requiring Audit ({directIssues.Count} users)", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextBrush") });
        dchDock.Children.Add(dchTitle);
        dcHeader.Child = dchDock;
        DockPanel.SetDock(dcHeader, Dock.Top);
        dcDock.Children.Add(dcHeader);

        var directGrid = CreateSettingsDataGrid(directIssues, minHeight: 140, maxHeight: 280);
        directGrid.Columns.Add(new DataGridTextColumn { Header = "User Display Name", Binding = new Binding(nameof(SecurityUserAccessIssue.DisplayName)), Width = new DataGridLength(.9, DataGridLengthUnitType.Star) });
        directGrid.Columns.Add(new DataGridTextColumn { Header = "Email Address", Binding = new Binding(nameof(SecurityUserAccessIssue.Email)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        directGrid.Columns.Add(new DataGridTextColumn { Header = "Inactive / Unknown Direct Codes", Binding = new Binding(nameof(SecurityUserAccessIssue.PermissionSummary)), Width = new DataGridLength(2.2, DataGridLengthUnitType.Star) });
        directGrid.MouseDoubleClick += (_, _) =>
        {
            if (directGrid.SelectedItem is SecurityUserAccessIssue issue)
            {
                _securitySelectedUser = _securityUsers.FirstOrDefault(user => user.UserId == issue.UserId);
                NavigateToSettings("User Access");
            }
        };
        dcDock.Children.Add(directGrid);
        directCard.Child = dcDock;
        outer.Children.Add(directCard);

        return outer;
    }

    private async Task RemoveOrphanRoleGrantsAsync()
    {
        if (_securitySelectedOrphan is not { } orphan || !SecurityHasPermission(SecurityRolesUpdate)) return;
        if (MessageBox.Show($"Remove {orphan.OrphanPermissions.Count} inactive or unknown grant(s) from '{orphan.RoleName}'?", "Repair role grants", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var result = await _apiClient.PostJsonAsync($"/api/roles/{Uri.EscapeDataString(orphan.RoleId)}/migrate-permissions",
            new Dictionary<string, object?> { ["action"] = "remove-orphans" }, _session.Token);
        if (!result.Success) _securityMessage = SecurityError(result.Error, result.StatusCode);
        else
        {
            _securityMessage = "Inactive and unknown grants removed from the selected role.";
            _securityHealth = await SecurityGetAsync("/api/roles/health/orphans");
            _securityOrphans = ReadOrphans(_securityHealth);
            _securitySelectedOrphan = null;
        }
        RenderCurrentPage();
    }

    private static List<SecurityRole> ReadRoles(JsonElement source) => SecurityJsonArray(source).Select(item => new SecurityRole(
        SecurityJsonString(item, "id"), SecurityJsonString(item, "name"), SecurityJsonString(item, "description"), SecurityJsonStringArray(item, "permissions"),
        SecurityJsonBool(item, "isActive", true), SecurityJsonInt(item, "userCount"))).ToList();

    private static List<SecurityPermission> ReadPermissions(JsonElement source) => SecurityJsonArray(source).Select(item => new SecurityPermission(
        SecurityJsonString(item, "id"), SecurityJsonString(item, "code"), SecurityJsonString(item, "module"), SecurityJsonString(item, "moduleLabel"),
        SecurityJsonString(item, "action"), SecurityJsonString(item, "label"), SecurityJsonInt(item, "sortOrder"), SecurityJsonBool(item, "isActive", true), SecurityJsonString(item, "description"))).ToList();

    private static List<SecurityUser> ReadUsers(JsonElement source) => SecurityJsonArray(source).Select(item => new SecurityUser(
        SecurityJsonString(item, "userId"), SecurityJsonString(item, "displayName", SecurityJsonString(item, "email")), SecurityJsonString(item, "email"),
        SecurityJsonString(item, "roleName", SecurityJsonString(item, "role")), SecurityJsonStringArray(item, "rolePermissions"), SecurityJsonStringArray(item, "directPermissions"),
        SecurityJsonString(item, "status", "ACTIVE"))).ToList();

    private static List<SecurityOrphanRole> ReadOrphans(JsonElement source) => SecurityJsonArray(SecurityJsonProperty(source, "orphans"))
        .Select(item => new SecurityOrphanRole(SecurityJsonString(item, "roleId"), SecurityJsonString(item, "roleName"), SecurityJsonInt(item, "userCount"), SecurityJsonStringArray(item, "orphanPermissions"))).ToList();

    private static List<JsonElement> SecurityJsonArray(JsonElement source)
    {
        if (source.ValueKind == JsonValueKind.Array) return source.EnumerateArray().Select(x => x.Clone()).ToList();
        foreach (var key in new[] { "items", "data", "results" })
        {
            var value = SecurityJsonProperty(source, key);
            if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().Select(x => x.Clone()).ToList();
        }
        return [];
    }

    private static JsonElement SecurityJsonProperty(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) ? value : default;

    private static string SecurityJsonString(JsonElement element, string property, string fallback = "")
    {
        var value = SecurityJsonProperty(element, property);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? fallback,
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => fallback
        };
    }

    private static int SecurityJsonInt(JsonElement element, string property)
    {
        var value = SecurityJsonProperty(element, property);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : 0;
    }

    private static int SecurityNestedInt(JsonElement element, string parent, string property) => SecurityJsonInt(SecurityJsonProperty(element, parent), property);

    private static bool SecurityJsonBool(JsonElement element, string property, bool fallback)
    {
        var value = SecurityJsonProperty(element, property);
        return value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => fallback };
    }

    private static List<string> SecurityJsonStringArray(JsonElement element, string property)
    {
        var value = SecurityJsonProperty(element, property);
        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList()
            : [];
    }

    private sealed record SecurityRole(string Id, string Name, string Description, List<string> Permissions, bool IsActive, int UserCount)
    {
        public int PermissionCount => Permissions.Count;
        public string Status => IsActive ? "Active" : "Inactive";
    }

    private sealed record SecurityPermission(string Id, string Code, string Module, string ModuleLabel, string Action,
        string Label, int SortOrder, bool IsActive, string Description = "")
    {
        public string ModuleDisplay => string.IsNullOrWhiteSpace(ModuleLabel) ? Module : ModuleLabel;
        public string Status => IsActive ? "Active" : "Inactive";
    }

    private sealed record SecurityUser(string UserId, string DisplayName, string Email, string RoleName,
        List<string> RolePermissions, List<string> DirectPermissions, string Status)
    {
        public int DirectCount => DirectPermissions.Count;
    }

    private sealed record SecurityOrphanRole(string RoleId, string RoleName, int UserCount, List<string> OrphanPermissions)
    {
        public string OrphanSummary => string.Join(", ", OrphanPermissions);
    }

    private sealed record SecurityUserAccessIssue(string UserId, string DisplayName, string Email, List<string> InactivePermissions)
    {
        public string PermissionSummary => string.Join(", ", InactivePermissions);
    }
}

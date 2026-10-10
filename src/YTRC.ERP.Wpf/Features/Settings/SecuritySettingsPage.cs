using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

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
    private StackPanel? _securityRolePermissionPanel;
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
        var page = PageStack();
        var subtitle = _settingsSection switch
        {
            "Role Access" => "Manage YTRC role grants using the same module:action permissions enforced by the backend.",
            "Permission Catalog" => "Review and maintain the canonical permission definitions used by roles and API guards.",
            "User Access" => "Review role-derived permissions and manage individual direct overrides.",
            "Access Health" => "Find inactive or unknown permission codes that remain assigned to roles.",
            _ => "A single view of role-based access, direct overrides, permission definitions and backend enforcement."
        };
        page.Children.Add(TitleBlock("Security & Access", subtitle));

        if (_session.IsPreview)
        {
            page.Children.Add(Banner("Sign in required", "Security administration uses authenticated YTRC FastAPI endpoints.", "#D97706"));
            return page;
        }
        if (!SecurityHasPermission(SecurityRolesRead))
        {
            page.Children.Add(Banner("Access restricted", "This workspace requires roles:read. The backend remains the authority for every operation.", "#DC2626"));
            return page;
        }

        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var refresh = new Button { Content = _securityLoading ? "Loading…" : "Refresh", IsEnabled = !_securityLoading,
            Style = (Style)FindResource(typeof(Button)), Margin = new Thickness(0, 0, 8, 8) };
        refresh.Click += async (_, _) => await LoadSecuritySettingsAsync();
        toolbar.Children.Add(refresh);
        toolbar.Children.Add(Label($"Backend · {AppSettings.Current.ApiBaseUrl}", 12, (Brush)FindResource("MutedBrush")));
        page.Children.Add(toolbar);
        if (!string.IsNullOrWhiteSpace(_securityMessage))
            page.Children.Add(Banner(_securityLoading ? "Loading" : "Security workspace", _securityMessage,
                _securityLoading ? "#297691" : "#DC2626"));

        if (_securityLoading) return page;
        page.Children.Add(_settingsSection switch
        {
            "Role Access" => BuildSecurityRolesPage(),
            "Permission Catalog" => BuildPermissionCatalogPage(),
            "User Access" => BuildUserAccessPage(),
            "Access Health" => BuildAccessHealthPage(),
            _ => BuildSecurityOverviewPage()
        });
        return page;
    }

    private UIElement BuildSecurityOverviewPage()
    {
        var content = new StackPanel();
        var metrics = new UniformGrid { Columns = 4, Margin = new Thickness(-5, 0, -5, 6) };
        metrics.Children.Add(SecurityMetric("Roles", SecurityNestedInt(_securityStats, "roles", "total"), "Active: " + SecurityNestedInt(_securityStats, "roles", "active")));
        metrics.Children.Add(SecurityMetric("Permission definitions", SecurityNestedInt(_securityStats, "permissions", "total"), "Active definitions"));
        metrics.Children.Add(SecurityMetric("Direct overrides", SecurityJsonInt(_securityStats, "usersWithOverrides"), "Users with personal grants"));
        metrics.Children.Add(SecurityMetric("Role grant issues", SecurityJsonInt(_securityHealth, "rolesWithOrphans"), SecurityJsonString(_securityHealth, "health", "Unknown")));
        content.Children.Add(metrics);

        var highRiskCodes = new HashSet<string>(["users:delete", "roles:delete", "access_control:manage", "permission_definitions:update", "workflow:manage"], StringComparer.Ordinal);
        var broadlyGrantedRoles = _securityRoles.Where(role => !role.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase) &&
            role.Permissions.Any(highRiskCodes.Contains)).Select(role => role.Name).ToList();
        if (broadlyGrantedRoles.Count > 0)
            content.Children.Add(Banner("Review elevated role grants", $"{broadlyGrantedRoles.Count} non-Administrator role(s) contain user/role deletion, access administration, permission catalog or workflow management rights: {string.Join(", ", broadlyGrantedRoles)}. The backend seed currently gives every seeded role the full permission set, so review and narrow those grants for least privilege.", "#DC2626"));

        content.Children.Add(SectionHeading("How access is evaluated"));
        content.Children.Add(Banner("Role permissions", "The backend loads the user's current role and direct grants, then compares exact module:action codes such as truckscale:read or bookings:update against each protected API route. Changes to a role are picked up from the database on subsequent API requests.", "#0F766E"));
        content.Children.Add(Banner("Direct user permissions", "A direct grant is added to the user's role grants. Use it for a documented exception; prefer updating the role when several people need the same access.", "#297691"));
        content.Children.Add(Banner("Catalog status", "Deactivating a definition prevents assigning it through the role editor, but the current backend authorization guard does not automatically revoke codes already assigned to roles or users. Remove stale grants in Role Access or User Access, then verify Access Health.", "#D97706"));

        var actions = new WrapPanel();
        actions.Children.Add(SecurityNavButton("Roles & permissions", "Role Access"));
        actions.Children.Add(SecurityNavButton("Permission catalog", "Permission Catalog"));
        actions.Children.Add(SecurityNavButton("User access", "User Access"));
        actions.Children.Add(SecurityNavButton("Access health", "Access Health"));
        content.Children.Add(actions);
        return content;
    }

    private UIElement SecurityMetric(string title, int value, string detail)
    {
        var body = new StackPanel { Margin = new Thickness(7), MinHeight = 80 };
        body.Children.Add(Label(title, 12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold));
        body.Children.Add(Label(value.ToString(), 24, (Brush)FindResource("TextBrush"), FontWeights.Bold));
        body.Children.Add(Label(detail, 11, (Brush)FindResource("MutedBrush")));
        return Card(body, "#0F766E");
    }

    private Button SecurityNavButton(string label, string section)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 2, 8, 2), Style = (Style)FindResource(typeof(Button)) };
        button.Click += (_, _) => NavigateToSettings(section);
        return button;
    }

    private UIElement BuildSecurityRolesPage()
    {
        var outer = new StackPanel();
        var toolbar = new WrapPanel();
        var add = new Button { Content = "New role", IsEnabled = SecurityHasPermission(SecurityRolesCreate), Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 8) };
        add.Click += (_, _) =>
        {
            _securityCreatingRole = true;
            _securitySelectedRole = new SecurityRole("", "", "", [], true, 0);
            RenderCurrentPage();
        };
        toolbar.Children.Add(add);
        var save = new Button { Content = "Save role", IsEnabled = _securityCreatingRole ? SecurityHasPermission(SecurityRolesCreate) : SecurityHasPermission(SecurityRolesUpdate), Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 8) };
        save.Click += async (_, _) => await SaveSecurityRoleAsync();
        toolbar.Children.Add(save);
        var delete = new Button { Content = "Delete role", IsEnabled = !_securityCreatingRole && _securitySelectedRole is { UserCount: 0 } && SecurityHasPermission(SecurityRolesDelete), Style = (Style)FindResource(typeof(Button)), Margin = new Thickness(0, 0, 8, 8) };
        delete.Click += async (_, _) => await DeleteSecurityRoleAsync();
        toolbar.Children.Add(delete);
        outer.Children.Add(toolbar);

        var columns = new Grid { MinHeight = 420 };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var roleGrid = new DataGrid { ItemsSource = _securityRoles, AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single, MinHeight = 420,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        roleGrid.Columns.Add(new DataGridTextColumn { Header = "Role", Binding = new Binding(nameof(SecurityRole.Name)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        roleGrid.Columns.Add(new DataGridTextColumn { Header = "Users", Binding = new Binding(nameof(SecurityRole.UserCount)), Width = 55 });
        roleGrid.Columns.Add(new DataGridTextColumn { Header = "Grants", Binding = new Binding(nameof(SecurityRole.PermissionCount)), Width = 60 });
        roleGrid.SelectedItem = _securitySelectedRole is null ? null : _securityRoles.FirstOrDefault(x => x.Id == _securitySelectedRole.Id);
        roleGrid.SelectionChanged += (_, _) =>
        {
            if (roleGrid.SelectedItem is SecurityRole selected && selected.Id != _securitySelectedRole?.Id)
            {
                _securityCreatingRole = false;
                _securitySelectedRole = selected;
                RenderCurrentPage();
            }
        };
        _securityRoleGrid = roleGrid;
        Grid.SetColumn(roleGrid, 0);
        columns.Children.Add(Card(roleGrid, "#0F766E"));

        var editor = BuildSecurityRoleEditor(_securitySelectedRole);
        Grid.SetColumn(editor, 2);
        columns.Children.Add(editor);
        outer.Children.Add(columns);
        return outer;
    }

    private UIElement BuildSecurityRoleEditor(SecurityRole? role)
    {
        if (role is null) return EmptyState("Choose a role", "Select a role to review its grants, or create a new role.");
        _securityRolePermissionChecks.Clear();
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading(_securityCreatingRole ? "Create role" : $"Edit · {role.Name}"));
        _securityRoleNameInput = new TextBox { Text = role.Name, Margin = new Thickness(0, 0, 0, 10), MinHeight = 32 };
        panel.Children.Add(Field("Role name", _securityRoleNameInput));
        _securityRoleDescriptionInput = new TextBox { Text = role.Description, Margin = new Thickness(0, 0, 0, 10), MinHeight = 32 };
        panel.Children.Add(Field("Description", _securityRoleDescriptionInput));
        _securityRoleActiveInput = new CheckBox { Content = "Role is active", IsChecked = role.IsActive, Margin = new Thickness(0, 0, 0, 8), IsEnabled = SecurityHasPermission(_securityCreatingRole ? SecurityRolesCreate : SecurityRolesUpdate) };
        panel.Children.Add(_securityRoleActiveInput);
        var inactiveCount = role.Permissions.Count(code => !_securityPermissions.Any(p => p.Code == code && p.IsActive));
        if (inactiveCount > 0)
            panel.Children.Add(Banner("Inactive or unknown grants", $"This role has {inactiveCount} code(s) missing from the active catalog. Saving will remove those codes; review the affected users first.", "#D97706"));
        panel.Children.Add(SectionHeading($"Permission grants · {role.Permissions.Count(code => _securityPermissions.Any(p => p.Code == code && p.IsActive))} active"));
        _securityRolePermissionPanel = new StackPanel();
        var scroll = new ScrollViewer { Content = _securityRolePermissionPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 290, MaxHeight = 470 };
        scroll.Content = BuildPermissionGroups(_securityPermissions, role.Permissions.ToHashSet(StringComparer.Ordinal),
            SecurityHasPermission(_securityCreatingRole ? SecurityRolesCreate : SecurityRolesUpdate), _securityRolePermissionChecks);
        panel.Children.Add(scroll);
        return Card(panel, "#297691");
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

    private StackPanel BuildPermissionGroups(IReadOnlyList<SecurityPermission> all, HashSet<string> selected, bool canEdit, List<CheckBox> checks)
    {
        var root = new StackPanel();
        foreach (var group in all.GroupBy(p => string.IsNullOrWhiteSpace(p.ModuleLabel) ? p.Module : p.ModuleLabel)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var list = new StackPanel { Margin = new Thickness(8, 2, 4, 8) };
            foreach (var permission in group.OrderBy(p => p.SortOrder).ThenBy(p => p.Code, StringComparer.Ordinal))
            {
                var checkbox = new CheckBox
                {
                    Content = $"{(string.IsNullOrWhiteSpace(permission.Label) ? permission.Action : permission.Label)}  ·  {permission.Code}",
                    Tag = permission.Code,
                    IsChecked = permission.IsActive && selected.Contains(permission.Code),
                    IsEnabled = canEdit && permission.IsActive,
                    ToolTip = permission.Description ?? permission.Code,
                    Margin = new Thickness(2, 4, 2, 4)
                };
                checks.Add(checkbox);
                list.Children.Add(checkbox);
            }
            var expander = new Expander { Header = group.Key, Content = list, IsExpanded = true, Margin = new Thickness(0, 2, 0, 2),
                Foreground = (Brush)FindResource("TextBrush"), FontWeight = FontWeights.SemiBold };
            root.Children.Add(expander);
        }
        return root;
    }

    private UIElement BuildPermissionCatalogPage()
    {
        var outer = new StackPanel();
        outer.Children.Add(Banner("Permission codes are API contracts", "The backend expects exact module:action codes. Module and action are locked after creation; retire unused codes by marking them inactive. The backend currently does not revoke existing role or direct grants just because a definition is deactivated.", "#D97706"));
        var toolbar = new WrapPanel();
        var add = new Button { Content = "Add definition", IsEnabled = SecurityHasPermission(SecurityCatalogUpdate), Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 8) };
        add.Click += (_, _) => { _securityCreatingPermission = true; _securitySelectedPermission = new SecurityPermission("", "", "", "", "", "", 0, true); RenderCurrentPage(); };
        toolbar.Children.Add(add);
        var save = new Button { Content = "Save definition", IsEnabled = SecurityHasPermission(SecurityCatalogUpdate), Style = (Style)FindResource(typeof(Button)), Margin = new Thickness(0, 0, 8, 8) };
        save.Click += async (_, _) => await SavePermissionDefinitionAsync();
        toolbar.Children.Add(save);
        outer.Children.Add(toolbar);

        var layout = new Grid { MinHeight = 420 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var grid = new DataGrid { ItemsSource = _securityPermissions, AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single, MinHeight = 420,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        grid.Columns.Add(new DataGridTextColumn { Header = "Module", Binding = new Binding(nameof(SecurityPermission.ModuleDisplay)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Code", Binding = new Binding(nameof(SecurityPermission.Code)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
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
        Grid.SetColumn(grid, 0);
        layout.Children.Add(Card(grid, "#0F766E"));

        var editor = BuildPermissionDefinitionEditor(_securitySelectedPermission);
        Grid.SetColumn(editor, 2);
        layout.Children.Add(editor);
        outer.Children.Add(layout);
        return outer;
    }

    private UIElement BuildPermissionDefinitionEditor(SecurityPermission? permission)
    {
        if (permission is null) return EmptyState("Choose a definition", "Select a definition to update its display name, description or active status.");
        var panel = new StackPanel();
        panel.Children.Add(SectionHeading(_securityCreatingPermission ? "New permission definition" : $"Edit · {permission.Code}"));
        _securityPermissionModuleInput = new TextBox { Text = permission.Module, IsReadOnly = !_securityCreatingPermission, Margin = new Thickness(0, 0, 0, 8), MinHeight = 32 };
        _securityPermissionActionInput = new TextBox { Text = permission.Action, IsReadOnly = !_securityCreatingPermission, Margin = new Thickness(0, 0, 0, 8), MinHeight = 32 };
        _securityPermissionModuleLabelInput = new TextBox { Text = permission.ModuleLabel, Margin = new Thickness(0, 0, 0, 8), MinHeight = 32 };
        _securityPermissionLabelInput = new TextBox { Text = permission.Label, Margin = new Thickness(0, 0, 0, 8), MinHeight = 32 };
        _securityPermissionDescriptionInput = new TextBox { Text = permission.Description, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90, Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(Field("Module slug", _securityPermissionModuleInput));
        panel.Children.Add(Field("Action", _securityPermissionActionInput));
        panel.Children.Add(Field("Module display name", _securityPermissionModuleLabelInput));
        panel.Children.Add(Field("Permission label", _securityPermissionLabelInput));
        panel.Children.Add(Field("Description", _securityPermissionDescriptionInput));
        _securityPermissionActiveInput = new CheckBox { Content = "Definition is active", IsChecked = permission.IsActive,
            IsEnabled = SecurityHasPermission(SecurityCatalogUpdate), Margin = new Thickness(0, 3, 0, 8) };
        panel.Children.Add(_securityPermissionActiveInput);
        panel.Children.Add(Label($"Code preview: {(_securityPermissionModuleInput.Text.Trim().Length == 0 ? "module" : _securityPermissionModuleInput.Text.Trim())}:{(_securityPermissionActionInput.Text.Trim().Length == 0 ? "action" : _securityPermissionActionInput.Text.Trim())}",
            12, (Brush)FindResource("MutedBrush"), FontWeights.SemiBold));
        panel.Children.Add(Label("Use lowercase module slugs and stable action names. Existing role assignments prevent changing the code.", 12, (Brush)FindResource("MutedBrush")));
        return Card(panel, "#297691");
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
        outer.Children.Add(Banner("Use direct grants sparingly", "Direct grants are additive to role permissions and replace the user's entire direct-grant list when saved. Prefer changing a role for shared access; verify the effective access after saving.", "#D97706"));
        var save = new Button { Content = "Save direct grants", IsEnabled = SecurityHasPermission(SecurityDirectGrant), Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 8) };
        save.Click += async (_, _) => await SaveUserDirectPermissionsAsync();
        outer.Children.Add(save);

        var layout = new Grid { MinHeight = 420 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var grid = new DataGrid { ItemsSource = _securityUsers, AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single, MinHeight = 420,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        grid.Columns.Add(new DataGridTextColumn { Header = "User", Binding = new Binding(nameof(SecurityUser.DisplayName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Role", Binding = new Binding(nameof(SecurityUser.RoleName)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Direct", Binding = new Binding(nameof(SecurityUser.DirectCount)), Width = 60 });
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
        Grid.SetColumn(grid, 0);
        layout.Children.Add(Card(grid, "#0F766E"));
        var editor = BuildUserAccessEditor(_securitySelectedUser);
        Grid.SetColumn(editor, 2);
        layout.Children.Add(editor);
        outer.Children.Add(layout);
        return outer;
    }

    private UIElement BuildUserAccessEditor(SecurityUser? user)
    {
        if (user is null) return EmptyState("Choose a user", "Select a user to compare role grants with direct overrides.");
        _securityUserPermissionChecks.Clear();
        var body = new StackPanel();
        body.Children.Add(SectionHeading(user.DisplayName));
        body.Children.Add(Label($"{user.Email} · {user.RoleName} · {user.RolePermissions.Count} role grant(s) · {user.DirectPermissions.Count} direct grant(s)", 12, (Brush)FindResource("MutedBrush")));
        var staleDirect = user.DirectPermissions.Count(code => !_securityPermissions.Any(p => p.Code == code && p.IsActive));
        if (staleDirect > 0)
            body.Children.Add(Banner("Inactive or unknown direct grants", $"This account has {staleDirect} direct grant(s) missing from the active catalog. Saving will remove those codes.", "#D97706"));
        body.Children.Add(SectionHeading("Role-derived grants (read only)"));
        body.Children.Add(Label(user.RolePermissions.Count == 0 ? "No role permissions returned." : string.Join("  ·  ", user.RolePermissions), 12, (Brush)FindResource("MutedBrush")));
        body.Children.Add(SectionHeading("Direct permission overrides"));
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 260, MaxHeight = 420 };
        scroll.Content = BuildPermissionGroups(_securityPermissions, user.DirectPermissions.ToHashSet(StringComparer.Ordinal),
            SecurityHasPermission(SecurityDirectGrant), _securityUserPermissionChecks);
        body.Children.Add(scroll);
        return Card(body, "#297691");
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
        outer.Children.Add(Banner(health == "healthy" ? "Permission assignments are healthy" : $"{count} role(s) need review",
            "The backend health endpoint checks role grants against active definitions. It does not include direct user grants; review those in User Access when retiring a code.",
            health == "healthy" ? "#0F766E" : "#D97706"));
        var grid = new DataGrid { ItemsSource = _securityOrphans, AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, SelectionMode = DataGridSelectionMode.Single, MinHeight = 300,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        grid.Columns.Add(new DataGridTextColumn { Header = "Role", Binding = new Binding(nameof(SecurityOrphanRole.RoleName)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Assigned users", Binding = new Binding(nameof(SecurityOrphanRole.UserCount)), Width = 95 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Inactive / unknown codes", Binding = new Binding(nameof(SecurityOrphanRole.OrphanSummary)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        grid.SelectedItem = _securityOrphans.FirstOrDefault(x => x.RoleId == _securitySelectedOrphan?.RoleId);
        grid.SelectionChanged += (_, _) => { _securitySelectedOrphan = grid.SelectedItem as SecurityOrphanRole; };
        _securityHealthGrid = grid;
        outer.Children.Add(Card(grid, "#D97706"));
        var activeCodes = _securityPermissions.Where(p => p.IsActive).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var directIssues = _securityUsers.Select(user => new SecurityUserAccessIssue(
                user.UserId, user.DisplayName, user.Email,
                user.DirectPermissions.Where(code => !activeCodes.Contains(code)).ToList()))
            .Where(user => user.InactivePermissions.Count > 0).ToList();
        outer.Children.Add(SectionHeading($"Direct grants requiring review · {directIssues.Count} user(s)"));
        outer.Children.Add(Label("The BE role-health endpoint does not include personal grants. Review these users in User Access and remove grants that are no longer valid.", 12, (Brush)FindResource("MutedBrush")));
        var directGrid = new DataGrid { ItemsSource = directIssues, AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column, MinHeight = 120, MaxHeight = 240,
            Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        directGrid.Columns.Add(new DataGridTextColumn { Header = "User", Binding = new Binding(nameof(SecurityUserAccessIssue.DisplayName)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        directGrid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new Binding(nameof(SecurityUserAccessIssue.Email)), Width = new DataGridLength(.9, DataGridLengthUnitType.Star) });
        directGrid.Columns.Add(new DataGridTextColumn { Header = "Inactive / unknown direct codes", Binding = new Binding(nameof(SecurityUserAccessIssue.PermissionSummary)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        directGrid.MouseDoubleClick += (_, _) =>
        {
            if (directGrid.SelectedItem is SecurityUserAccessIssue issue)
            {
                _securitySelectedUser = _securityUsers.FirstOrDefault(user => user.UserId == issue.UserId);
                NavigateToSettings("User Access");
            }
        };
        outer.Children.Add(Card(directGrid, "#297691"));
        var remove = new Button { Content = "Remove inactive / unknown grants from selected role", IsEnabled = _securitySelectedOrphan is not null && SecurityHasPermission(SecurityRolesUpdate),
            Style = (Style)FindResource(typeof(Button)), Margin = new Thickness(0, 8, 0, 4) };
        remove.Click += async (_, _) => await RemoveOrphanRoleGrantsAsync();
        outer.Children.Add(remove);
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

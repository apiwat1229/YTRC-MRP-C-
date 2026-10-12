using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

internal sealed record UserRoleChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

internal sealed record UserAccountRecord(
    string Id,
    string DisplayName,
    string EmployeeId,
    string Username,
    string Email,
    string Department,
    string Position,
    string RoleName,
    string RoleId,
    string Status,
    string Phone)
{
    public bool IsAdministrator => RoleName.Contains("admin", StringComparison.OrdinalIgnoreCase);
    public string DepartmentPosition => string.Join(" · ", new[] { Department, Position }.Where(value => !string.IsNullOrWhiteSpace(value)));

    public static UserAccountRecord FromJson(JsonElement item)
    {
        var firstName = Read(item, "firstName");
        var lastName = Read(item, "lastName");
        var name = Read(item, "displayName");
        if (string.IsNullOrWhiteSpace(name)) name = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = FirstNonEmpty(Read(item, "username"), Read(item, "email"), "Unnamed user");
        return new UserAccountRecord(
            Read(item, "id"), name, Read(item, "employeeId"), Read(item, "username"), Read(item, "email"),
            Read(item, "department"), Read(item, "position"), Read(item, "roleName", Read(item, "role", "User")),
            Read(item, "roleId"), Read(item, "status", "ACTIVE"), Read(item, "phone"));
    }

    public static List<UserAccountRecord> ReadMany(JsonElement payload)
        => Unwrap(payload).Where(row => row.ValueKind == JsonValueKind.Object).Select(FromJson).ToList();

    public static List<UserRoleChoice> ReadRoles(JsonElement payload)
        => Unwrap(payload)
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new UserRoleChoice(Read(row, "id"), Read(row, "name")))
            .Where(role => !string.IsNullOrWhiteSpace(role.Name))
            .DistinctBy(role => $"{role.Id}|{role.Name}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<JsonElement> Unwrap(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array) return payload.EnumerateArray().Select(value => value.Clone()).ToList();
        if (payload.ValueKind != JsonValueKind.Object) return [];
        foreach (var key in new[] { "data", "users", "items", "results" })
            if (payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().Select(row => row.Clone()).ToList();
        return [payload.Clone()];
    }

    private static string Read(JsonElement item, string property, string fallback = "")
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(property, out var value)) return fallback;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;
    }

    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
}

public partial class MainWindow
{
    private const string UserCreatePermission = "users:create";
    private const string UserUpdatePermission = "users:update";
    private const string UserDeletePermission = "users:delete";

    private bool UserHasPermission(string permission) =>
        _session.User.Role == YTRC.ERP.Domain.Models.UserRole.SystemAdmin ||
        _session.User.RawPermissions.Contains(permission, StringComparer.Ordinal);

    private UIElement BuildUsersPage()
    {
        if (_session.IsPreview)
        {
            var preview = new StackPanel();
            preview.Children.Add(SectionHeading("User accounts"));
            preview.Children.Add(Banner("Sign in required", "Sign in with an account that has user administration access to manage accounts.", "#D97706"));
            return preview;
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var searchIcon = new FluentIcon
        {
            Icon = FluentIcons.Common.Icon.Search,
            IconSize = IconSize.Size16,
            IconVariant = IconVariant.Regular,
            FontSize = 16,
            Width = 16,
            Height = 16,
            Foreground = CreateBrush("#64748B"),
            Margin = new Thickness(8, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(searchIcon, 0);
        searchGrid.Children.Add(searchIcon);

        _userAccountsSearchBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 2, 8, 2),
            ToolTip = "Search name, employee ID, email or department"
        };
        var searchPlaceholder = new TextBlock
        {
            Text = "Search...",
            FontSize = 13,
            Foreground = CreateBrush("#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            IsHitTestVisible = false,
            Visibility = string.IsNullOrEmpty(_userAccountsSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed
        };
        _userAccountsSearchBox.TextChanged += (_, _) =>
        {
            searchPlaceholder.Visibility = string.IsNullOrEmpty(_userAccountsSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplyUserAccountFilter();
        };

        var inputHost = new Grid();
        inputHost.Children.Add(searchPlaceholder);
        inputHost.Children.Add(_userAccountsSearchBox);
        Grid.SetColumn(inputHost, 1);
        searchGrid.Children.Add(inputHost);

        var searchBoxContainer = new Border
        {
            Width = 180,
            Height = 34,
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusSmall"),
            BorderBrush = CreateBrush("#CFD9E5"),
            BorderThickness = new Thickness(1),
            Background = Brushes.White,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = searchGrid
        };

        var statuses = new[] { "All statuses" }.Concat(_userAccounts.Select(user => user.Status)
                .Where(status => !string.IsNullOrWhiteSpace(status)).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(status => status, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        _userAccountsStatusFilter = new ComboBox
        {
            ItemsSource = statuses,
            SelectedIndex = 0,
            Width = 125,
            Height = 34,
            MinHeight = 34,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _userAccountsStatusFilter.SelectionChanged += (_, _) => ApplyUserAccountFilter();

        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(searchBoxContainer);
        actions.Children.Add(_userAccountsStatusFilter);

        var refresh = UserIconButton("Refresh users", FluentIcons.Common.Icon.ArrowClockwise);
        refresh.Click += async (_, _) => await LoadRemoteWorkspaceAsync();
        actions.Children.Add(refresh);

        var edit = UserIconButton("Edit selected user", FluentIcons.Common.Icon.Edit);
        edit.IsEnabled = false;
        edit.Click += async (_, _) => await EditUserAccountAsync();
        actions.Children.Add(edit);

        var delete = UserIconButton("Delete selected user", FluentIcons.Common.Icon.Delete);
        delete.IsEnabled = false;
        delete.Click += async (_, _) => await DeleteUserAccountAsync();
        actions.Children.Add(delete);

        var create = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new FluentIcon { Icon = FluentIcons.Common.Icon.Add, IconSize = IconSize.Size16, IconVariant = IconVariant.Regular, Width = 16, Height = 16, Margin = new Thickness(0, 0, 7, 0) },
                    new TextBlock { Text = "New user", VerticalAlignment = VerticalAlignment.Center }
                }
            },
            IsEnabled = UserHasPermission(UserCreatePermission),
            Style = (Style)FindResource("PrimaryButton"),
            Height = 34,
            MinWidth = 112,
            Padding = new Thickness(14, 4, 14, 4),
            Margin = new Thickness(0, 0, 0, 0),
            ToolTip = UserHasPermission(UserCreatePermission) ? "Create a user account" : "Requires users:create permission"
        };
        create.Click += async (_, _) => await EditUserAccountAsync(create: true);
        actions.Children.Add(create);

        var accountBadge = UiTokens.CreateBadge(
            $"{_userAccounts.Count} enrolled accounts",
            UiTokens.Brushes.MintSurface,
            UiTokens.Brushes.Accent,
            UiTokens.Brushes.MintBorder,
            UiTokens.Radii.Medium,
            new Thickness(10, 3, 10, 3));
        accountBadge.VerticalAlignment = VerticalAlignment.Center;

        var heroHeader = CreatePageHeroHeader(
            string.Empty,
            "User Accounts",
            "Create, update and maintain user credentials, login identities, and role assignments.",
            FluentIcons.Common.Icon.People,
            actions,
            titleBadge: accountBadge);
        Grid.SetRow(heroHeader, 0);
        root.Children.Add(heroHeader);

        // Row 1: Unified Table Card with DataGrid
        var tableCard = new Border
        {
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            ClipToBounds = true
        };
        var tableDock = new DockPanel();

        if (_pageMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            _pageMessage.StartsWith("User account", StringComparison.OrdinalIgnoreCase))
        {
            var isError = _pageMessage.StartsWith("Failed", StringComparison.OrdinalIgnoreCase);
            var noticeBorder = new Border
            {
                Background = isError ? new SolidColorBrush(Color.FromRgb(254, 242, 242)) : new SolidColorBrush(Color.FromRgb(240, 253, 244)),
                BorderBrush = isError ? new SolidColorBrush(Color.FromRgb(254, 202, 202)) : new SolidColorBrush(Color.FromRgb(187, 247, 208)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(14, 6, 14, 6)
            };
            var notice = Label(_pageMessage, UiTokens.FontSizes.Caption, isError ? UiTokens.Brushes.Danger : UiTokens.Brushes.Accent, UiTokens.Weights.SemiBold);
            noticeBorder.Child = notice;
            DockPanel.SetDock(noticeBorder, Dock.Top);
            tableDock.Children.Add(noticeBorder);
        }

        var rows = FilterUserAccounts();
        var grid = CreateSettingsDataGrid(rows, minHeight: 150, maxHeight: double.PositiveInfinity);
        grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.DisplayName)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Employee ID", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.EmployeeId)), Width = new DataGridLength(.7, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Username", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.Username)), Width = new DataGridLength(.8, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Email", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.Email)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Department / position", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.DepartmentPosition)), Width = new DataGridLength(1.1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Role", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.RoleName)), Width = new DataGridLength(.9, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new System.Windows.Data.Binding(nameof(UserAccountRecord.Status)), Width = new DataGridLength(.7, DataGridLengthUnitType.Star) });
        grid.SelectionChanged += (_, _) =>
        {
            var selected = grid.SelectedItem as UserAccountRecord;
            edit.IsEnabled = selected is not null && UserHasPermission(UserUpdatePermission);
            delete.IsEnabled = selected is not null && !selected.IsAdministrator && UserHasPermission(UserDeletePermission);
            delete.ToolTip = selected?.IsAdministrator == true
                ? "Administrator accounts cannot be deleted"
                : UserHasPermission(UserDeletePermission) ? "Delete selected user" : "Requires users:delete permission";
        };
        grid.MouseDoubleClick += async (_, _) => { if (grid.SelectedItem is UserAccountRecord) await EditUserAccountAsync(); };
        grid.VerticalAlignment = VerticalAlignment.Stretch;
        _userAccountsGrid = grid;

        tableDock.Children.Add(grid);
        tableCard.Child = tableDock;

        Grid.SetRow(tableCard, 1);
        root.Children.Add(tableCard);
        return root;
    }

    private Button UserIconButton(string title, FluentIcons.Common.Icon icon)
    {
        var button = new Button
        {
            Content = new FluentIcon { Icon = icon, IconSize = IconSize.Size20, IconVariant = IconVariant.Regular,
                Width = 20, Height = 20, Foreground = (Brush)FindResource("MutedBrush") },
            Width = 36,
            Height = 34,
            MinWidth = 36,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 7, 0),
            ToolTip = title,
            Style = (Style)FindResource(typeof(Button))
        };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        return button;
    }

    private List<UserAccountRecord> FilterUserAccounts()
    {
        var query = _userAccountsSearchBox?.Text.Trim() ?? "";
        var status = _userAccountsStatusFilter?.SelectedItem?.ToString() ?? "All statuses";
        return _userAccounts.Where(user =>
                (status == "All statuses" || user.Status.Equals(status, StringComparison.OrdinalIgnoreCase)) &&
                (query.Length == 0 || new[] { user.DisplayName, user.EmployeeId, user.Username, user.Email, user.Department, user.Position, user.RoleName }
                    .Any(value => value.Contains(query, StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    private void ApplyUserAccountFilter()
    {
        if (_userAccountsGrid is null) return;
        var selectedId = (_userAccountsGrid.SelectedItem as UserAccountRecord)?.Id;
        _userAccountsGrid.ItemsSource = FilterUserAccounts();
        _userAccountsGrid.SelectedItem = _userAccountsGrid.Items.Cast<UserAccountRecord>().FirstOrDefault(user => user.Id == selectedId);
    }

    private async Task EditUserAccountAsync(bool create = false)
    {
        var current = create ? null : _userAccountsGrid?.SelectedItem as UserAccountRecord;
        if (!create && current is null) return;
        if (_session.IsPreview) return;
        if (!UserHasPermission(create ? UserCreatePermission : UserUpdatePermission))
        {
            MessageBox.Show(this, $"This action requires { (create ? UserCreatePermission : UserUpdatePermission) } permission.",
                "Access restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var roles = _userRoleChoices.ToList();
        if (!roles.Any(role => role.Name.Equals("User", StringComparison.OrdinalIgnoreCase)))
            roles.Insert(0, new UserRoleChoice("", "User"));
        if (current is not null && !roles.Any(role => role.Id == current.RoleId && role.Name.Equals(current.RoleName, StringComparison.OrdinalIgnoreCase)))
            roles.Add(new UserRoleChoice(current.RoleId, current.RoleName));

        var editor = new UserAccountEditorWindow(current, roles) { Owner = this };
        if (editor.ShowDialog() != true || editor.Payload is null) return;
        try
        {
            var result = create
                ? await _apiClient.PostJsonAsync("/api/users", editor.Payload, _session.Token)
                : await _apiClient.PatchJsonAsync($"/api/users/{Uri.EscapeDataString(current!.Id)}", editor.Payload, _session.Token);
            if (!result.Success) throw new InvalidOperationException(UserApiError(result.Error, result.StatusCode));
            await LoadRemoteWorkspaceAsync();
            _pageMessage = create ? "User account created." : "User account updated.";
            RenderCurrentPage();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, create ? "Create user failed" : "Update user failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteUserAccountAsync()
    {
        var user = _userAccountsGrid?.SelectedItem as UserAccountRecord;
        if (user is null || user.IsAdministrator || _session.IsPreview) return;
        if (!UserHasPermission(UserDeletePermission))
        {
            MessageBox.Show(this, $"This action requires {UserDeletePermission} permission.", "Access restricted",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (MessageBox.Show(this,
                $"Permanently delete the account for {user.DisplayName} ({user.Email})? This cannot be undone.",
                "Delete user account", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        var result = await _apiClient.DeleteAsync($"/api/users/{Uri.EscapeDataString(user.Id)}", _session.Token);
        if (!result.Success)
        {
            MessageBox.Show(this, UserApiError(result.Error, result.StatusCode), "Delete user failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        await LoadRemoteWorkspaceAsync();
        _pageMessage = "User account deleted.";
        RenderCurrentPage();
    }

    private static string UserApiError(string error, int statusCode)
    {
        if (statusCode == 401) return "Your session has expired. Sign in again to continue.";
        if (statusCode == 403) return "The server denied this action. Check the users permission assigned to your role.";
        return string.IsNullOrWhiteSpace(error) ? $"The user request failed (HTTP {statusCode})." : error;
    }
}

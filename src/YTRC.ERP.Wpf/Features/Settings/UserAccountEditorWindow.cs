using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

internal sealed class UserAccountEditorWindow : Window
{
    private readonly UserAccountRecord? _source;
    private readonly ComboBox _role;
    private readonly ComboBox _status;
    private readonly PasswordBox _password;
    private readonly TextBox _displayName;
    private readonly TextBox _employeeId;
    private readonly TextBox _username;
    private readonly TextBox _email;
    private readonly TextBox _department;
    private readonly TextBox _position;
    private readonly TextBox _phone;

    public Dictionary<string, object?>? Payload { get; private set; }

    public UserAccountEditorWindow(UserAccountRecord? source, IReadOnlyList<UserRoleChoice> roles)
    {
        _source = source;
        var isCreate = source is null;
        Title = isCreate ? "Create user account" : "Edit user account";
        Width = 780;
        Height = 670;
        MinWidth = 650;
        MinHeight = 580;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#F5F7FA");

        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        heading.Children.Add(new TextBlock
        {
            Text = isCreate ? "Create user account" : "Edit user account",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E")
        });
        root.Children.Add(heading);

        var form = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 5; row++) form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _displayName = Input(source?.DisplayName);
        _employeeId = Input(source?.EmployeeId);
        _username = Input(source?.Username);
        _email = Input(source?.Email);
        _department = Input(source?.Department);
        _position = Input(source?.Position);
        _phone = Input(source?.Phone);
        _password = new PasswordBox { Height = 34, MinHeight = 34, ToolTip = isCreate ? "Required for a new account" : "Leave blank to keep the current password" };
        _role = new ComboBox
        {
            ItemsSource = roles,
            DisplayMemberPath = nameof(UserRoleChoice.Name),
            Height = 34,
            MinHeight = 34
        };
        var selectedRole = roles.FirstOrDefault(item => item.Id == source?.RoleId && item.Name.Equals(source?.RoleName, StringComparison.OrdinalIgnoreCase))
                           ?? roles.FirstOrDefault(item => item.Name.Equals(source?.RoleName ?? "User", StringComparison.OrdinalIgnoreCase))
                           ?? roles.FirstOrDefault(item => item.Name.Equals("User", StringComparison.OrdinalIgnoreCase));
        _role.SelectedItem = selectedRole ?? roles.FirstOrDefault();

        var statuses = new List<string> { "ACTIVE", "INACTIVE" };
        if (!string.IsNullOrWhiteSpace(source?.Status) && !statuses.Contains(source.Status, StringComparer.OrdinalIgnoreCase))
            statuses.Insert(0, source.Status);
        _status = new ComboBox { ItemsSource = statuses, SelectedItem = source?.Status ?? "ACTIVE", Height = 34, MinHeight = 34 };

        AddField(form, "Display name", _displayName, 0, 0);
        AddField(form, "Employee ID", _employeeId, 0, 1);
        AddField(form, "Username", _username, 1, 0);
        AddField(form, "Email *", _email, 1, 1);
        AddField(form, "Department", _department, 2, 0);
        AddField(form, "Position", _position, 2, 1);
        AddField(form, isCreate ? "Password *" : "New password", _password, 3, 0);
        AddField(form, "Role", _role, 3, 1);
        AddField(form, "Phone", _phone, 4, 0);
        AddField(form, "Status", _status, 4, 1);

        var formCard = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#E1E7EF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = form
        };
        var scroller = new ScrollViewer { Content = formCard, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroller);
        Grid.SetRow(scroller, 1);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", MinWidth = 104, Height = 36, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        var save = new Button
        {
            Content = isCreate ? "Create user" : "Save changes",
            MinWidth = 124,
            Height = 36,
            Style = (Style)System.Windows.Application.Current.FindResource("PrimaryButton")
        };
        save.Click += (_, _) => Save();
        footer.Children.Add(cancel);
        footer.Children.Add(save);
        root.Children.Add(footer);
        Grid.SetRow(footer, 2);
        Content = root;
    }

    private static void AddField(Grid form, string label, Control input, int row, int column)
    {
        var stack = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 8, 0, column == 0 ? 8 : 0, 12) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#4E5F74"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        stack.Children.Add(input);
        form.Children.Add(stack);
        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, column);
    }

    private static TextBox Input(string? value) => new() { Text = value ?? "", Height = 34, MinHeight = 34 };

    private void Save()
    {
        var email = _email.Text.Trim();
        if (email.Length == 0 || !email.Contains('@') || email.EndsWith('@') || email.Contains(' '))
        {
            MessageBox.Show(this, "Enter a valid email address.", "Check user details", MessageBoxButton.OK, MessageBoxImage.Warning);
            _email.Focus();
            return;
        }
        if (_source is null && string.IsNullOrWhiteSpace(_password.Password))
        {
            MessageBox.Show(this, "A password is required for a new user.", "Check user details", MessageBoxButton.OK, MessageBoxImage.Warning);
            _password.Focus();
            return;
        }

        var selectedRole = _role.SelectedItem as UserRoleChoice;
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["displayName"] = Clean(_displayName.Text),
            ["employeeId"] = Clean(_employeeId.Text),
            ["username"] = Clean(_username.Text),
            ["email"] = email,
            ["department"] = Clean(_department.Text),
            ["position"] = Clean(_position.Text),
            ["phone"] = Clean(_phone.Text),
            ["role"] = selectedRole?.Name ?? "User",
            ["roleId"] = string.IsNullOrWhiteSpace(selectedRole?.Id) ? null : selectedRole.Id,
            ["status"] = _status.SelectedItem?.ToString() ?? "ACTIVE"
        };
        if (_source is null || !string.IsNullOrWhiteSpace(_password.Password))
            payload["password"] = _password.Password;
        Payload = payload;
        DialogResult = true;
        Close();
    }

    private static string? Clean(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}

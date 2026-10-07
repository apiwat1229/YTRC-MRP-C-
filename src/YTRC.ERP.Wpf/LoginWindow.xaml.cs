using System.Windows;
using YTRC.ERP.Application.Contracts;
using YTRC.ERP.Domain.Configuration;
using YTRC.ERP.Domain.Models;

namespace YTRC.ERP.Wpf;

public partial class LoginWindow : Window
{
    private readonly IAuthenticationUseCase _signInUseCase;
    private bool _isPasswordVisible;
    private bool _syncingPassword;
    public static UserSession? Session { get; private set; }
    public static void ResetSession() => Session = null;

    public LoginWindow(IAuthenticationUseCase signInUseCase)
    {
        _signInUseCase = signInUseCase;
        InitializeComponent();
        UsernameBox.Text = AppSettings.Current.SavedUsername;
        RememberBox.IsChecked = AppSettings.Current.RememberUsername;
        if (UsernameBox.Text.Length > 0) PasswordBox.Focus();
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameBox.Text.Trim();
        var password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;
        if (username.Length == 0) { StatusText.Text = "Enter your username or email."; UsernameBox.Focus(); return; }
        if (password.Length == 0)
        {
            StatusText.Text = "Enter your password.";
            FocusPasswordInput();
            return;
        }

        SetBusy(true, "Signing in…");
        var baseUrl = ServerUrl.NormalizeBaseUrl(AppSettings.Current.ApiBaseUrl);
        var result = await _signInUseCase.SignInAsync(baseUrl, username, password);
        if (!result.Success || result.User is null)
        {
            SetBusy(false, result.ErrorMessage);
            if (_isPasswordVisible) { PasswordTextBox.Focus(); PasswordTextBox.SelectAll(); }
            else { PasswordBox.Focus(); PasswordBox.SelectAll(); }
            return;
        }

        AppSettings.Current.ApiBaseUrl = baseUrl;
        AppSettings.Current.RememberUsername = RememberBox.IsChecked == true;
        AppSettings.Current.SavedUsername = AppSettings.Current.RememberUsername ? username : "";
        App.SettingsStore.Save(AppSettings.Current);
        Session = new UserSession(result.User, result.Token);
        SetBusy(false, "Signed in.");
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void PasswordVisibility_Click(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordBox.Visibility = _isPasswordVisible ? Visibility.Collapsed : Visibility.Visible;
        PasswordTextBox.Visibility = _isPasswordVisible ? Visibility.Visible : Visibility.Collapsed;
        PasswordVisibilityButton.Content = _isPasswordVisible ? "Hide" : "Show";
        PasswordVisibilityButton.ToolTip = _isPasswordVisible ? "Hide password" : "Show password";
        FocusPasswordInput();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _syncingPassword = true;
        PasswordTextBox.Text = PasswordBox.Password;
        _syncingPassword = false;
    }

    private void PasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_syncingPassword) return;
        _syncingPassword = true;
        PasswordBox.Password = PasswordTextBox.Text;
        _syncingPassword = false;
    }

    private void FocusPasswordInput()
    {
        if (_isPasswordVisible)
        {
            PasswordTextBox.Focus();
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
        }
        else
        {
            PasswordBox.Focus();
        }
    }

    private void SetBusy(bool busy, string message)
    {
        SignInButton.IsEnabled = !busy;
        UsernameBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy;
        PasswordTextBox.IsEnabled = !busy;
        PasswordVisibilityButton.IsEnabled = !busy;
        RememberBox.IsEnabled = !busy;
        StatusText.Text = message;
        StatusText.Foreground = busy ? System.Windows.Media.Brushes.SlateGray :
            message is "Signed in." ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.IndianRed;
    }
}

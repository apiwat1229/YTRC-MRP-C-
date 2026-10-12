namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private bool _isPromptingReauth;

    private async Task RefreshConnectionBadgeAsync()
    {
        var health = await _apiClient.TestConnectionAsync(AppSettings.Current.ApiBaseUrl);
        _apiOnline = health.IsOnline;
        SetConnection(health.IsOnline, health.IsOnline ? $"API connected · v{health.Version}" : "API offline");
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        LoginWindow.ResetSession();
        Close();
    }

    private void ApiClient_SessionExpired(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (_isPromptingReauth || _session.IsPreview) return;
            _isPromptingReauth = true;
            try
            {
                MessageBox.Show(this, "เซสชันของคุณหมดอายุแล้ว (Session Expired) กรุณาเข้าสู่ระบบใหม่อีกครั้ง",
                    "Session Expired", MessageBoxButton.OK, MessageBoxImage.Information);
                var login = new LoginWindow(App.SignInUseCase) { Owner = this };
                if (login.ShowDialog() == true && LoginWindow.Session is not null)
                {
                    _session = LoginWindow.Session;
                    UserNameText.Text = _session.User.FullName;
                    RoleText.Text = _session.IsPreview ? "Guest preview · read only" : _session.User.RoleName;
                    await RefreshConnectionAsync();
                    RenderCurrentPage();
                }
            }
            finally
            {
                _isPromptingReauth = false;
            }
        });
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        _apiClient.SessionExpired -= ApiClient_SessionExpired;
        _scaleService.ReadingChanged -= ScaleService_ReadingChanged;
        await _scaleService.DisconnectAsync(0);
        await _scaleService.DisconnectAsync(1);
    }
}

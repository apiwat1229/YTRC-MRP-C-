namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
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

    private async void Window_Closed(object? sender, EventArgs e)
    {
        _scaleService.ReadingChanged -= ScaleService_ReadingChanged;
        await _scaleService.DisconnectAsync(0);
        await _scaleService.DisconnectAsync(1);
    }
}

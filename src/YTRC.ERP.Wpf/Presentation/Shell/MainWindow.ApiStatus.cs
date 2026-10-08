namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private async Task LoadRemoteWorkspaceAsync()
    {
        if (_session.IsPreview) { _pageMessage = "Sign in to load live records."; _activeRows = []; RenderCurrentPage(); return; }
        var endpoint = _settingsSection switch { "Users" => "/api/users", "Roles" => "/api/roles", _ => "/api/access-control/staff" };
        _pageMessage = "Loading data from the ERP API…"; _activeRows = []; RenderCurrentPage();
        var result = await _apiClient.GetAsync(endpoint, _session.Token);
        if (!result.Success && _settingsSection == "Staff" && result.StatusCode == 404)
            result = await _apiClient.GetAsync("/api/users", _session.Token);
        if (!result.Success)
        {
            _pageMessage = $"Failed to load data: {result.Error}";
            _activeRows = [];
        }
        else
        {
            _activeRows = ApiRowMapper.Map(result.Data, _settingsSection);
            _pageMessage = _activeRows.Count == 0 ? "The server returned no records." : $"Loaded {_activeRows.Count} records from {AppSettings.Current.ApiBaseUrl}.";
        }
        RenderCurrentPage();
    }

    private async Task RefreshConnectionAsync()
    {
        SetConnection(false, "Checking API…");
        var health = await _apiClient.TestConnectionAsync(AppSettings.Current.ApiBaseUrl);
        _apiOnline = health.IsOnline;
        SetConnection(_apiOnline, _apiOnline ? $"API connected · v{health.Version}" : "API offline");
        if (!_apiOnline && _currentPage == "Settings" && _settingsSection == "System Config")
            _pageMessage = health.Message;
        if (_currentPage is "Home" or "Purchase") RenderCurrentPage();
    }

    private void SetConnection(bool online, string message)
    {
        BackstageConnectionText.Text = message;
        BackstageConnectionText.Foreground = online ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(82, 98, 119));
    }
}

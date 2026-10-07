namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private void MainRibbon_SelectedTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUiInitialized || sender is not Fluent.Ribbon ribbon || ribbon.SelectedTabItem?.Tag is not string page) return;
        NavigateToPage(page);
    }

    private void NavigateToPage(string page)
    {
        if (string.Equals(_currentPage, page, StringComparison.OrdinalIgnoreCase)) return;

        if (page == "Settings")
        {
            _settingsSection = "System Config";
            _activeRows = [];
        }

        _currentPage = page;
        _pageMessage = "";
        RenderCurrentPage();
    }

    private bool TrySelectRibbonTab(string page)
    {
        var tab = MainRibbon.Tabs
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), page, StringComparison.OrdinalIgnoreCase));
        if (tab is null) return false;
        if (ReferenceEquals(MainRibbon.SelectedTabItem, tab)) NavigateToPage(page);
        else MainRibbon.SelectedTabItem = tab;
        return true;
    }

    private void NavigateToSettings(string section)
    {
        if (_currentPage != "Settings")
        {
            _settingsSection = "System Config";
            _activeRows = [];
        }

        TrySelectRibbonTab("Settings");
        _currentPage = "Settings";
        _settingsSection = section;
        _pageMessage = "";
        RenderCurrentPage();
        if (section is "Users" or "Roles" or "Staff") _ = LoadRemoteWorkspaceAsync();
    }

    private void RenderCurrentPage()
    {
        if (ExportCsvButton is not null)
            ExportCsvButton.IsEnabled = _currentPage == "Settings" &&
                _settingsSection is ("Users" or "Roles" or "Staff") && _activeRows.Count > 0;

        if (_currentPage == "Home") WorkspaceHost.Content = BuildDashboard();
        else if (_currentPage == "Settings") WorkspaceHost.Content = BuildSettingsPage();
        else if (_currentPage == "Search") WorkspaceHost.Content = BuildSearchResults(SearchBox.Text.Trim());
        else if (Enum.TryParse<DepartmentId>(_currentPage, out var dept)) WorkspaceHost.Content = BuildDepartmentPage(dept);
        else WorkspaceHost.Content = BuildSearchResults(_currentPage);
    }

    private async void RibbonAction_Click(object sender, RoutedEventArgs e)
    {
        var action = (sender as FrameworkElement)?.Tag?.ToString() ?? "";
        if (action == "Dashboard")
        {
            _currentPage = "Home";
            _pageMessage = "";
            TrySelectRibbonTab("Home");
            RenderCurrentPage();
        }
        else if (action == "Refresh")
        {
            if (_currentPage == "Home" || _currentPage == "Settings" && _settingsSection == "System Config") await RefreshConnectionAsync();
            else if (_currentPage == "Settings" && _settingsSection is "Users" or "Roles" or "Staff") await LoadRemoteWorkspaceAsync();
            else SetPageMessage("The source project does not yet define live transaction queries for this workspace.");
        }
        else if (action == "Reports") SetPageMessage("Report endpoints and report layouts are not defined in the source project yet.");
        else if (action == "Export") ExportRows();
        else if (action.StartsWith("Settings:", StringComparison.Ordinal)) NavigateToSettings(action["Settings:".Length..]);
        else if (action == "New") SetPageMessage("New transactions remain unavailable until the ERP API workflow contracts are implemented.");
    }
}

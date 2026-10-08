namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private void MainRibbon_Loaded(object sender, RoutedEventArgs e)
    {
        if (MainRibbon.TitleBar is not { } titleBar) return;
        titleBar.Header = "YTRC-MRP";
        titleBar.HeaderAlignment = HorizontalAlignment.Left;
        titleBar.IsCollapsed = false;
    }

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
        _remoteWorkspaceGrid = null;
        if (ExportCsvButton is not null)
            ExportCsvButton.IsEnabled = _currentPage == "Settings" &&
                _settingsSection is ("Users" or "Roles" or "Staff") && _activeRows.Count > 0;
        if (DataToolsGroup is not null)
            DataToolsGroup.Visibility = _currentPage == "Settings" &&
                _settingsSection is ("Users" or "Roles" or "Staff") && _activeRows.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (_currentPage == "Home") WorkspaceHost.Content = BuildDashboard();
        else if (_currentPage == "Settings") WorkspaceHost.Content = BuildSettingsPage();
        else if (_currentPage == DepartmentId.Purchase.ToString()) WorkspaceHost.Content = BuildPurchasingPage();
        else if (_currentPage == "Search") WorkspaceHost.Content = BuildSearchResults(SearchBox.Text.Trim());
        else if (Enum.TryParse<DepartmentId>(_currentPage, out var dept)) WorkspaceHost.Content = BuildDepartmentPage(dept);
        else WorkspaceHost.Content = BuildSearchResults(_currentPage);
    }

    private void PurchasingDropDown_Opened(object? sender, EventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag?.ToString();
        if (tag?.StartsWith("Purchasing:", StringComparison.Ordinal) == true)
            SelectPurchasingSection(tag["Purchasing:".Length..]);
    }

    private void PurchasingDropDown_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Fluent.DropDownButton button) return;

        button.ApplyTemplate();
        if (button.Template.FindName("PART_ButtonBorder", button) is Border chrome)
            chrome.CornerRadius = (CornerRadius)FindResource("AppCornerRadius");
    }

    private void PurchasingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag?.ToString();
        if (tag is null || !tag.StartsWith("PurchasingAction:", StringComparison.Ordinal)) return;

        var actionParts = tag["PurchasingAction:".Length..].Split(':', 2);
        if (actionParts.Length == 2)
            SelectPurchasingAction(actionParts[0], actionParts[1]);
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
        else if (action == "Search") Search_Click(sender, e);
        else if (action == "SignOut") SignOut_Click(sender, e);
        else if (action.StartsWith("Purchasing:", StringComparison.Ordinal))
            SelectPurchasingSection(action["Purchasing:".Length..]);
        else if (action == "Refresh")
        {
            if (_currentPage == "Home" || _currentPage == "Settings" && _settingsSection == "System Config") await RefreshConnectionAsync();
            else if (_currentPage == "Settings" && _settingsSection is "Users" or "Roles" or "Staff") await LoadRemoteWorkspaceAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection is "Cuplump" or "USS") await LoadBookingQueueAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection is "Supplier" or "Rubber Type" or "Order") await LoadPurchasingRecordsAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString()) await RefreshConnectionAsync();
            else SetPageMessage("The source project does not yet define live transaction queries for this workspace.");
        }
        else if (action == "Reports") SetPageMessage("Report endpoints and report layouts are not defined in the source project yet.");
        else if (action == "Export") ExportRows();
        else if (action == "AutoFitColumns")
        {
            if (_remoteWorkspaceGrid is not null)
                foreach (var column in _remoteWorkspaceGrid.Columns) column.Width = DataGridLength.Auto;
        }
        else if (action.StartsWith("Settings:", StringComparison.Ordinal)) NavigateToSettings(action["Settings:".Length..]);
        else if (action == "New") SetPageMessage("New transactions remain unavailable until the ERP API workflow contracts are implemented.");
    }

    private void SearchGroupLauncher_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show($"YTRC-MRP Desktop\nClient version {ApplicationInfo.Version}\n\nDepartment workspaces and local administration tools.",
            "About YTRC-MRP", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

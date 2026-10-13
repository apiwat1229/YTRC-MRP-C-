namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private void MainRibbon_Loaded(object sender, RoutedEventArgs e)
    {
        if (MainRibbon.TitleBar is { } titleBar)
        {
            titleBar.Header = "YTRC-MRP";
            titleBar.HeaderAlignment = HorizontalAlignment.Left;
            titleBar.IsCollapsed = false;
        }

        MainRibbon.IsMinimized = true;
    }

    private void MainRibbon_SelectedTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUiInitialized) return;
        // When clicking tabs on the Titlebar / ribbon tab header, switch the ribbon tab only without redirecting the page.
        // Navigation should only occur when the user explicitly clicks a command in the Ribbon bar.
    }

    private void NavigateToPage(string page)
    {
        if (string.Equals(page, "TruckScale", StringComparison.OrdinalIgnoreCase))
            page = TruckScalePageId;
        if (string.Equals(_currentPage, page, StringComparison.OrdinalIgnoreCase)) return;

        if (string.Equals(page, DepartmentId.Receiving.ToString(), StringComparison.OrdinalIgnoreCase))
            _receivingLoadedDate = DateTime.MinValue;

        if (page == "Settings")
        {
            _settingsSection = "System Config";
            _activeRows = [];
        }

        _currentPage = page;
        _pageMessage = "";
        RenderCurrentPage();
        if (string.Equals(page, DepartmentId.Receiving.ToString(), StringComparison.OrdinalIgnoreCase))
            _ = LoadReceivingAsync(force: true);
    }

    private bool TrySelectRibbonTab(string page)
    {
        var tab = MainRibbon.Tabs
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), page, StringComparison.OrdinalIgnoreCase));
        if (tab is null) return false;
        MainRibbon.SelectedTabItem = tab;
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
        if (section is "Users" or "Staff") _ = LoadRemoteWorkspaceAsync();
        else if (IsSecuritySettingsSection(section)) _ = LoadSecuritySettingsAsync();
    }

    private void RenderCurrentPage()
    {
        _remoteWorkspaceGrid = null;
        var isTruckScalePage = _currentPage is TruckScalePageId or TruckScaleSummaryPageId;
        if (!isTruckScalePage && _truckScaleRefreshTimer.IsEnabled) _truckScaleRefreshTimer.Stop();
        var isFillPage = isTruckScalePage || _currentPage == "Settings" ||
            (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection is "Marking Preparation" or "SI Export");
        WorkspaceScrollViewer.VerticalScrollBarVisibility = isFillPage
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
        var hasSettingsRecords = _currentPage == "Settings" &&
            _settingsSection is ("Users" or "Roles" or "Staff") && _activeRows.Count > 0;
        if (ExportCsvButton is not null)
            ExportCsvButton.IsEnabled = hasSettingsRecords;
        if (SettingsRecordsGroup is not null)
            SettingsRecordsGroup.Visibility = hasSettingsRecords ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsLayoutGroup is not null)
            SettingsLayoutGroup.Visibility = hasSettingsRecords ? Visibility.Visible : Visibility.Collapsed;

        UIElement pageContent;
        if (_currentPage == "Home") pageContent = BuildDashboard();
        else if (_currentPage == "Settings") pageContent = BuildSettingsPage();
        else if (_currentPage == DepartmentId.Purchase.ToString()) pageContent = BuildPurchasingPage();
        else if (_currentPage is TruckScalePageId or TruckScaleSummaryPageId)
            pageContent = BuildTruckScalePage(_currentPage == TruckScaleSummaryPageId ? "queue-summary" : "check-in");
        else if (_currentPage == CuplumpReceivingPageId) pageContent = BuildReceivingPage();
        else if (_currentPage == "RRC:Scales") pageContent = BuildRrcScaleSettingsPage();
        else if (_currentPage == "Search") pageContent = BuildSearchResults(SearchBox.Text.Trim());
        else if (Enum.TryParse<DepartmentId>(_currentPage, out var dept)) pageContent = BuildDepartmentPage(dept);
        else pageContent = BuildSearchResults(_currentPage);

        ShowWorkspacePage(pageContent, isFillPage);
    }

    private void ShowWorkspacePage(UIElement pageContent, bool isFillPage)
    {
        PinnedPageHeaderHost.Content = null;
        PinnedPageHeaderHost.Visibility = Visibility.Collapsed;

        // Fill pages own a fixed-height workspace with their own layout / scrolling; do not rip out their headers.
        if (!isFillPage && FindStickyPageHeader(pageContent) is { } pageHeader && RemoveStickyPageHeader(pageHeader))
        {
            PinnedPageHeaderHost.Content = pageHeader;
            PinnedPageHeaderHost.Visibility = Visibility.Visible;

            if (pageContent is FrameworkElement root)
                root.Margin = new Thickness(root.Margin.Left, 0, root.Margin.Right, root.Margin.Bottom);
        }

        WorkspaceHost.Content = pageContent;
        WorkspaceScrollViewer.ScrollToTop();
    }

    private static FrameworkElement? FindStickyPageHeader(DependencyObject root)
    {
        if (root is FrameworkElement element && Equals(element.Tag, StickyPageHeaderTag))
            return element;

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (FindStickyPageHeader(child) is { } header)
                return header;

        return null;
    }

    private static bool RemoveStickyPageHeader(FrameworkElement header)
    {
        switch (LogicalTreeHelper.GetParent(header))
        {
            case Panel panel when panel.Children.Contains(header):
                panel.Children.Remove(header);
                return true;
            case Decorator decorator when ReferenceEquals(decorator.Child, header):
                decorator.Child = null;
                return true;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, header):
                contentControl.Content = null;
                return true;
            case ContentPresenter contentPresenter when ReferenceEquals(contentPresenter.Content, header):
                contentPresenter.Content = null;
                return true;
            case ItemsControl itemsControl when itemsControl.Items.Contains(header):
                itemsControl.Items.Remove(header);
                return true;
            default:
                return false;
        }
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
            chrome.CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium");
    }

    private void PurchasingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag?.ToString();
        if (tag is null || !tag.StartsWith("PurchasingAction:", StringComparison.Ordinal)) return;

        var actionParts = tag["PurchasingAction:".Length..].Split(':', 2);
        if (actionParts.Length == 2)
        {
            CollapseRibbonAfterCommand();
            SelectPurchasingAction(actionParts[0], actionParts[1]);
        }
    }

    private async void RibbonAction_Click(object sender, RoutedEventArgs e)
    {
        var action = (sender as FrameworkElement)?.Tag?.ToString() ?? "";
        CollapseRibbonAfterCommand();
        if (action == "Dashboard")
        {
            _currentPage = "Home";
            _pageMessage = "";
            TrySelectRibbonTab("Home");
            RenderCurrentPage();
        }
        else if (action == "Search") Search_Click(sender, e);
        else if (action == "SignOut") SignOut_Click(sender, e);
        else if (action is "Production:WorkOrders" or "Production")
        {
            TrySelectRibbonTab("Production");
            NavigateToPage("Production");
        }
        else if (action is "QA:Inspection" or "QA")
        {
            TrySelectRibbonTab("QA");
            NavigateToPage("QA");
        }
        else if (action is "Warehouse:Inventory" or "Warehouse")
        {
            TrySelectRibbonTab("Warehouse");
            NavigateToPage("Warehouse");
        }
        else if (action is "Maintenance:Hub" or "Maintenance")
        {
            TrySelectRibbonTab("Maintenance");
            NavigateToPage("Maintenance");
        }
        else if (action is "IT:Support" or "IT")
        {
            TrySelectRibbonTab("IT");
            NavigateToPage("IT");
        }
        else if (action.StartsWith("Purchasing:", StringComparison.Ordinal))
            SelectPurchasingSection(action["Purchasing:".Length..]);
        else if (action is "RRC:Cuplump:TruckScale" or "RRC:Cuplump:QueueSummary" or
                 "RRC:USS:TruckScale" or "RRC:USS:QueueSummary")
        {
            var parts = action.Split(':');
            OpenTruckScalePage(parts[2] == "QueueSummary" ? "queue-summary" : "check-in",
                parts[1].Equals("USS", StringComparison.OrdinalIgnoreCase) ? "uss" : "cuplump");
        }
        else if (action == "RRC:Cuplump:Receiving")
        {
            TrySelectRibbonTab(DepartmentId.Receiving.ToString());
            if (!_session.User.HasDepartmentAccess(DepartmentId.Receiving))
            {
                _currentPage = DepartmentId.Receiving.ToString();
                _pageMessage = "";
                RenderCurrentPage();
                return;
            }
            _currentPage = CuplumpReceivingPageId;
            _receivingLoadedDate = DateTime.MinValue;
            _pageMessage = "";
            RenderCurrentPage();
            await LoadReceivingAsync(force: true);
        }
        else if (action is "RRC:TruckScale" or "RRC:QueueSummary")
            OpenTruckScalePage(action == "RRC:QueueSummary" ? "queue-summary" : "check-in", "all");
        else if (action is "RRC:Scales" or "Settings:Scales")
        {
            TrySelectRibbonTab(DepartmentId.Receiving.ToString());
            _currentPage = "RRC:Scales";
            _pageMessage = "";
            RenderCurrentPage();
        }
        else if (action == "Refresh")
        {
            if (_currentPage == "Home" || _currentPage == "Settings" && _settingsSection == "System Config") await RefreshConnectionAsync();
            else if (_currentPage == "Settings" && _settingsSection is "Users" or "Staff") await LoadRemoteWorkspaceAsync();
            else if (_currentPage == "Settings" && IsSecuritySettingsSection(_settingsSection)) await LoadSecuritySettingsAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection is "Cuplump" or "USS") await LoadBookingQueueAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection is "Supplier" or "Rubber Type" or "Order") await LoadPurchasingRecordsAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection == "Marking Preparation") await LoadMarkingPreparationReportsAsync();
            else if (_currentPage == DepartmentId.Purchase.ToString() && _purchasingSection == "SI Export") await LoadSIExportsAsync();
            else if (_currentPage == DepartmentId.Receiving.ToString() || _currentPage == CuplumpReceivingPageId)
                await LoadReceivingAsync(force: true);
            else if (_currentPage is TruckScalePageId or TruckScaleSummaryPageId) RefreshTruckScalePage();
            else if (_currentPage == "RRC:Scales") RefreshScalePorts();
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
        CollapseRibbonAfterCommand();
        MessageBox.Show($"YTRC-MRP Desktop\nClient version {ApplicationInfo.Version}\n\nDepartment workspaces and local administration tools.",
            "About YTRC-MRP", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CollapseRibbonAfterCommand()
    {
        if (!MainRibbon.CanMinimize) return;
        MainRibbon.IsMinimized = true;
    }
}

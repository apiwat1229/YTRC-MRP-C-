namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildDashboard()
    {
        var page = PageStack();
        var heroHeader = CreatePageHeroHeader(
            "ENTERPRISE RESOURCE PLANNING",
            "Executive Operations Dashboard",
            "Department operations, real-time indicators, and operational workspaces at a glance.",
            FluentIcons.Common.Icon.Home);
        page.Children.Add(heroHeader);
        page.Children.Add(SectionHeading("Live indicators"));
        var metrics = new Grid { Margin = new Thickness(0, 0, 0, 20) };
        for (var i = 0; i < 4; i++) metrics.ColumnDefinitions.Add(new ColumnDefinition());
        var items = new[] { ("Pending approvals", "—", "No dashboard aggregate endpoint"),
            ("Purchase orders", "—", "Awaiting ERP data contract"),
            ("QA reviews", "—", "Awaiting ERP data contract"),
            ("Inventory alerts", "—", "Awaiting ERP data contract") };
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var content = new StackPanel();
            content.Children.Add(Label(item.Item1, 12, (Brush)FindResource("MutedBrush")));
            content.Children.Add(Label(item.Item2, 28, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
            content.Children.Add(Label(item.Item3, 11, (Brush)FindResource("MutedBrush")));
            var card = Card(content, DepartmentCatalog.All[i].Accent);
            card.Margin = new Thickness(0, 0, i == 3 ? 0 : 12, 0);
            Grid.SetColumn(card, i); metrics.Children.Add(card);
        }
        page.Children.Add(metrics);
        page.Children.Add(SectionHeading("Departments"));
        var grid = new UniformGrid { Columns = 3, Rows = 0, Margin = new Thickness(-7, -4, -7, 14) };
        foreach (var dept in DepartmentCatalog.All)
        {
            var body = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var code = new Grid { Width = 42, Height = 42, Margin = new Thickness(0, 0, 10, 0) };
            code.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Fill = new SolidColorBrush(Color.FromRgb(239, 246, 247))
            });
            code.Children.Add(Label(dept.Code, 13,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString(dept.Accent)!), FontWeights.SemiBold, true));
            body.Children.Add(code);
            var text = new StackPanel(); text.Children.Add(Label(dept.Name, 15, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
            text.Children.Add(Label(dept.Description, 12, (Brush)FindResource("MutedBrush")));
            body.Children.Add(text);
            var btn = new Button { Content = body, Tag = dept.Id.ToString(), HorizontalContentAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(7), Padding = new Thickness(10, 11, 12, 11), MinHeight = 75 };
            btn.Click += DepartmentTile_Click;
            grid.Children.Add(btn);
        }
        page.Children.Add(grid);
        page.Children.Add(Banner("Integration status", _apiOnline
            ? $"Server reachable at {AppSettings.Current.ApiBaseUrl}. Department KPI and transaction endpoints still need to be connected."
            : "The current project is a client foundation. No ERP records are stored locally.", "#526F91"));
        return page;
    }

    private void DepartmentTile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string page)
        {
            if (TrySelectRibbonTab(page)) return;
            _currentPage = page;
            _pageMessage = "";
            RenderCurrentPage();
        }
    }
}

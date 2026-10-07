namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildSearchResults(string query)
    {
        var page = PageStack();
        page.Children.Add(TitleBlock("Search results", $"Matches for “{query}”"));
        var lower = query.ToLowerInvariant();
        var matches = DepartmentCatalog.All.Where(d => d.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            d.Code.Contains(query, StringComparison.OrdinalIgnoreCase) || d.Workflows.Any(w => w.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 0) page.Children.Add(EmptyState("No matching work areas", "Try searching for QA, orders, warehouse, inspections or production."));
        foreach (var match in matches)
        {
            var workflows = string.Join(" · ", match.Workflows.Where(w => w.Contains(lower, StringComparison.OrdinalIgnoreCase)));
            var body = new StackPanel(); body.Children.Add(Label(match.Name, 15, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
            body.Children.Add(Label(workflows.Length > 0 ? workflows : match.Description, 12, (Brush)FindResource("MutedBrush")));
            var button = new Button { Content = body, Tag = match.Id.ToString(), HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 8) };
            button.Click += DepartmentTile_Click; page.Children.Add(button);
        }
        return page;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text.Trim();
        if (query.Length == 0) return;
        _currentPage = "Search";
        _pageMessage = "";
        RenderCurrentPage();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Search_Click(sender, new RoutedEventArgs());
    }
}

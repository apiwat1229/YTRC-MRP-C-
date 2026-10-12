namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private UIElement BuildDepartmentPage(DepartmentId id)
    {
        var info = DepartmentCatalog.Get(id);
        var page = PageStack();
        if (!_session.User.HasDepartmentAccess(id))
        {
            page.Children.Add(TitleBlock(info.Name, info.Description));
            page.Children.Add(Banner("Access restricted", "Your account does not have permission to view this department. Contact your administrator.", "#DC2626"));
            return page;
        }

        if (id == DepartmentId.Receiving)
            return BuildReceivingPage();

        page.Children.Add(CreatePageHeroHeader(
            $"{info.Code} DEPARTMENT WORKSPACE",
            info.Name,
            info.Description,
            FluentIcons.Common.Icon.AppFolder));
        page.Children.Add(Banner(_apiOnline ? "Server connected · workspace is ready" : "Workspace is ready to connect",
            "Department pages in the source project are starter views; no transaction records are loaded or submitted here.", info.Accent));

        var cards = new Grid { Margin = new Thickness(-6, 10, -6, 17) };
        for (var i = 0; i < 3; i++) cards.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < info.Workflows.Count; i++)
        {
            var col = new StackPanel();
            col.Children.Add(Label(info.Workflows[i], 15, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
            col.Children.Add(Label("Starter work area", 12, (Brush)FindResource("MutedBrush")));
            var route = i < info.ApiEndpoints.Count ? info.ApiEndpoints[i] : "API route not specified in source";
            col.Children.Add(Label(route, 11, new SolidColorBrush(Color.FromRgb(15, 118, 110))));
            var card = Card(col, info.Accent); card.Margin = new Thickness(6); card.Padding = new Thickness(17);
            Grid.SetColumn(card, i); cards.Children.Add(card);
        }
        page.Children.Add(cards);
        page.Children.Add(SectionHeading("Records & activity"));
        page.Children.Add(EmptyState("No live records are available in this workspace",
            "The repository documents API routes for some departments, but the source MFC pages do not query or submit department transactions yet."));
        return page;
    }
}

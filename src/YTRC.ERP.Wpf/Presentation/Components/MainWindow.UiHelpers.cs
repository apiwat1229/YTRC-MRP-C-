namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private static StackPanel PageStack() => new() { Margin = new Thickness(28, 23, 28, 30) };

    private UIElement TitleBlock(string title, string subtitle)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        stack.Children.Add(Label(title, 28, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        stack.Children.Add(Label(subtitle, 14, (Brush)FindResource("MutedBrush")));
        return stack;
    }

    private static TextBlock SectionHeading(string text) => new()
    { Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(30, 45, 62)), Margin = new Thickness(0, 15, 0, 10) };

    private static TextBlock Label(string text, double size, Brush brush, FontWeight? weight = null, bool center = false)
        => new() { Text = text, FontSize = size, Foreground = brush, FontWeight = weight ?? FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };

    private static Border Card(UIElement child, string accent)
    {
        var body = new DockPanel();
        var stripe = new Border { Width = 4, Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)!) };
        DockPanel.SetDock(stripe, Dock.Left); body.Children.Add(stripe);
        var content = new Border { Padding = new Thickness(13, 10, 10, 10), Child = child };
        body.Children.Add(content);
        return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(225, 231, 239)),
            BorderThickness = new Thickness(1), Child = body, Padding = new Thickness(0), CornerRadius = new CornerRadius(5) };
    }

    private UIElement Banner(string title, string detail, string accent)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(title, 15, (Brush)FindResource("TextBrush"), FontWeights.SemiBold));
        stack.Children.Add(Label(detail, 12, (Brush)FindResource("MutedBrush")));
        var card = Card(stack, accent); card.Margin = new Thickness(0, 2, 0, 15); card.Padding = new Thickness(0);
        return card;
    }

    private UIElement EmptyState(string title, string detail)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20) };
        stack.Children.Add(Label(title, 16, (Brush)FindResource("TextBrush"), FontWeights.SemiBold, true));
        stack.Children.Add(Label(detail, 12, (Brush)FindResource("MutedBrush"), center: true));
        return new Border { Background = Brushes.White, BorderBrush = (Brush)FindResource("BorderBrushSoft"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6), MinHeight = 165, Child = stack };
    }

    private static UIElement Field(string title, UIElement control)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(title, 12, new SolidColorBrush(Color.FromRgb(78, 95, 116)), FontWeights.SemiBold));
        stack.Children.Add(control);
        return stack;
    }

    private static UIElement FieldCompact(string title, UIElement control)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 7, 2) };
        stack.Children.Add(Label(title, 11, new SolidColorBrush(Color.FromRgb(104, 120, 141))));
        stack.Children.Add(control);
        return stack;
    }

    private Button ActionButton(string title, string action, bool primary = false)
    {
        var button = new Button { Content = title, Tag = action, Margin = new Thickness(0, 0, 8, 8),
            Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button)) };
        button.Click += SettingsAction_Click;
        return button;
    }
}

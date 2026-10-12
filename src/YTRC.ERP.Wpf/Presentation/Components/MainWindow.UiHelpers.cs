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

    private Border Card(UIElement child, string accent)
    {
        var appRadius = (CornerRadius)FindResource("AppCornerRadiusMedium");
        var body = new DockPanel();
        var stripe = new Border
        {
            Width = 4,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)!),
            CornerRadius = new CornerRadius(appRadius.TopLeft, 0, 0, appRadius.BottomLeft)
        };
        DockPanel.SetDock(stripe, Dock.Left); body.Children.Add(stripe);
        var content = new Border { Padding = new Thickness(13, 10, 10, 10), Child = child };
        body.Children.Add(content);
        return new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(225, 231, 239)),
            BorderThickness = new Thickness(1), Child = body, Padding = new Thickness(0),
            CornerRadius = appRadius };
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
            CornerRadius = (CornerRadius)FindResource("AppCornerRadiusMedium"), MinHeight = 165, Child = stack };
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
        var stack = new StackPanel { Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Label(title, 11, new SolidColorBrush(Color.FromRgb(104, 120, 141))));
        stack.Children.Add(control);
        return stack;
    }

    private Button ActionButton(string title, string action, bool primary = false)
    {
        var icon = CommonToolbarActionIcon(title);
        var button = icon is { } iconName
            ? IconOnlyActionButton(title, iconName, new Thickness(0, 0, 8, 8))
            : new Button { Content = title, Margin = new Thickness(0, 0, 8, 8),
                Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button)) };
        button.Tag = action;
        button.Click += SettingsAction_Click;
        return button;
    }

    private Button IconOnlyActionButton(string title, FluentIcons.Common.Icon icon, Thickness? margin = null)
    {
        var button = new Button
        {
            Content = new FluentIcons.Wpf.FluentIcon
            {
                Icon = icon,
                IconSize = FluentIcons.Common.IconSize.Size20,
                IconVariant = FluentIcons.Common.IconVariant.Regular,
                FontSize = 20,
                Width = 20,
                Height = 20,
                Foreground = (Brush)FindResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = 36,
            MinWidth = 36,
            Height = 34,
            Margin = margin ?? new Thickness(0, 0, 8, 0),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = title,
            Style = (Style)FindResource(typeof(Button))
        };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        return button;
    }

    private static FluentIcons.Common.Icon? CommonToolbarActionIcon(string title)
    {
        if (title.StartsWith("Refresh", StringComparison.OrdinalIgnoreCase))
            return FluentIcons.Common.Icon.ArrowClockwise;
        if (title.StartsWith("Edit", StringComparison.OrdinalIgnoreCase))
            return FluentIcons.Common.Icon.Edit;
        if (title.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Remove", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Move to Deleted", StringComparison.OrdinalIgnoreCase))
            return FluentIcons.Common.Icon.Delete;
        if (title.StartsWith("Cancel", StringComparison.OrdinalIgnoreCase))
            return FluentIcons.Common.Icon.CalendarCancel;
        if (title.StartsWith("Print", StringComparison.OrdinalIgnoreCase))
            return FluentIcons.Common.Icon.Print;
        return null;
    }
}

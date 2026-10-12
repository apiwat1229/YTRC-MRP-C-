using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentIcons.Common;
using FluentIcons.Wpf;

namespace YTRC.ERP.Wpf;

public partial class MainWindow
{
    private static StackPanel PageStack() => new() { Margin = UiTokens.PageMargin };

    private Border CreatePageHeroHeader(
        string tagBadgeText,
        string title,
        string subtitle,
        FluentIcons.Common.Icon icon,
        FrameworkElement? rightActions = null,
        FrameworkElement? titleBadge = null)
    {
        var headerCard = new Border
        {
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            Padding = UiTokens.HeroHeaderPadding,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var headerDock = new DockPanel();

        if (rightActions is not null)
        {
            DockPanel.SetDock(rightActions, Dock.Right);
            rightActions.VerticalAlignment = VerticalAlignment.Center;
            headerDock.Children.Add(rightActions);
        }

        var leftContent = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

        if (!string.IsNullOrEmpty(tagBadgeText))
        {
            var badgeBorder = UiTokens.CreateBadge(tagBadgeText);
            badgeBorder.Margin = new Thickness(0, 0, 0, 4);
            badgeBorder.HorizontalAlignment = HorizontalAlignment.Left;
            leftContent.Children.Add(badgeBorder);
        }

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var iconBox = new Border
        {
            Width = UiTokens.HeroIconSize,
            Height = UiTokens.HeroIconSize,
            CornerRadius = UiTokens.Radii.Medium,
            Background = UiTokens.Brushes.MintSurface,
            BorderBrush = UiTokens.Brushes.MintBorder,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new FluentIcon
            {
                Icon = icon,
                IconSize = IconSize.Size20,
                IconVariant = IconVariant.Regular,
                Foreground = UiTokens.Brushes.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        titleRow.Children.Add(iconBox);

        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var mainTitleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        mainTitleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = UiTokens.FontSizes.PageTitle,
            FontWeight = UiTokens.Weights.Bold,
            Foreground = UiTokens.Brushes.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = UiTokens.AppFontFamily
        });

        if (titleBadge is not null)
        {
            titleBadge.Margin = new Thickness(8, 0, 0, 0);
            titleBadge.VerticalAlignment = VerticalAlignment.Center;
            mainTitleRow.Children.Add(titleBadge);
        }

        titleStack.Children.Add(mainTitleRow);
        if (!string.IsNullOrEmpty(subtitle))
        {
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = UiTokens.FontSizes.Caption,
                Foreground = UiTokens.Brushes.TextMuted,
                Margin = new Thickness(0, 1, 0, 0),
                FontFamily = UiTokens.AppFontFamily
            });
        }

        titleRow.Children.Add(titleStack);
        leftContent.Children.Add(titleRow);
        headerDock.Children.Add(leftContent);

        headerCard.Child = headerDock;
        return headerCard;
    }

    private UIElement TitleBlock(string title, string subtitle)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
        stack.Children.Add(Label(title, UiTokens.FontSizes.Hero, UiTokens.Brushes.TextPrimary, UiTokens.Weights.SemiBold));
        stack.Children.Add(Label(subtitle, UiTokens.FontSizes.CardTitle, UiTokens.Brushes.TextMuted));
        return stack;
    }

    private static TextBlock SectionHeading(string text)
        => UiTokens.CreateHeading(text, UiTokens.FontSizes.Section, UiTokens.Weights.SemiBold, UiTokens.Brushes.TextPrimary, new Thickness(0, 15, 0, 10));

    private static TextBlock Label(string text, double size, Brush brush, FontWeight? weight = null, bool center = false)
        => UiTokens.CreateLabel(text, size, brush, weight, center);

    private Border Card(UIElement child, string accent)
    {
        var appRadius = UiTokens.Radii.Card;
        var body = new DockPanel();
        var stripe = new Border
        {
            Width = 4,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)!),
            CornerRadius = new CornerRadius(appRadius.TopLeft, 0, 0, appRadius.BottomLeft)
        };
        DockPanel.SetDock(stripe, Dock.Left);
        body.Children.Add(stripe);
        var content = new Border { Padding = UiTokens.CardPaddingCompact, Child = child };
        body.Children.Add(content);
        return new Border
        {
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            Child = body,
            Padding = new Thickness(0),
            CornerRadius = appRadius
        };
    }

    private UIElement Banner(string title, string detail, string accent)
    {
        var stack = new StackPanel();
        stack.Children.Add(Label(title, UiTokens.FontSizes.Section, UiTokens.Brushes.TextPrimary, UiTokens.Weights.SemiBold));
        stack.Children.Add(Label(detail, UiTokens.FontSizes.Caption, UiTokens.Brushes.TextMuted));
        var card = Card(stack, accent);
        card.Margin = new Thickness(0, 2, 0, 15);
        card.Padding = new Thickness(0);
        return card;
    }

    private UIElement EmptyState(string title, string detail)
    {
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20)
        };
        stack.Children.Add(Label(title, UiTokens.FontSizes.Section, UiTokens.Brushes.TextPrimary, UiTokens.Weights.SemiBold, true));
        stack.Children.Add(Label(detail, UiTokens.FontSizes.Caption, UiTokens.Brushes.TextMuted, center: true));
        return new Border
        {
            Background = UiTokens.Brushes.CardBackground,
            BorderBrush = UiTokens.Brushes.BorderSoft,
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.Radii.Card,
            MinHeight = 165,
            Child = stack
        };
    }

    private static UIElement Field(string title, UIElement control)
    {
        var stack = new StackPanel { Margin = UiTokens.FieldMargin };
        stack.Children.Add(Label(title, UiTokens.FontSizes.Caption, UiTokens.Brushes.TextMuted, UiTokens.Weights.SemiBold));
        stack.Children.Add(control);
        return stack;
    }

    private static UIElement FieldCompact(string title, UIElement control)
    {
        var stack = new StackPanel { Margin = UiTokens.FieldMarginCompact, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(Label(title, UiTokens.FontSizes.Small, UiTokens.Brushes.TextMuted));
        stack.Children.Add(control);
        return stack;
    }

    private Button ActionButton(string title, string action, bool primary = false)
    {
        var icon = CommonToolbarActionIcon(title);
        var button = icon is { } iconName
            ? IconOnlyActionButton(title, iconName, new Thickness(0, 0, 8, 8))
            : new Button
            {
                Content = title,
                Margin = new Thickness(0, 0, 8, 8),
                Style = primary ? (Style)FindResource("PrimaryButton") : (Style)FindResource(typeof(Button))
            };
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
                Foreground = UiTokens.Brushes.TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            },
            Width = UiTokens.ToolbarButtonSize + 2,
            MinWidth = UiTokens.ToolbarButtonSize + 2,
            Height = UiTokens.ButtonHeight,
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

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace YTRC.ERP.Wpf;

internal sealed class BookingQueueDatePicker : Button
{
    public static readonly DependencyProperty MonthSelectionOnlyProperty = DependencyProperty.Register(
        nameof(MonthSelectionOnly), typeof(bool), typeof(BookingQueueDatePicker), new PropertyMetadata(false));

    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(BookingQueueDatePicker),
        new FrameworkPropertyMetadata(null, OnSelectedDateChanged));

    public static readonly DependencyProperty DateDisplayFormatProperty = DependencyProperty.Register(
        nameof(DateDisplayFormat), typeof(string), typeof(BookingQueueDatePicker),
        new FrameworkPropertyMetadata("dd MMM yyyy", OnDateDisplayFormatChanged));

    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(BookingQueueDatePicker),
        new FrameworkPropertyMetadata("Select a date", (d, _) => ((BookingQueueDatePicker)d).UpdateDateText()));

    public static readonly RoutedEvent DateSelectedEvent = EventManager.RegisterRoutedEvent(
        nameof(DateSelected), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<DateTime?>),
        typeof(BookingQueueDatePicker));

    private readonly TextBlock _dateText;
    private readonly System.Windows.Controls.Calendar _calendar;
    private readonly Popup _popup;
    private readonly Border _popupBorder;
    private readonly Grid _calendarContainer;
    private readonly Grid _monthPickerPanel;
    private readonly TextBlock _monthPickerYearText;
    private readonly Button[] _monthButtons = new Button[12];
    private int _monthPickerYear;
    private bool _initializingCalendar;

    public bool MonthSelectionOnly
    {
        get => (bool)GetValue(MonthSelectionOnlyProperty);
        set => SetValue(MonthSelectionOnlyProperty, value);
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public string DateDisplayFormat
    {
        get => (string)GetValue(DateDisplayFormatProperty);
        set => SetValue(DateDisplayFormatProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public event RoutedPropertyChangedEventHandler<DateTime?> DateSelected
    {
        add => AddHandler(DateSelectedEvent, value);
        remove => RemoveHandler(DateSelectedEvent, value);
    }

    public BookingQueueDatePicker()
    {
        Width = UiTokens.DatePickerWidth;
        Height = UiTokens.DatePickerHeight;
        Padding = new Thickness(6, 0, 6, 0);
        Background = Brushes.White;
        BorderBrush = Brush("#CFD9E5");
        BorderThickness = new Thickness(1);
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        Cursor = Cursors.Hand;
        ToolTip = "Choose date. Press Delete to clear.";
        if (System.Windows.Application.Current?.TryFindResource(typeof(Button)) is Style buttonStyle)
            Style = buttonStyle;

        _dateText = new TextBlock
        {
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        Content = _dateText;

        _calendar = new System.Windows.Controls.Calendar
        {
            SelectionMode = CalendarSelectionMode.SingleDate,
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("en-GB"),
            Style = MainWindow.CreateBookingQueueCalendarStyle()
        };

        _calendarContainer = new Grid();
        _calendarContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _calendarContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _calendarContainer.Children.Add(_calendar);
        Grid.SetRow(_calendar, 0);

        var calendarFooter = new DockPanel { Margin = new Thickness(6, 0, 6, 4) };
        var todayBtn = CreateCalendarActionButton("Today", () =>
        {
            var prev = SelectedDate?.Date;
            var today = DateTime.Today;
            if (prev != today)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(prev, today, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, today);
            _popup?.IsOpen = false;
        });
        var clearBtn = CreateCalendarActionButton("Clear", () =>
        {
            var prev = SelectedDate?.Date;
            if (prev != null)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(prev, null, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, null);
            _popup?.IsOpen = false;
        });
        DockPanel.SetDock(todayBtn, Dock.Left);
        DockPanel.SetDock(clearBtn, Dock.Right);
        calendarFooter.Children.Add(todayBtn);
        calendarFooter.Children.Add(clearBtn);
        _calendarContainer.Children.Add(calendarFooter);
        Grid.SetRow(calendarFooter, 1);

        _monthPickerYearText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#1E2D3E"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _monthPickerPanel = CreateMonthPickerPanel();
        _popupBorder = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#D8E0EA"),
            BorderThickness = new Thickness(1),
            CornerRadius = UiTokens.CornerRadiusMedium,
            Padding = new Thickness(5),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(15, 23, 42),
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = .16
            },
            Child = _calendarContainer
        };
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade,
            Child = _popupBorder
        };
        _calendar.SelectedDatesChanged += (_, _) =>
        {
            if (_initializingCalendar || MonthSelectionOnly || _calendar.SelectedDate is not DateTime selectedDate) return;
            var previousDate = SelectedDate?.Date;
            var nextDate = selectedDate.Date;
            if (previousDate != nextDate)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousDate, nextDate, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, nextDate);
            _popup.IsOpen = false;
        };
        _calendar.DisplayModeChanged += (_, _) =>
        {
            if (_initializingCalendar || !_popup.IsOpen || !MonthSelectionOnly ||
                _calendar.DisplayMode != CalendarMode.Month) return;

            var selectedMonth = new DateTime(_calendar.DisplayDate.Year, _calendar.DisplayDate.Month, 1);
            var previousMonth = SelectedDate?.Date;
            if (previousMonth != selectedMonth)
                RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousMonth, selectedMonth, DateSelectedEvent));
            SetCurrentValue(SelectedDateProperty, selectedMonth);
            _popup.IsOpen = false;
        };

        KeyDown += (_, e) =>
        {
            if (e.Key is Key.Delete or Key.Back)
            {
                var previousDate = SelectedDate?.Date;
                if (previousDate != null)
                    RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousDate, null, DateSelectedEvent));
                SetCurrentValue(SelectedDateProperty, null);
                _popup.IsOpen = false;
                e.Handled = true;
            }
        };

        Click += (_, _) => OpenCalendar();
        UpdateDateText();
    }

    private static Button CreateCalendarActionButton(string text, Action onClick)
    {
        var btn = new Button
        {
            Content = text,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#0F766E"),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Padding = new Thickness(6, 2, 6, 2)
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void OpenCalendar()
    {
        var initialDate = SelectedDate?.Date ?? DateTime.Today;
        if (MonthSelectionOnly)
        {
            _monthPickerYear = initialDate.Year;
            UpdateMonthPicker();
            _popupBorder.Child = _monthPickerPanel;
        }
        else
        {
            _calendar.ClearValue(FrameworkElement.WidthProperty);
            _calendar.ClearValue(FrameworkElement.HeightProperty);
            _calendar.ClearValue(Control.PaddingProperty);
            _popupBorder.Child = _calendarContainer;
        }

        _initializingCalendar = true;
        if (!MonthSelectionOnly)
        {
            _calendar.DisplayDate = initialDate;
            _calendar.SelectedDate = SelectedDate?.Date;
            _calendar.DisplayMode = CalendarMode.Month;
        }
        _initializingCalendar = false;
        _popup.IsOpen = true;
    }

    private Grid CreateMonthPickerPanel()
    {
        var panel = new Grid { Width = 288, Height = 222 };
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        header.Children.Add(CreateMonthPickerButton("‹", () => ChangeMonthPickerYear(-1), isNavigation: true));
        Grid.SetColumn(_monthPickerYearText, 1);
        header.Children.Add(_monthPickerYearText);
        var nextYear = CreateMonthPickerButton("›", () => ChangeMonthPickerYear(1), isNavigation: true);
        Grid.SetColumn(nextYear, 2);
        header.Children.Add(nextYear);
        panel.Children.Add(header);

        var months = new Grid { Margin = new Thickness(2, 4, 2, 2) };
        for (var column = 0; column < 4; column++)
            months.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var row = 0; row < 3; row++)
            months.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (var month = 1; month <= 12; month++)
        {
            var monthNumber = month;
            var label = CultureInfo.GetCultureInfo("en-GB").DateTimeFormat.GetAbbreviatedMonthName(month);
            if (month == 9) label = "Sept";
            var button = CreateMonthPickerButton(label, () => SelectMonth(monthNumber));
            _monthButtons[month - 1] = button;
            Grid.SetRow(button, (month - 1) / 4);
            Grid.SetColumn(button, (month - 1) % 4);
            months.Children.Add(button);
        }

        Grid.SetRow(months, 1);
        panel.Children.Add(months);
        return panel;
    }

    private static Button CreateMonthPickerButton(string label, Action onClick, bool isNavigation = false)
    {
        var template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                             TargetType="{x:Type Button}">
                <Border x:Name="Chrome" Background="{TemplateBinding Background}" CornerRadius="7">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"
                                      Margin="{TemplateBinding Padding}"
                                      RecognizesAccessKey="True"
                                      TextElement.Foreground="{TemplateBinding Foreground}" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="#EDF7F5" />
                        <Setter Property="Foreground" Value="#0F766E" />
                        <Setter Property="FontWeight" Value="SemiBold" />
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Chrome" Property="Background" Value="#DDEEEB" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter Property="Foreground" Value="#B7C0CA" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Brush("#344054")));
        style.Setters.Add(new Setter(Control.FontSizeProperty, isNavigation ? 20d : 15d));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Normal));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(isNavigation ? 1 : 2)));

        var button = new Button
        {
            Content = label,
            Style = style,
            Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void ChangeMonthPickerYear(int offset)
    {
        var nextYear = _monthPickerYear + offset;
        if (nextYear is < 1 or > 9999) return;
        _monthPickerYear = nextYear;
        UpdateMonthPicker();
    }

    private void UpdateMonthPicker()
    {
        _monthPickerYearText.Text = _monthPickerYear.ToString(CultureInfo.InvariantCulture);
        var selected = SelectedDate;
        for (var index = 0; index < _monthButtons.Length; index++)
        {
            var isSelected = selected is { } date && date.Year == _monthPickerYear && date.Month == index + 1;
            var button = _monthButtons[index];
            button.Background = isSelected ? Brush("#D7EDF2") : Brushes.Transparent;
            button.Foreground = isSelected ? Brush("#0F625B") : Brush("#344054");
            button.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void SelectMonth(int month)
    {
        var selectedMonth = new DateTime(_monthPickerYear, month, 1);
        var previousMonth = SelectedDate?.Date;
        if (previousMonth != selectedMonth)
            RaiseEvent(new RoutedPropertyChangedEventArgs<DateTime?>(previousMonth, selectedMonth, DateSelectedEvent));
        SetCurrentValue(SelectedDateProperty, selectedMonth);
        _popup.IsOpen = false;
    }

    private void UpdateDateText()
    {
        if (SelectedDate is { } date)
        {
            _dateText.Text = date.ToString(DateDisplayFormat, CultureInfo.GetCultureInfo("en-GB"));
            _dateText.Foreground = Brush("#1E2D3E");
            _dateText.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            _dateText.Text = string.IsNullOrEmpty(PlaceholderText) ? "Select a date" : PlaceholderText;
            _dateText.Foreground = Brush("#94A3B8");
            _dateText.FontWeight = FontWeights.Normal;
        }
    }

    private static void OnSelectedDateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is BookingQueueDatePicker picker) picker.UpdateDateText();
    }

    private static void OnDateDisplayFormatChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is BookingQueueDatePicker picker) picker.UpdateDateText();
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color)!);
}

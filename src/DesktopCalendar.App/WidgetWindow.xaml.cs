using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Core.Calendar;
using DesktopCalendar.Core.Desktop;
using DesktopCalendar.Core.Holiday;
using DesktopCalendar.Core.Storage;

namespace DesktopCalendar.App;

public partial class WidgetWindow : Window
{
    private const double DefaultLeft = 100;
    private const double DefaultTop = 100;
    private const double DefaultWidth = 560;
    private const double DefaultHeight = 640;
    private const double MinWidgetWidth = 420;
    private const double MinWidgetHeight = 480;
    private const int MaxVisibleSchedulesPerCell = 3;

    private static readonly CultureInfo Korean = new("ko-KR");
    private static readonly string[] WeekdayLabels = ["일", "월", "화", "수", "목", "금", "토"];

    private const int MaxVisibleDDays = 5;

    private readonly SettingsStore _settings;
    private readonly ScheduleRepository _scheduleRepository = new();
    private readonly HolidayRepository _holidayRepository = new();
    private readonly DDayRepository _dDayRepository = new();
    private readonly HashSet<int> _fetchingHolidayYears = [];
    private readonly DesktopBackgroundHost _backgroundHost = new();
    private readonly DispatcherTimer _reattachTimer;

    private IntPtr _hwnd;
    private bool _isLocked;
    private Point _dragStartScreenPoint;
    private bool _isDragging;
    private DateTime _displayedMonth;
    private double _fontScale;

    public WidgetWindow()
    {
        InitializeComponent();

        _settings = new SettingsStore();
        LoadWindowState();
        _fontScale = _settings.GetDouble("Widget.FontScale", 1.0);

        _displayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ApplyFontScale();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;

        _reattachTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _reattachTimer.Tick += (_, _) => _backgroundHost.EnsureAttached(_hwnd);
    }

    private void LoadWindowState()
    {
        Left = _settings.GetDouble("Widget.Left", DefaultLeft);
        Top = _settings.GetDouble("Widget.Top", DefaultTop);
        Width = _settings.GetDouble("Widget.Width", DefaultWidth);
        Height = _settings.GetDouble("Widget.Height", DefaultHeight);
        _isLocked = _settings.GetBool("Widget.Locked", false);
    }

    private void SaveWindowState()
    {
        _settings.SetDouble("Widget.Left", Left);
        _settings.SetDouble("Widget.Top", Top);
        _settings.SetDouble("Widget.Width", Width);
        _settings.SetDouble("Widget.Height", Height);
        _settings.SetBool("Widget.Locked", _isLocked);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;

        // 태스크바/Alt+Tab에서 숨김 (DESIGN.md 4.1 step 5)
        DesktopBackgroundHost.HideFromTaskbarAndAltTab(_hwnd);

        LockMenuItem.IsChecked = _isLocked;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _backgroundHost.Attach(_hwnd);
        _reattachTimer.Start();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _reattachTimer.Stop();
        SaveWindowState();
    }

    private void PanelBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isLocked)
            return;

        _isDragging = true;
        _dragStartScreenPoint = PointToScreen(e.GetPosition(this));
        ((System.Windows.Controls.Border)sender).CaptureMouse();
    }

    private void PanelBorder_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging)
            return;

        var current = PointToScreen(e.GetPosition(this));
        var delta = current - _dragStartScreenPoint;

        Left += delta.X;
        Top += delta.Y;

        _dragStartScreenPoint = current;
    }

    private void PanelBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        ((System.Windows.Controls.Border)sender).ReleaseMouseCapture();
        SaveWindowState();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_isLocked)
            return;

        Width = Math.Max(MinWidgetWidth, Width + e.HorizontalChange);
        Height = Math.Max(MinWidgetHeight, Height + e.VerticalChange);
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        SaveWindowState();
    }

    private void LockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _isLocked = LockMenuItem.IsChecked;
        SaveWindowState();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ApplyFontScale()
    {
        MonthYearText.FontSize = 14 * _fontScale;
        PrevMonthButton.FontSize = 13 * _fontScale;
        NextMonthButton.FontSize = 13 * _fontScale;
        HintText.FontSize = 10 * _fontScale;

        BuildWeekdayHeader();
        BuildDDayPanel();
        RenderMonth();
    }

    private void BuildDDayPanel()
    {
        DDayPanel.Children.Clear();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = _dDayRepository.GetAll()
            .Select(d => (DDay: d, Remaining: DDayCalculator.ComputeDaysRemaining(d, today)))
            .OrderBy(x => x.Remaining)
            .Take(MaxVisibleDDays)
            .ToList();

        DDayPanel.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var (dday, remaining) in items)
        {
            var chip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(210, 150, 90, 200)),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(5, 2, 5, 2),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = $"{DDayCalculator.Format(remaining)} {dday.Title}",
                    FontSize = 10 * _fontScale,
                    Foreground = Brushes.White,
                },
            };
            chip.MouseLeftButtonDown += DDayChip_MouseLeftButtonDown;
            DDayPanel.Children.Add(chip);
        }
    }

    private void DDayChip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // 패널 드래그로 전파되지 않도록 막음
        OpenDDayManager();
    }

    private void DDayManageMenuItem_Click(object sender, RoutedEventArgs e) => OpenDDayManager();

    private void OpenDDayManager()
    {
        var window = new DDayListWindow(_dDayRepository);
        window.ShowDialog();
        BuildDDayPanel();
    }

    private void FontSizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FontSizeDialog(_fontScale);
        if (dialog.ShowDialog() == true)
        {
            _fontScale = dialog.SelectedScale;
            _settings.SetDouble("Widget.FontScale", _fontScale);
            ApplyFontScale();
        }
    }

    private void BuildWeekdayHeader()
    {
        WeekdayHeader.Children.Clear();
        for (int i = 0; i < WeekdayLabels.Length; i++)
        {
            WeekdayHeader.Children.Add(new TextBlock
            {
                Text = WeekdayLabels[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 11 * _fontScale,
                Foreground = i == 0 ? Brushes.IndianRed : i == 6 ? Brushes.CornflowerBlue : Brushes.White,
            });
        }
    }

    private void RenderMonth()
    {
        MonthYearText.Text = _displayedMonth.ToString("yyyy년 M월", Korean) + "  ▾";

        var schedules = _scheduleRepository.GetByMonth(_displayedMonth.Year, _displayedMonth.Month);
        var schedulesByDate = new Dictionary<DateOnly, List<Schedule>>();
        foreach (var schedule in schedules)
        {
            var start = DateOnly.FromDateTime(schedule.StartAt);
            var end = DateOnly.FromDateTime(schedule.EndAt);
            for (var cursor = start; cursor <= end; cursor = cursor.AddDays(1))
            {
                if (!schedulesByDate.TryGetValue(cursor, out var list))
                    schedulesByDate[cursor] = list = [];
                list.Add(schedule);
            }
        }

        var holidaysByDate = _holidayRepository.GetByMonth(_displayedMonth.Year, _displayedMonth.Month)
            .ToDictionary(h => h.Date);
        EnsureHolidaysForYearAsync(_displayedMonth.Year);

        DaysGrid.Children.Clear();

        var startOffset = (int)_displayedMonth.DayOfWeek;
        var gridStart = _displayedMonth.AddDays(-startOffset);

        for (int i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var dateOnly = DateOnly.FromDateTime(date);
            schedulesByDate.TryGetValue(dateOnly, out var daySchedules);
            holidaysByDate.TryGetValue(dateOnly, out var holiday);
            DaysGrid.Children.Add(BuildDayCell(date, daySchedules, holiday));
        }
    }

    private async void EnsureHolidaysForYearAsync(int year)
    {
        if (_holidayRepository.IsYearCached(year) || _fetchingHolidayYears.Contains(year))
            return;

        var apiKey = _settings.GetString("Holiday.ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            return;

        _fetchingHolidayYears.Add(year);
        try
        {
            var client = new HolidayApiClient(apiKey);
            var holidays = await client.GetHolidaysAsync(year);
            _holidayRepository.ReplaceYearFromApi(year, holidays);

            if (_displayedMonth.Year == year)
                RenderMonth();
        }
        catch (HolidayApiException ex)
        {
            MessageBox.Show(ex.Message, "공휴일 조회 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _fetchingHolidayYears.Remove(year);
        }
    }

    private void ApiKeySettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ApiKeyDialog(_settings.GetString("Holiday.ApiKey"));
        if (dialog.ShowDialog() == true && dialog.ApiKey is not null)
        {
            _settings.SetString("Holiday.ApiKey", dialog.ApiKey);
            EnsureHolidaysForYearAsync(_displayedMonth.Year);
        }
    }

    private Border BuildDayCell(DateTime date, List<Schedule>? daySchedules, Holiday? holiday)
    {
        var isCurrentMonth = date.Month == _displayedMonth.Month;
        var isToday = date.Date == DateTime.Today;

        Brush numberForeground = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => Brushes.IndianRed,
            DayOfWeek.Saturday => Brushes.CornflowerBlue,
            _ => Brushes.White,
        };

        if (holiday is not null)
            numberForeground = Brushes.IndianRed;

        if (!isCurrentMonth)
            numberForeground = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));

        var content = new DockPanel { LastChildFill = true };

        content.Children.Add(new TextBlock
        {
            Text = date.Day.ToString(),
            Foreground = numberForeground,
            FontSize = 11 * _fontScale,
            FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
            Margin = new Thickness(3, 2, 0, 2),
        });
        DockPanel.SetDock(content.Children[^1], Dock.Top);

        if (holiday is not null)
        {
            var holidayText = new TextBlock
            {
                Text = holiday.Name,
                FontSize = 9 * _fontScale,
                Foreground = Brushes.IndianRed,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(3, 0, 3, 2),
            };
            DockPanel.SetDock(holidayText, Dock.Top);
            content.Children.Add(holidayText);
        }

        var eventsPanel = new StackPanel { Margin = new Thickness(2, 0, 2, 2) };
        if (daySchedules is { Count: > 0 })
        {
            var visible = daySchedules.Take(MaxVisibleSchedulesPerCell);
            foreach (var schedule in visible)
                eventsPanel.Children.Add(BuildScheduleChip(schedule, _fontScale));

            var overflow = daySchedules.Count - MaxVisibleSchedulesPerCell;
            if (overflow > 0)
            {
                eventsPanel.Children.Add(new TextBlock
                {
                    Text = $"+{overflow}개",
                    FontSize = 9 * _fontScale,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 220, 220, 220)),
                    Margin = new Thickness(2, 1, 0, 0),
                });
            }
        }
        content.Children.Add(eventsPanel);

        var cell = new Border
        {
            Margin = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = isToday ? new SolidColorBrush(Color.FromArgb(90, 90, 160, 255)) : Brushes.Transparent,
            Cursor = Cursors.Hand,
            Tag = DateOnly.FromDateTime(date),
            Child = content,
        };

        if (holiday is not null)
            cell.ToolTip = holiday.Name;

        cell.MouseLeftButtonDown += DayCell_MouseLeftButtonDown;

        return cell;
    }

    private static Border BuildScheduleChip(Schedule schedule, double fontScale)
    {
        var chipBrush = TryParseColor(schedule.Color) ?? new SolidColorBrush(Color.FromArgb(210, 70, 130, 200));
        var text = schedule.IsAllDay ? schedule.Title : $"{schedule.StartAt:HH:mm} {schedule.Title}";

        return new Border
        {
            Background = chipBrush,
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(3, 1, 3, 1),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 10 * fontScale,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }

    private static SolidColorBrush? TryParseColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return null;

        try
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(colorHex)!;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // 패널 드래그로 전파되지 않도록 막음

        if (sender is not Border { Tag: DateOnly date })
            return;

        var dialog = new DayEventsWindow(date, _scheduleRepository, _holidayRepository);
        dialog.ShowDialog();

        RenderMonth();
    }

    private void PrevMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        RenderMonth();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        RenderMonth();
    }

    private void MonthYearText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // 패널 드래그로 전파되지 않도록 막음

        var picker = new MonthPickerWindow(_displayedMonth.Year, _displayedMonth.Month);
        if (picker.ShowDialog() == true)
        {
            _displayedMonth = new DateTime(picker.SelectedYear, picker.SelectedMonth, 1);
            RenderMonth();
        }
    }
}

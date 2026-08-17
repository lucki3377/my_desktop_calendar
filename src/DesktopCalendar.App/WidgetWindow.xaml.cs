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
using DesktopCalendar.Core.Google;
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
    private readonly GoogleEventRepository _googleEventRepository = new();
    private readonly SqliteDpapiDataStore _googleDataStore = new();
    private readonly GoogleSettings _googleSettings;
    private readonly HashSet<int> _fetchingHolidayYears = [];
    private readonly DesktopBackgroundHost _backgroundHost = new();
    private readonly DispatcherTimer _reattachTimer;
    private readonly DispatcherTimer _googleSyncTimer;

    private IntPtr _hwnd;
    private bool _isLocked;
    private Point _dragStartScreenPoint;
    private bool _isDragging;
    private DateTime _displayedMonth;
    private double _fontScale;
    private WidgetTheme _theme;

    /// <summary>현재 구글 이벤트 캐시가 담고 있는 구간. 표시할 달이 이 밖이면 다시 동기화한다.</summary>
    private DateTime _googleCachedRangeStart;
    private DateTime _googleCachedRangeEnd;
    private bool _isSyncingGoogle;

    public WidgetWindow()
    {
        InitializeComponent();

        _settings = new SettingsStore();
        _googleSettings = new GoogleSettings(_settings);
        LoadWindowState();
        _fontScale = _settings.GetDouble("Widget.FontScale", 1.0);

        _displayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _theme = WidgetTheme.Load(_settings);
        ApplyTheme();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closing += OnClosing;

        _reattachTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _reattachTimer.Tick += (_, _) => _backgroundHost.EnsureAttached(_hwnd);

        // 구글 일정 폴링 동기화 (DESIGN.md 4.4 — 주기는 설정에서 조절)
        _googleSyncTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(GoogleSettings.DefaultSyncIntervalMinutes)
        };
        _googleSyncTimer.Tick += (_, _) => SyncGoogleEventsAsync(_displayedMonth);
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
        ApplyGoogleSyncSchedule();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _reattachTimer.Stop();
        _googleSyncTimer.Stop();
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

    /// <summary>글자 크기와 색상 테마를 화면 전체에 다시 적용한다.</summary>
    private void ApplyTheme()
    {
        PanelBorder.Background = _theme.PanelBrush;

        MonthYearText.FontSize = 14 * _fontScale;
        MonthYearText.Foreground = _theme.PrimaryText;
        PrevMonthButton.FontSize = 13 * _fontScale;
        PrevMonthButton.Foreground = _theme.PrimaryText;
        NextMonthButton.FontSize = 13 * _fontScale;
        NextMonthButton.Foreground = _theme.PrimaryText;
        HintText.FontSize = 10 * _fontScale;
        HintText.Foreground = _theme.MutedText;

        BuildWeekdayHeader();
        BuildDDayPanel();
        RenderMonth();
    }

    /// <summary>트레이 메뉴에서도 같은 창을 열 수 있게 공개해 둔다.</summary>
    public void OpenAppearanceDialog() => AppearanceMenuItem_Click(this, new RoutedEventArgs());

    private void AppearanceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AppearanceDialog(_theme);
        if (dialog.ShowDialog() == true)
        {
            _theme = dialog.SelectedTheme;
            _theme.Save(_settings);
            ApplyTheme();
        }
    }

    private void HelpMenuItem_Click(object sender, RoutedEventArgs e) =>
        new HelpWindow().ShowDialog();

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
            ApplyTheme();
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
                Foreground = i == 0 ? _theme.SundayText : i == 6 ? _theme.SaturdayText : _theme.PrimaryText,
            });
        }
    }

    private void RenderMonth()
    {
        MonthYearText.Text = _displayedMonth.ToString("yyyy년 M월", Korean) + "  ▾";

        var itemsByDate = new Dictionary<DateOnly, List<DayItem>>();

        foreach (var occurrence in _scheduleRepository.GetOccurrencesByMonth(_displayedMonth.Year, _displayedMonth.Month))
        {
            AddSpanningItem(itemsByDate,
                new DayItem(occurrence.Title, occurrence.StartAt, occurrence.IsAllDay, IsGoogle: false, occurrence.Color),
                occurrence.StartAt, occurrence.EndAt);
        }

        // 구글 일정 병합 (DESIGN.md 4.5 — 토글이 꺼져 있으면 캐시가 있어도 표시하지 않음)
        if (_googleSettings.ShowEvents)
        {
            foreach (var googleEvent in _googleEventRepository.GetByMonth(_displayedMonth.Year, _displayedMonth.Month))
            {
                AddSpanningItem(itemsByDate,
                    new DayItem(googleEvent.Title, googleEvent.StartAt, googleEvent.IsAllDay, IsGoogle: true, null),
                    googleEvent.StartAt, googleEvent.EndAt);
            }
        }

        foreach (var list in itemsByDate.Values)
            list.Sort((a, b) => a.StartAt.CompareTo(b.StartAt));

        // 내장 계산 공휴일을 먼저 채워 넣어야(동기) 이번 렌더링에 바로 반영된다.
        EnsureBuiltinHolidaysForYear(_displayedMonth.Year);

        var holidaysByDate = _holidayRepository.GetByMonth(_displayedMonth.Year, _displayedMonth.Month)
            .ToDictionary(h => h.Date);
        EnsureHolidaysForYearAsync(_displayedMonth.Year);
        EnsureGoogleEventsForMonth(_displayedMonth);

        DaysGrid.Children.Clear();

        var startOffset = (int)_displayedMonth.DayOfWeek;
        var gridStart = _displayedMonth.AddDays(-startOffset);

        for (int i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var dateOnly = DateOnly.FromDateTime(date);
            itemsByDate.TryGetValue(dateOnly, out var dayItems);
            holidaysByDate.TryGetValue(dateOnly, out var holiday);
            DaysGrid.Children.Add(BuildDayCell(date, dayItems, holiday));
        }
    }

    /// <summary>여러 날에 걸친 일정을 겹치는 모든 날짜 칸에 넣는다 (DESIGN.md 4.3).</summary>
    private static void AddSpanningItem(
        Dictionary<DateOnly, List<DayItem>> itemsByDate, DayItem item, DateTime startAt, DateTime endAt)
    {
        var start = DateOnly.FromDateTime(startAt);
        var end = DateOnly.FromDateTime(endAt);
        for (var cursor = start; cursor <= end; cursor = cursor.AddDays(1))
        {
            if (!itemsByDate.TryGetValue(cursor, out var list))
                itemsByDate[cursor] = list = [];
            list.Add(item);
        }
    }

    /// <summary>표시할 달이 구글 캐시 구간 밖이면 그 달을 기준으로 다시 동기화한다.</summary>
    private void EnsureGoogleEventsForMonth(DateTime month)
    {
        if (!_googleSettings.IsConnected)
            return;

        var monthStart = new DateTime(month.Year, month.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        if (monthStart >= _googleCachedRangeStart && monthEnd <= _googleCachedRangeEnd)
            return;

        SyncGoogleEventsAsync(month);
    }

    /// <summary>
    /// 구글 일정을 백그라운드로 받아온다. 실패해도 위젯 동작을 막지 않도록 조용히 넘어가고,
    /// 자세한 오류는 설정 창의 "지금 동기화"에서 확인할 수 있다.
    /// </summary>
    private async void SyncGoogleEventsAsync(DateTime anchorMonth)
    {
        if (_isSyncingGoogle || !_googleSettings.IsConnected)
            return;

        _isSyncingGoogle = true;
        try
        {
            var syncService = new GoogleSyncService(_googleSettings, _googleEventRepository, _googleDataStore);
            var result = await syncService.SyncAsync(anchorMonth);
            if (!result.Success)
                return;

            _googleCachedRangeStart = result.RangeStart;
            _googleCachedRangeEnd = result.RangeEnd;
            RenderMonth();
        }
        finally
        {
            _isSyncingGoogle = false;
        }
    }

    private void ApplyGoogleSyncSchedule()
    {
        _googleSyncTimer.Stop();
        if (!_googleSettings.IsConnected)
            return;

        _googleSyncTimer.Interval = TimeSpan.FromMinutes(_googleSettings.SyncIntervalMinutes);
        _googleSyncTimer.Start();
    }

    private void GoogleSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var window = new GoogleSettingsWindow(_googleSettings, _googleEventRepository, _googleDataStore);
        window.ShowDialog();

        // 계정/캘린더/주기가 바뀌었을 수 있으므로 캐시 구간을 무효화하고 다시 받아온다.
        _googleCachedRangeStart = DateTime.MinValue;
        _googleCachedRangeEnd = DateTime.MinValue;

        ApplyGoogleSyncSchedule();
        RenderMonth();
    }

    /// <summary>
    /// API 키가 없어도 공휴일이 보이도록, 앱이 직접 계산한 공휴일을 그 연도에 한 번 채워 넣는다
    /// (DESIGN.md 4.2 — API 데이터가 들어오면 그쪽으로 교체된다).
    /// </summary>
    private void EnsureBuiltinHolidaysForYear(int year)
    {
        if (_holidayRepository.IsYearCached(year) || _holidayRepository.IsBuiltinYearApplied(year))
            return;

        _holidayRepository.ApplyBuiltinYear(year, KoreanHolidayCalculator.GetHolidays(year));
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

    private Border BuildDayCell(DateTime date, List<DayItem>? dayItems, Holiday? holiday)
    {
        var isCurrentMonth = date.Month == _displayedMonth.Month;
        var isToday = date.Date == DateTime.Today;

        Brush numberForeground = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => _theme.SundayText,
            DayOfWeek.Saturday => _theme.SaturdayText,
            _ => _theme.PrimaryText,
        };

        if (holiday is not null)
            numberForeground = _theme.HolidayText;

        if (!isCurrentMonth)
            numberForeground = _theme.FadedText;

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
                Foreground = _theme.HolidayText,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(3, 0, 3, 2),
            };
            DockPanel.SetDock(holidayText, Dock.Top);
            content.Children.Add(holidayText);
        }

        var eventsPanel = new StackPanel { Margin = new Thickness(2, 0, 2, 2) };
        if (dayItems is { Count: > 0 })
        {
            var visible = dayItems.Take(MaxVisibleSchedulesPerCell);
            foreach (var item in visible)
                eventsPanel.Children.Add(BuildItemChip(item, _fontScale));

            var overflow = dayItems.Count - MaxVisibleSchedulesPerCell;
            if (overflow > 0)
            {
                eventsPanel.Children.Add(new TextBlock
                {
                    Text = $"+{overflow}개",
                    FontSize = 9 * _fontScale,
                    Foreground = _theme.MutedText,
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

    private static Border BuildItemChip(DayItem item, double fontScale)
    {
        // 색을 고르지 않은 일정은 기본 파랑, 구글 일정은 초록 계열로 구분해서 그린다.
        var chipBrush = ScheduleColors.ToBrush(item.Color, item.IsGoogle);
        var text = item.IsAllDay ? item.Title : $"{item.StartAt:HH:mm} {item.Title}";

        return new Border
        {
            Background = chipBrush,
            ToolTip = item.IsGoogle ? $"[구글] {item.Title}" : null,
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

    private void DayCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // 패널 드래그로 전파되지 않도록 막음

        if (sender is not Border { Tag: DateOnly date })
            return;

        var googleEvents = _googleSettings.ShowEvents
            ? _googleEventRepository.GetByDate(date)
            : [];

        var dialog = new DayEventsWindow(date, _scheduleRepository, _holidayRepository, googleEvents);
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

    /// <summary>날짜 칸에 칩으로 그릴 항목. 로컬 일정과 구글 일정을 같은 모양으로 다루기 위한 표시용 모델.</summary>
    private sealed record DayItem(string Title, DateTime StartAt, bool IsAllDay, bool IsGoogle, string? Color);
}

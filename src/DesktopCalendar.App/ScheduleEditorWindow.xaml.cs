using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

public partial class ScheduleEditorWindow : Window
{
    private const double SwatchSize = 26;

    private readonly Guid? _originalId;

    /// <summary>선택한 색(#RRGGBB). null이면 기본색으로 저장한다.</summary>
    private string? _selectedColor;

    /// <summary>기존 일정에서 이어받는 "이 날짜는 건너뛰기" 목록. 편집해도 유지되어야 한다.</summary>
    private IReadOnlyList<DateOnly> _recurrenceExceptions = [];

    public Schedule? Result { get; private set; }

    public ScheduleEditorWindow(DateOnly date, Schedule? existing)
    {
        InitializeComponent();

        _originalId = existing?.Id;

        if (existing is not null)
        {
            TitleBox.Text = existing.Title;
            DescriptionBox.Text = existing.Description ?? string.Empty;
            IsAllDayCheckBox.IsChecked = existing.IsAllDay;
            StartDatePicker.SelectedDate = existing.StartAt.Date;
            EndDatePicker.SelectedDate = existing.EndAt.Date;
            StartTimeBox.Text = existing.StartAt.ToString("HH:mm");
            EndTimeBox.Text = existing.EndAt.ToString("HH:mm");
            _selectedColor = existing.Color;
            _recurrenceExceptions = existing.RecurrenceExceptions;

            if (existing.RecurrenceUntil is { } until)
                RecurrenceUntilPicker.SelectedDate = until.ToDateTime(TimeOnly.MinValue);
        }
        else
        {
            var defaultDate = date.ToDateTime(TimeOnly.MinValue);
            StartDatePicker.SelectedDate = defaultDate;
            EndDatePicker.SelectedDate = defaultDate;
            StartTimeBox.Text = "09:00";
            EndTimeBox.Text = "10:00";
        }

        BuildColorSwatches();
        BuildReminderOptions(existing?.ReminderMinutesBefore);
        BuildRecurrenceOptions(existing?.Recurrence ?? RecurrenceType.None);
        UpdateTimeFieldsEnabled();
        UpdateRecurrenceFieldsEnabled();
    }

    /// <summary>반복 주기 선택지.</summary>
    private static IReadOnlyList<RecurrenceOption> RecurrenceOptions { get; } =
    [
        new("반복 안 함", RecurrenceType.None),
        new("매일", RecurrenceType.Daily),
        new("매주", RecurrenceType.Weekly),
        new("매월", RecurrenceType.Monthly),
        new("매년", RecurrenceType.Yearly),
    ];

    private void BuildRecurrenceOptions(RecurrenceType current)
    {
        RecurrenceComboBox.ItemsSource = RecurrenceOptions;
        RecurrenceComboBox.SelectedItem =
            RecurrenceOptions.FirstOrDefault(o => o.Type == current) ?? RecurrenceOptions[0];
    }

    private void RecurrenceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateRecurrenceFieldsEnabled();

    private void UpdateRecurrenceFieldsEnabled()
    {
        // XAML 로드 도중에는 아직 컨트롤이 없을 수 있다.
        if (RecurrenceUntilPicker is null)
            return;

        var isRecurring = SelectedRecurrence != RecurrenceType.None;
        RecurrenceUntilPicker.IsEnabled = isRecurring;
        if (!isRecurring)
            RecurrenceUntilPicker.SelectedDate = null;
    }

    private RecurrenceType SelectedRecurrence =>
        (RecurrenceComboBox.SelectedItem as RecurrenceOption)?.Type ?? RecurrenceType.None;

    /// <summary>알림 선택지. 값이 null이면 알리지 않는다.</summary>
    private static IReadOnlyList<ReminderOption> ReminderOptions { get; } =
    [
        new("알림 없음", null),
        new("시작할 때", 0),
        new("5분 전", 5),
        new("10분 전", 10),
        new("30분 전", 30),
        new("1시간 전", 60),
        new("2시간 전", 120),
        new("1일 전", 24 * 60),
    ];

    private void BuildReminderOptions(int? currentMinutes)
    {
        ReminderComboBox.ItemsSource = ReminderOptions;
        ReminderComboBox.SelectedItem =
            ReminderOptions.FirstOrDefault(o => o.Minutes == currentMinutes) ?? ReminderOptions[0];
    }

    private void BuildColorSwatches()
    {
        foreach (var (name, hex) in ScheduleColors.Palette)
        {
            var swatch = new Border
            {
                Width = SwatchSize,
                Height = SwatchSize,
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(0, 0, 6, 0),
                Background = ScheduleColors.ToBrush(hex),
                Cursor = Cursors.Hand,
                ToolTip = name,
                Tag = hex,
            };
            swatch.MouseLeftButtonDown += ColorSwatch_MouseLeftButtonDown;
            ColorPanel.Children.Add(swatch);
        }

        UpdateSwatchSelection();
    }

    private void ColorSwatch_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border swatch)
            return;

        _selectedColor = swatch.Tag as string;
        UpdateSwatchSelection();
    }

    /// <summary>선택된 스와치에만 테두리를 둘러 표시한다.</summary>
    private void UpdateSwatchSelection()
    {
        foreach (var child in ColorPanel.Children)
        {
            if (child is not Border swatch)
                continue;

            var isSelected = string.Equals(swatch.Tag as string, _selectedColor, StringComparison.OrdinalIgnoreCase);
            swatch.BorderBrush = isSelected ? Brushes.Black : Brushes.LightGray;
            swatch.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }
    }

    private void StartDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StartDatePicker.SelectedDate is not DateTime startDate)
            return;

        // 종료 날짜가 비어있거나 시작 날짜보다 이전이면 시작 날짜로 맞춰준다 (다일 일정은 사용자가 직접 뒤로 조정)
        if (EndDatePicker.SelectedDate is not DateTime endDate || endDate < startDate)
            EndDatePicker.SelectedDate = startDate;
    }

    private void IsAllDayCheckBox_CheckedChanged(object sender, RoutedEventArgs e) => UpdateTimeFieldsEnabled();

    private void UpdateTimeFieldsEnabled()
    {
        var enabled = IsAllDayCheckBox.IsChecked != true;
        StartTimeBox.IsEnabled = enabled;
        EndTimeBox.IsEnabled = enabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            MessageBox.Show("제목을 입력하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (StartDatePicker.SelectedDate is not DateTime startDate || EndDatePicker.SelectedDate is not DateTime endDate)
        {
            MessageBox.Show("시작/종료 날짜를 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (endDate.Date < startDate.Date)
        {
            MessageBox.Show("종료 날짜가 시작 날짜보다 빠를 수 없습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var isAllDay = IsAllDayCheckBox.IsChecked == true;
        DateTime startAt;
        DateTime endAt;

        if (isAllDay)
        {
            startAt = startDate.Date;
            endAt = endDate.Date;
        }
        else
        {
            if (!TimeOnly.TryParse(StartTimeBox.Text, out var startTime) ||
                !TimeOnly.TryParse(EndTimeBox.Text, out var endTime))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다 (예: 09:00).", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            startAt = startDate.Date + startTime.ToTimeSpan();
            endAt = endDate.Date + endTime.ToTimeSpan();

            if (endAt <= startAt)
            {
                MessageBox.Show("종료 일시가 시작 일시보다 늦어야 합니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        Result = new Schedule
        {
            Id = _originalId ?? Guid.NewGuid(),
            Title = TitleBox.Text.Trim(),
            Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim(),
            StartAt = startAt,
            EndAt = endAt,
            IsAllDay = isAllDay,
            Color = _selectedColor,
            ReminderMinutesBefore = (ReminderComboBox.SelectedItem as ReminderOption)?.Minutes,
            Recurrence = SelectedRecurrence,
            RecurrenceUntil = SelectedRecurrence == RecurrenceType.None || RecurrenceUntilPicker.SelectedDate is null
                ? null
                : DateOnly.FromDateTime(RecurrenceUntilPicker.SelectedDate.Value),
            RecurrenceExceptions = SelectedRecurrence == RecurrenceType.None ? [] : _recurrenceExceptions,
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>알림 콤보박스 항목. <paramref name="Minutes"/>가 null이면 알림 없음.</summary>
    public sealed record ReminderOption(string Label, int? Minutes);

    /// <summary>반복 콤보박스 항목.</summary>
    public sealed record RecurrenceOption(string Label, RecurrenceType Type);
}

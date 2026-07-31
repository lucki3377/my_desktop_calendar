using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.App;

public partial class ScheduleEditorWindow : Window
{
    private readonly Guid? _originalId;

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
        }
        else
        {
            var defaultDate = date.ToDateTime(TimeOnly.MinValue);
            StartDatePicker.SelectedDate = defaultDate;
            EndDatePicker.SelectedDate = defaultDate;
            StartTimeBox.Text = "09:00";
            EndTimeBox.Text = "10:00";
        }

        UpdateTimeFieldsEnabled();
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
        };

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

using System.Globalization;
using System.Windows;
using DesktopCalendar.Core.Calendar;
using DesktopCalendar.Core.Google;
using DesktopCalendar.Core.Holiday;

namespace DesktopCalendar.App;

public partial class DayEventsWindow : Window
{
    private static readonly CultureInfo Korean = new("ko-KR");

    private readonly DateOnly _date;
    private readonly ScheduleRepository _repository;
    private readonly HolidayRepository _holidayRepository;

    /// <summary>구글에서 가져온 일정(읽기 전용). 표시 토글이 꺼져 있으면 비어 있다.</summary>
    private readonly IReadOnlyList<GoogleEvent> _googleEvents;

    public DayEventsWindow(
        DateOnly date,
        ScheduleRepository repository,
        HolidayRepository holidayRepository,
        IReadOnlyList<GoogleEvent>? googleEvents = null)
    {
        InitializeComponent();
        _date = date;
        _repository = repository;
        _holidayRepository = holidayRepository;
        _googleEvents = googleEvents ?? [];

        DateTitleText.Text = date.ToDateTime(TimeOnly.MinValue).ToString("yyyy년 M월 d일 (ddd)", Korean);
        LoadList();
        LoadHolidayStatus();
    }

    private void LoadHolidayStatus()
    {
        var holiday = _holidayRepository.GetByDate(_date);
        if (holiday is not null)
        {
            HolidayStatusText.Text = $"공휴일: {holiday.Name}";
            HolidayToggleButton.Content = "공휴일 해제";
        }
        else
        {
            HolidayStatusText.Text = "공휴일 아님";
            HolidayToggleButton.Content = "공휴일로 추가";
        }
    }

    private void HolidayToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var existing = _holidayRepository.GetByDate(_date);
        if (existing is not null)
        {
            _holidayRepository.Remove(_date);
        }
        else
        {
            var inputDialog = new SimpleInputDialog("공휴일 이름을 입력하세요.", "공휴일로 추가", "임시공휴일");
            if (inputDialog.ShowDialog() != true || inputDialog.InputText is null)
                return;

            _holidayRepository.AddManual(_date, inputDialog.InputText);
        }

        LoadHolidayStatus();
    }

    private void LoadList()
    {
        var entries = _repository.GetByDate(_date)
            .Select(s => new ScheduleListItem(s, Format(s.StartAt, s.EndAt, s.IsAllDay, s.Title)))
            .ToList();

        entries.AddRange(_googleEvents.Select(g => new ScheduleListItem(
            null, "[구글] " + Format(g.StartAt, g.EndAt, g.IsAllDay, g.Title))));

        ScheduleListBox.ItemsSource = entries;
    }

    private static string Format(DateTime startAt, DateTime endAt, bool isAllDay, string title)
    {
        var isMultiDay = startAt.Date != endAt.Date;
        var rangePrefix = isMultiDay ? $"{startAt:M/d}~{endAt:M/d} " : string.Empty;

        return isAllDay
            ? $"{rangePrefix}[종일] {title}"
            : $"{rangePrefix}{startAt:HH:mm}~{endAt:HH:mm}  {title}";
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new ScheduleEditorWindow(_date, null);
        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            _repository.Add(editor.Result);
            LoadList();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleListBox.SelectedItem is not ScheduleListItem item)
        {
            MessageBox.Show("수정할 일정을 목록에서 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (item.Schedule is null)
        {
            ShowGoogleReadOnlyNotice();
            return;
        }

        var editor = new ScheduleEditorWindow(_date, item.Schedule);
        if (editor.ShowDialog() == true && editor.Result is not null)
        {
            _repository.Update(editor.Result);
            LoadList();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ScheduleListBox.SelectedItem is not ScheduleListItem item)
        {
            MessageBox.Show("삭제할 일정을 목록에서 선택하세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (item.Schedule is null)
        {
            ShowGoogleReadOnlyNotice();
            return;
        }

        var confirm = MessageBox.Show($"'{item.Schedule.Title}' 일정을 삭제할까요?", "삭제 확인",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm == MessageBoxResult.Yes)
        {
            _repository.Delete(item.Schedule.Id);
            LoadList();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private static void ShowGoogleReadOnlyNotice() =>
        MessageBox.Show(
            "구글 캘린더에서 가져온 일정은 읽기 전용입니다. 구글 캘린더에서 직접 수정하세요.",
            "안내", MessageBoxButton.OK, MessageBoxImage.Information);

    /// <summary><paramref name="Schedule"/>이 null이면 구글에서 가져온 읽기 전용 항목이다.</summary>
    private sealed record ScheduleListItem(Schedule? Schedule, string Display)
    {
        public override string ToString() => Display;
    }
}

using System.Globalization;
using System.Windows.Threading;
using DesktopCalendar.Core.Calendar;
using DesktopCalendar.Core.Storage;
using Hardcodet.Wpf.TaskbarNotification;

namespace DesktopCalendar.App;

/// <summary>
/// 일정 알림을 트레이 풍선으로 띄우는 백그라운드 서비스 (DESIGN.md 4.10).
/// 판단 로직 자체는 <see cref="ReminderPlanner"/>(테스트 가능한 순수 로직)에 있고,
/// 여기서는 주기적으로 DB를 훑어 넘기고 결과를 화면에 띄우는 일만 한다.
/// </summary>
public sealed class ReminderService(TaskbarIcon trayIcon) : IDisposable
{
    private const string LastCheckedKey = "Reminder.LastCheckedAt";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>앱이 꺼져 있던 동안 밀린 알림을 이 시간까지만 소급해서 띄운다.</summary>
    private static readonly TimeSpan MaxLookback = TimeSpan.FromMinutes(10);

    private readonly ScheduleRepository _repository = new();
    private readonly SettingsStore _settings = new();
    private readonly DispatcherTimer _timer = new() { Interval = CheckInterval };

    public void Start()
    {
        _timer.Tick += (_, _) => CheckDueReminders();
        _timer.Start();
        CheckDueReminders();
    }

    private void CheckDueReminders()
    {
        var now = DateTime.Now;
        var after = ReminderPlanner.ClampLookback(LoadLastChecked(), now, MaxLookback);

        // 알림은 최대 1일 전까지만 걸 수 있으므로, 그 범위의 회차만 살펴보면 충분하다.
        var rangeEnd = now.AddMinutes(ReminderPlanner.MaxMinutesBefore);
        var candidates = _repository.GetReminderCandidates(after, rangeEnd);
        var occurrences = RecurrenceExpander.ExpandAll(candidates, after, rangeEnd);

        foreach (var occurrence in ReminderPlanner.SelectDue(occurrences, after, now))
            Notify(occurrence);

        SaveLastChecked(now);
    }

    private void Notify(ScheduleOccurrence occurrence)
    {
        var when = occurrence.IsAllDay
            ? occurrence.StartAt.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR"))
            : occurrence.StartAt.ToString("HH:mm");

        var message = $"{when} · {ReminderPlanner.DescribeLeadTime(occurrence)}";
        trayIcon.ShowBalloonTip(occurrence.Title, message, BalloonIcon.Info);
    }

    private DateTime? LoadLastChecked()
    {
        var raw = _settings.GetString(LastCheckedKey);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    private void SaveLastChecked(DateTime value) =>
        _settings.SetString(LastCheckedKey, value.ToString("o", CultureInfo.InvariantCulture));

    public void Dispose() => _timer.Stop();
}

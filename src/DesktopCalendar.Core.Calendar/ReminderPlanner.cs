namespace DesktopCalendar.Core.Calendar;

/// <summary>
/// "지금 알려야 할 일정"을 고르는 순수 로직 (DESIGN.md 4.10).
/// 타이머가 정확한 시각에 깨어난다는 보장이 없으므로, 시각을 비교하는 대신
/// "지난번에 확인한 시점 이후 ~ 지금까지" 구간에 알림 시각이 들어왔는지로 판단한다.
/// </summary>
public static class ReminderPlanner
{
    /// <summary>알림을 최대 며칠 전까지 걸 수 있는지 (1일).</summary>
    public const int MaxMinutesBefore = 24 * 60;

    /// <summary>
    /// 앱이 꺼져 있던 동안 밀린 알림이 한꺼번에 쏟아지지 않도록 조회 시작점을 자른다.
    /// 마지막 확인 시점이 없거나 너무 오래됐으면 <paramref name="maxLookback"/>만큼만 거슬러 올라간다.
    /// </summary>
    public static DateTime ClampLookback(DateTime? lastCheckedAt, DateTime now, TimeSpan maxLookback)
    {
        var earliest = now - maxLookback;
        return lastCheckedAt is null || lastCheckedAt.Value < earliest ? earliest : lastCheckedAt.Value;
    }

    /// <summary>
    /// 알림 시각이 (<paramref name="after"/>, <paramref name="now"/>] 구간에 들어온 회차들.
    /// 구간을 반열림으로 둬서 같은 알림이 두 번 나가지 않는다.
    /// 반복 일정도 회차 단위로 판단하므로 매 회차마다 제때 알림이 간다.
    /// </summary>
    public static IReadOnlyList<ScheduleOccurrence> SelectDue(
        IEnumerable<ScheduleOccurrence> occurrences, DateTime after, DateTime now) =>
        [.. occurrences
            .Where(o => o.ReminderAt is { } reminderAt && reminderAt > after && reminderAt <= now)
            .OrderBy(o => o.StartAt)];

    /// <summary>알림 문구에 쓸 "언제 시작하는지" 표현.</summary>
    public static string DescribeLeadTime(ScheduleOccurrence occurrence)
    {
        if (occurrence.ReminderMinutesBefore is not { } minutes || minutes <= 0)
            return "지금 시작합니다.";

        return minutes switch
        {
            < 60 => $"{minutes}분 뒤에 시작합니다.",
            MaxMinutesBefore => "내일 시작합니다.",
            _ when minutes % 60 == 0 => $"{minutes / 60}시간 뒤에 시작합니다.",
            _ => $"{minutes / 60}시간 {minutes % 60}분 뒤에 시작합니다.",
        };
    }
}

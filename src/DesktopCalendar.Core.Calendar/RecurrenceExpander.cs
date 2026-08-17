namespace DesktopCalendar.Core.Calendar;

/// <summary>반복 일정을 펼친 하나의 회차. 원본 일정과 그 회차의 실제 시각을 함께 들고 있다.</summary>
public sealed record ScheduleOccurrence(Schedule Schedule, DateTime StartAt, DateTime EndAt)
{
    public string Title => Schedule.Title;
    public bool IsAllDay => Schedule.IsAllDay;
    public string? Color => Schedule.Color;
    public int? ReminderMinutesBefore => Schedule.ReminderMinutesBefore;

    /// <summary>이 회차의 알림 시각. 알림이 꺼져 있으면 null.</summary>
    public DateTime? ReminderAt =>
        ReminderMinutesBefore is null ? null : StartAt.AddMinutes(-ReminderMinutesBefore.Value);

    /// <summary>이 회차가 시작하는 날짜. 반복 일정의 개별 회차를 가리킬 때 쓰는 키다.</summary>
    public DateOnly StartDate => DateOnly.FromDateTime(StartAt);
}

/// <summary>
/// 반복 일정을 특정 기간의 회차들로 펼치는 순수 로직 (DESIGN.md 4.11).
///
/// 없는 날짜는 <b>당기지 않고 건너뛴다</b>: 매달 31일 반복은 2월에 열리지 않고,
/// 2월 29일 매년 반복은 평년에 열리지 않는다. 흔한 캘린더 앱들과 같은 동작이다.
/// (D-day의 매년 반복은 "며칠 남았는지"를 늘 보여줘야 해서 2/28로 당기는데, 그건 별개 규칙이다.)
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>한 번의 펼치기에서 만들어낼 수 있는 회차 수 상한 (무기한 반복이 폭주하지 않도록).</summary>
    private const int MaxOccurrences = 1000;

    /// <summary>
    /// [<paramref name="rangeStart"/>, <paramref name="rangeEnd"/>) 구간과 겹치는 회차들을 돌려준다.
    /// </summary>
    public static IReadOnlyList<ScheduleOccurrence> Expand(
        Schedule schedule, DateTime rangeStart, DateTime rangeEnd)
    {
        if (!schedule.IsRecurring)
        {
            return Overlaps(schedule.StartAt, schedule.EndAt, rangeStart, rangeEnd)
                ? [new ScheduleOccurrence(schedule, schedule.StartAt, schedule.EndAt)]
                : [];
        }

        var duration = schedule.EndAt - schedule.StartAt;
        var exceptions = schedule.RecurrenceExceptions.ToHashSet();
        var results = new List<ScheduleOccurrence>();

        // 구간보다 한참 앞에서 시작하는 반복이면 앞부분은 계산하지 않고 건너뛴다.
        var index = FirstIndexNear(schedule, duration, rangeStart);

        for (var produced = 0; produced < MaxOccurrences; produced++, index++)
        {
            var start = OccurrenceStart(schedule, index);
            if (start is null)
                continue; // 그 주기에 존재하지 않는 날짜 (2월 31일 등)

            if (start.Value >= rangeEnd)
                break;

            var startDate = DateOnly.FromDateTime(start.Value);
            if (schedule.RecurrenceUntil is { } until && startDate > until)
                break;

            var end = start.Value + duration;
            if (end < rangeStart || exceptions.Contains(startDate))
                continue;

            results.Add(new ScheduleOccurrence(schedule, start.Value, end));
        }

        return results;
    }

    /// <summary>여러 일정을 한꺼번에 펼쳐 시작 시각 순으로 돌려준다.</summary>
    public static IReadOnlyList<ScheduleOccurrence> ExpandAll(
        IEnumerable<Schedule> schedules, DateTime rangeStart, DateTime rangeEnd) =>
        [.. schedules
            .SelectMany(s => Expand(s, rangeStart, rangeEnd))
            .OrderBy(o => o.StartAt)];

    /// <summary><paramref name="index"/>번째 회차의 시작 시각. 그 주기에 없는 날짜면 null.</summary>
    private static DateTime? OccurrenceStart(Schedule schedule, int index)
    {
        var origin = schedule.StartAt;
        if (index == 0)
            return origin;

        switch (schedule.Recurrence)
        {
            case RecurrenceType.Daily:
                return origin.AddDays(index);

            case RecurrenceType.Weekly:
                return origin.AddDays(index * 7L);

            case RecurrenceType.Monthly:
            {
                var month = origin.Month - 1 + index;
                var year = origin.Year + (int)Math.Floor(month / 12.0);
                month = ((month % 12) + 12) % 12 + 1;
                return year is < 1 or > 9999 || DateTime.DaysInMonth(year, month) < origin.Day
                    ? null
                    : new DateTime(year, month, origin.Day) + origin.TimeOfDay;
            }

            case RecurrenceType.Yearly:
            {
                var year = origin.Year + index;
                return year is < 1 or > 9999 || DateTime.DaysInMonth(year, origin.Month) < origin.Day
                    ? null
                    : new DateTime(year, origin.Month, origin.Day) + origin.TimeOfDay;
            }

            default:
                return index == 0 ? origin : null;
        }
    }

    /// <summary>
    /// <paramref name="rangeStart"/> 근처에서 시작하는 회차 번호를 어림한다.
    /// 넉넉하게 하나 앞에서 시작해, 구간에 걸쳐 있는 다일 회차를 놓치지 않게 한다.
    /// </summary>
    private static int FirstIndexNear(Schedule schedule, TimeSpan duration, DateTime rangeStart)
    {
        // 다일 일정은 시작이 구간보다 앞서도 구간에 걸쳐 있을 수 있으므로 그만큼 뒤로 물러선다.
        var target = rangeStart - duration;
        if (target <= schedule.StartAt)
            return 0;

        var index = schedule.Recurrence switch
        {
            RecurrenceType.Daily => (int)(target - schedule.StartAt).TotalDays,
            RecurrenceType.Weekly => (int)((target - schedule.StartAt).TotalDays / 7),
            RecurrenceType.Monthly => ((target.Year - schedule.StartAt.Year) * 12) + target.Month - schedule.StartAt.Month,
            RecurrenceType.Yearly => target.Year - schedule.StartAt.Year,
            _ => 0,
        };

        return Math.Max(0, index - 1);
    }

    private static bool Overlaps(DateTime start, DateTime end, DateTime rangeStart, DateTime rangeEnd) =>
        start < rangeEnd && end >= rangeStart;
}

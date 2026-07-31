namespace DesktopCalendar.Core.Calendar;

/// <summary>
/// D-day 계산 순수 로직 (DESIGN.md 4.6). WPF/저장소에 의존하지 않아 단위테스트 가능.
/// </summary>
public static class DDayCalculator
{
    /// <summary>
    /// 기준일(today)로부터 다음 발생일까지 남은 일수를 계산한다.
    /// 매년 반복 항목은 "오늘 기준 가장 가까운 다가오는 발생일"(오늘 포함)로 환산한다.
    /// 결과가 0이면 D-DAY, 양수면 D-day, 음수면 이미 지난 일회성 항목이다(반복 항목은 항상 0 이상).
    /// </summary>
    public static int ComputeDaysRemaining(DDay dday, DateOnly today)
    {
        var target = dday.IsRecurringYearly ? NextOccurrence(dday.TargetDate, today) : dday.TargetDate;
        return target.DayNumber - today.DayNumber;
    }

    /// <summary>D-7, D-DAY, D+3 형태의 표시 문자열로 변환한다.</summary>
    public static string Format(int daysRemaining) => daysRemaining switch
    {
        0 => "D-DAY",
        > 0 => $"D-{daysRemaining}",
        _ => $"D+{-daysRemaining}",
    };

    private static DateOnly NextOccurrence(DateOnly original, DateOnly today)
    {
        var candidate = SafeDate(today.Year, original.Month, original.Day);
        if (candidate < today)
            candidate = SafeDate(today.Year + 1, original.Month, original.Day);

        return candidate;
    }

    /// <summary>2월 29일처럼 해당 연도에 존재하지 않는 날짜는 그 달의 마지막 날로 보정한다.</summary>
    private static DateOnly SafeDate(int year, int month, int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, daysInMonth));
    }
}

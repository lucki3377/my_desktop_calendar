using System.Globalization;

namespace DesktopCalendar.Core.Calendar;

/// <summary>
/// 양력 날짜를 음력 표기로 바꾼다 (DESIGN.md 4.13).
/// 제사·음력 생일처럼 음력을 함께 봐야 하는 경우를 위해 날짜 칸에 작게 곁들여 보여준다.
/// </summary>
public static class KoreanLunarDate
{
    private static readonly KoreanLunisolarCalendar Lunar = new();

    /// <summary>
    /// "7.5" 형태의 음력 표기. 윤달이면 "윤7.5".
    /// <see cref="KoreanLunisolarCalendar"/>가 다루지 못하는 연도(대략 2051년 이후)면 null.
    /// </summary>
    public static string? Format(DateOnly date)
    {
        var parts = Convert(date);
        if (parts is null)
            return null;

        var (_, month, day, isLeapMonth) = parts.Value;
        return isLeapMonth ? $"윤{month}.{day}" : $"{month}.{day}";
    }

    /// <summary>
    /// 음력 날짜를 양력으로 바꾼다. 그 해에 없는 날짜(없는 윤달, 29일까지인 달의 30일 등)면 null.
    /// </summary>
    public static DateOnly? ToSolar(int lunarYear, int lunarMonth, int lunarDay, bool isLeapMonth = false)
    {
        if (lunarMonth is < 1 or > 12 || lunarDay is < 1 or > 30)
            return null;

        try
        {
            var leapMonthIndex = Lunar.GetLeapMonth(lunarYear);

            int monthIndex;
            if (isLeapMonth)
            {
                // 그 해의 윤달이 요청한 달이어야 한다 (윤6월은 leapMonthIndex가 7로 온다).
                if (leapMonthIndex == 0 || leapMonthIndex != lunarMonth + 1)
                    return null;

                monthIndex = leapMonthIndex;
            }
            else
            {
                monthIndex = leapMonthIndex > 0 && lunarMonth >= leapMonthIndex ? lunarMonth + 1 : lunarMonth;
            }

            if (lunarDay > Lunar.GetDaysInMonth(lunarYear, monthIndex))
                return null;

            return DateOnly.FromDateTime(Lunar.ToDateTime(lunarYear, monthIndex, lunarDay, 0, 0, 0, 0));
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>음력 연/월/일과 윤달 여부. 변환할 수 없으면 null.</summary>
    public static (int Year, int Month, int Day, bool IsLeapMonth)? Convert(DateOnly date)
    {
        try
        {
            var value = date.ToDateTime(TimeOnly.MinValue);
            var year = Lunar.GetYear(value);
            var monthIndex = Lunar.GetMonth(value);
            var day = Lunar.GetDayOfMonth(value);

            // GetMonth는 윤달을 하나의 월로 세므로, 윤달이 앞에 있으면 번호가 하나 밀려 있다.
            var leapMonthIndex = Lunar.GetLeapMonth(year);
            var isLeapMonth = leapMonthIndex > 0 && monthIndex == leapMonthIndex;
            var month = leapMonthIndex > 0 && monthIndex >= leapMonthIndex ? monthIndex - 1 : monthIndex;

            return (year, month, day, isLeapMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}

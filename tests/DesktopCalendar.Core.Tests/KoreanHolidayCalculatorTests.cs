using DesktopCalendar.Core.Holiday;

namespace DesktopCalendar.Core.Tests;

/// <summary>
/// 내장 공휴일 계산기 검증. 기대값은 실제로 시행된 연도별 공휴일(대체공휴일 포함)이다.
/// </summary>
public class KoreanHolidayCalculatorTests
{
    private static HashSet<DateOnly> DatesOf(int year) =>
        KoreanHolidayCalculator.GetHolidays(year).Select(h => h.Date).ToHashSet();

    [Theory]
    [InlineData(2026, 1, 1)]   // 1월 1일
    [InlineData(2026, 3, 1)]   // 삼일절
    [InlineData(2026, 5, 5)]   // 어린이날
    [InlineData(2026, 6, 6)]   // 현충일
    [InlineData(2026, 8, 15)]  // 광복절
    [InlineData(2026, 10, 3)]  // 개천절
    [InlineData(2026, 10, 9)]  // 한글날
    [InlineData(2026, 12, 25)] // 기독탄신일
    public void 양력_고정_공휴일이_포함된다(int year, int month, int day)
    {
        Assert.Contains(new DateOnly(year, month, day), DatesOf(year));
    }

    [Theory]
    // 설날 연휴 3일 (음력 1/1 기준 앞뒤 하루)
    [InlineData(2024, 2, 9, 2, 11)]
    [InlineData(2025, 1, 28, 1, 30)]
    [InlineData(2026, 2, 16, 2, 18)]
    public void 설날_연휴_3일이_계산된다(int year, int startMonth, int startDay, int endMonth, int endDay)
    {
        var dates = DatesOf(year);
        for (var date = new DateOnly(year, startMonth, startDay);
             date <= new DateOnly(year, endMonth, endDay);
             date = date.AddDays(1))
        {
            Assert.Contains(date, dates);
        }
    }

    [Theory]
    // 추석 연휴 3일 (음력 8/15 기준 앞뒤 하루)
    [InlineData(2024, 9, 16, 9, 18)]
    [InlineData(2025, 10, 5, 10, 7)]
    [InlineData(2026, 9, 24, 9, 26)]
    public void 추석_연휴_3일이_계산된다(int year, int startMonth, int startDay, int endMonth, int endDay)
    {
        var dates = DatesOf(year);
        for (var date = new DateOnly(year, startMonth, startDay);
             date <= new DateOnly(year, endMonth, endDay);
             date = date.AddDays(1))
        {
            Assert.Contains(date, dates);
        }
    }

    [Theory]
    [InlineData(2024, 5, 15)]
    [InlineData(2025, 5, 5)]  // 어린이날과 겹친 해
    [InlineData(2026, 5, 24)]
    public void 부처님오신날이_음력_4월_8일로_계산된다(int year, int month, int day)
    {
        Assert.Contains(new DateOnly(year, month, day), DatesOf(year));
    }

    [Theory]
    [InlineData(2023, 1, 24)]  // 설날(1/22)이 일요일 → 연휴 다음 첫 평일
    [InlineData(2024, 2, 12)]  // 설날 연휴 마지막날(2/11)이 일요일
    [InlineData(2024, 5, 6)]   // 어린이날(5/5)이 일요일
    [InlineData(2025, 3, 3)]   // 삼일절(3/1)이 토요일
    [InlineData(2025, 5, 6)]   // 어린이날과 부처님오신날이 같은 날(5/5)로 겹침
    [InlineData(2025, 10, 8)]  // 추석 연휴 첫날(10/5)이 일요일
    [InlineData(2026, 3, 2)]   // 삼일절(3/1)이 일요일
    [InlineData(2026, 8, 17)]  // 광복절(8/15)이 토요일
    [InlineData(2026, 10, 5)]  // 개천절(10/3)이 토요일
    [InlineData(2026, 5, 25)]  // 부처님오신날(5/24)이 일요일
    public void 대체공휴일이_지정된다(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        var holiday = KoreanHolidayCalculator.GetHolidays(year).SingleOrDefault(h => h.Date == date);

        Assert.NotNull(holiday);
        Assert.Equal(HolidayKind.SubstituteHoliday, holiday!.Kind);
    }

    [Fact]
    public void 현충일이_토요일이어도_대체공휴일이_없다()
    {
        // 2026년 현충일은 토요일이지만 현충일은 대체공휴일 대상이 아니다.
        Assert.Equal(DayOfWeek.Saturday, new DateOnly(2026, 6, 6).DayOfWeek);
        Assert.DoesNotContain(new DateOnly(2026, 6, 8), DatesOf(2026));
    }

    [Fact]
    public void 대체공휴일_제도_시행_전에는_대체공휴일이_없다()
    {
        // 2013년 삼일절은 금요일이라 무관하지만, 2013년에는 어린이날(5/5, 일요일)에도 대체공휴일이 없었다.
        Assert.Equal(DayOfWeek.Sunday, new DateOnly(2013, 5, 5).DayOfWeek);
        Assert.DoesNotContain(new DateOnly(2013, 5, 6), DatesOf(2013));
    }

    [Fact]
    public void 모든_공휴일의_출처는_Builtin이다()
    {
        Assert.All(KoreanHolidayCalculator.GetHolidays(2026), h => Assert.Equal(HolidaySource.Builtin, h.Source));
    }

    [Fact]
    public void 날짜가_중복되지_않는다()
    {
        var holidays = KoreanHolidayCalculator.GetHolidays(2025);
        Assert.Equal(holidays.Count, holidays.Select(h => h.Date).Distinct().Count());
    }
}

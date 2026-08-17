using DesktopCalendar.Core.Calendar;

namespace DesktopCalendar.Core.Tests;

/// <summary>음력 날짜 변환과 음력 매년 반복 검증.</summary>
public class LunarScheduleTests
{
    [Theory]
    // 음력 1월 1일 = 설날 (공휴일 계산기에서 이미 검증한 해들)
    [InlineData(2024, 1, 1, 2024, 2, 10)]
    [InlineData(2025, 1, 1, 2025, 1, 29)]
    [InlineData(2026, 1, 1, 2026, 2, 17)]
    // 음력 8월 15일 = 추석
    [InlineData(2026, 8, 15, 2026, 9, 25)]
    // 음력 4월 8일 = 부처님오신날
    [InlineData(2026, 4, 8, 2026, 5, 24)]
    public void 음력을_양력으로_바꾼다(
        int lunarYear, int lunarMonth, int lunarDay, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day),
            KoreanLunarDate.ToSolar(lunarYear, lunarMonth, lunarDay));
    }

    [Fact]
    public void 양력_음력_왕복_변환이_일치한다()
    {
        for (var date = new DateOnly(2026, 1, 1); date.Year == 2026; date = date.AddDays(1))
        {
            var lunar = KoreanLunarDate.Convert(date);
            Assert.NotNull(lunar);

            var (y, m, d, isLeap) = lunar!.Value;
            Assert.Equal(date, KoreanLunarDate.ToSolar(y, m, d, isLeap));
        }
    }

    [Fact]
    public void 없는_윤달은_변환되지_않는다()
    {
        // 2026년에는 윤달이 없다.
        Assert.Null(KoreanLunarDate.ToSolar(2026, 6, 15, isLeapMonth: true));
    }

    [Fact]
    public void 그_달에_없는_날짜는_변환되지_않는다()
    {
        // 29일까지인 음력 달의 30일은 존재하지 않는다. 2026년 중 그런 달을 하나 찾아 확인한다.
        var missing = Enumerable.Range(1, 12)
            .FirstOrDefault(month => KoreanLunarDate.ToSolar(2026, month, 30) is null);

        Assert.NotEqual(0, missing);
        Assert.Null(KoreanLunarDate.ToSolar(2026, missing, 30));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(13, 1)]
    [InlineData(1, 31)]
    public void 범위를_벗어난_음력_값은_null이다(int month, int day)
    {
        Assert.Null(KoreanLunarDate.ToSolar(2026, month, day));
    }

    private static Schedule LunarBirthday(DateTime startAt) => new()
    {
        Title = "음력 생일",
        StartAt = startAt,
        EndAt = startAt.AddHours(1),
        Recurrence = RecurrenceType.LunarYearly,
    };

    [Fact]
    public void 음력_매년_반복은_해마다_다른_양력_날짜에_열린다()
    {
        // 음력 1월 1일(2026년 기준 2/17)로 잡은 일정
        var schedule = LunarBirthday(new DateTime(2026, 2, 17, 9, 0, 0));

        var year2027 = RecurrenceExpander.Expand(
            schedule, new DateTime(2027, 1, 1), new DateTime(2028, 1, 1));

        Assert.Single(year2027);
        // 2027년의 음력 1월 1일(설날)은 양력 2월 7일이다 (2월 6일은 음력 섣달그믐)
        Assert.Equal(new DateOnly(2027, 2, 7), DateOnly.FromDateTime(year2027[0].StartAt));
    }

    [Fact]
    public void 음력_매년_반복은_시각을_유지한다()
    {
        var schedule = LunarBirthday(new DateTime(2026, 2, 17, 19, 30, 0));

        var occurrences = RecurrenceExpander.Expand(
            schedule, new DateTime(2027, 1, 1), new DateTime(2028, 1, 1));

        Assert.Equal(new TimeSpan(19, 30, 0), occurrences[0].StartAt.TimeOfDay);
    }

    [Fact]
    public void 음력_매년_반복은_같은_음력_날짜를_유지한다()
    {
        // 음력 8월 15일(추석)로 잡으면 몇 년이 지나도 음력으로는 늘 8월 15일이어야 한다.
        var schedule = LunarBirthday(new DateTime(2026, 9, 25, 9, 0, 0));

        for (var year = 2027; year <= 2032; year++)
        {
            var occurrences = RecurrenceExpander.Expand(
                schedule, new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));

            Assert.Single(occurrences);

            var lunar = KoreanLunarDate.Convert(occurrences[0].StartDate);
            Assert.NotNull(lunar);
            Assert.Equal(8, lunar!.Value.Month);
            Assert.Equal(15, lunar.Value.Day);
        }
    }

    [Fact]
    public void 음력_반복도_제외한_날짜를_건너뛴다()
    {
        var schedule = LunarBirthday(new DateTime(2026, 2, 17, 9, 0, 0));
        schedule.RecurrenceExceptions = [new DateOnly(2027, 2, 7)];

        Assert.Empty(RecurrenceExpander.Expand(
            schedule, new DateTime(2027, 1, 1), new DateTime(2028, 1, 1)));
    }

    [Fact]
    public void 음력_반복도_종료일_이후에는_열리지_않는다()
    {
        var schedule = LunarBirthday(new DateTime(2026, 2, 17, 9, 0, 0));
        schedule.RecurrenceUntil = new DateOnly(2026, 12, 31);

        Assert.Empty(RecurrenceExpander.Expand(
            schedule, new DateTime(2027, 1, 1), new DateTime(2028, 1, 1)));
    }
}
